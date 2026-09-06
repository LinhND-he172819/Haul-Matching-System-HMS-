using System.Security.Cryptography;
using HMS.Shared.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace HMS.Shared.Infrastructure.Services;

/// <summary>
/// Local disk file storage implementation.
/// Files are stored under {BasePath}/uploads/ with GUID-based filenames.
/// Replace this with S3/Azure Blob/Cloudinary implementation without changing business logic.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _basePath;
    private readonly ILogger<LocalFileStorageService> _logger;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp"
    };

    // Magic bytes for content sniffing (first 4-16 bytes of each format)
    private static readonly Dictionary<string, byte[]> FileMagicBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".jpg",  [0xFF, 0xD8, 0xFF] },      // JPEG starts with FF D8 FF
        { ".jpeg", [0xFF, 0xD8, 0xFF] },      // JPEG
        { ".png",  [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A] }, // PNG
        { ".webp", [0x52, 0x49, 0x46, 0x46] }, // RIFF (WEBP files start with RIFF)
    };

    public LocalFileStorageService(IConfiguration configuration, ILogger<LocalFileStorageService> logger)
    {
        _basePath = configuration["FileStorage:BasePath"]
            ?? Path.Combine(AppContext.BaseDirectory, "uploads");
        _logger = logger;

        // Ensure base directory exists
        if (!Directory.Exists(_basePath))
            Directory.CreateDirectory(_basePath);
    }

    public async Task<string> SaveAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default)
    {
        // Validate extension
        var ext = Path.GetExtension(originalFileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ArgumentException($"Định dạng tệp không hợp lệ: {ext}. Chỉ chấp nhận JPG, PNG, WEBP.");

        // Validate content type
        if (!AllowedContentTypes.Contains(contentType))
            throw new ArgumentException($"Loại nội dung không hợp lệ: {contentType}.");

        // Validate magic bytes (content sniffing) — prevents content-type spoofing
        // where a file with a valid extension contains arbitrary data.
        // WebP magic bytes are at offset 8 (RIFF + 4 bytes size + WEBP), so we need 12 bytes.
        var magicHeader = new byte[12];
        var bytesRead = 0;
        int read;
        while ((read = await fileStream.ReadAsync(magicHeader, bytesRead, magicHeader.Length - bytesRead, ct)) > 0)
            bytesRead += read;

        if (bytesRead < 3)
            throw new ArgumentException("Tệp tin quá nhỏ hoặc rỗng.");

        if (!FileMagicBytes.TryGetValue(ext, out var expectedMagic))
            throw new ArgumentException($"Không thể xác thực mã magic cho định dạng {ext}.");

        // For JPEG, check first 3 bytes
        // For PNG, check first 8 bytes
        // For WebP, check bytes 8-11 (the "WEBP" identifier after "RIFF....")
        bool magicValid = ext is ".webp" or ".WEBP"
            ? bytesRead >= 12
              && magicHeader[0] == 0x52 && magicHeader[1] == 0x49 && magicHeader[2] == 0x46 && magicHeader[3] == 0x46 // RIFF
              && magicHeader[8] == 0x57 && magicHeader[9] == 0x45 && magicHeader[10] == 0x42 && magicHeader[11] == 0x50 // WEBP
            : magicHeader.AsSpan(0, expectedMagic.Length).SequenceEqual(expectedMagic);

        if (!magicValid)
        {
            _logger.LogWarning("Magic byte mismatch for {FileName}: expected {Ext}, content does not match", originalFileName, ext);
            throw new ArgumentException($"Tệp tin không đúng định dạng {ext} (magic bytes không khớp).");
        }

        // Reset stream position so CopyToAsync can read from the beginning
        if (fileStream.CanSeek)
            fileStream.Seek(0, SeekOrigin.Begin);

        // Generate safe filename: GUID + original extension
        var safeFileName = $"{Guid.NewGuid()}{ext.ToLowerInvariant()}";

        // Subdirectory by date for organization
        var dateDir = DateTime.UtcNow.ToString("yyyy-MM");
        var dirPath = Path.Combine(_basePath, dateDir);
        if (!Directory.Exists(dirPath))
            Directory.CreateDirectory(dirPath);

        var filePath = Path.Combine(dirPath, safeFileName);

        // Write file
        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
        await fileStream.CopyToAsync(fs, ct);

        _logger.LogInformation("File saved: {FilePath} ({Size} bytes)", filePath, fs.Length);

        // Return relative storage key (date/safeFileName)
        return $"{dateDir}/{safeFileName}";
    }

    /// <summary>
    /// Resolves a storage key to an absolute path, rejecting path traversal attempts.
    /// </summary>
    private string ResolveSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ArgumentException("Storage key là bắt buộc.");

        // Reject rooted paths (e.g. "C:\..." or "\\server\share") and traversal segments.
        if (Path.IsPathRooted(storageKey) || storageKey.Contains(".."))
            throw new ArgumentException("Storage key không hợp lệ.");

        var fullPath = Path.GetFullPath(Path.Combine(_basePath, storageKey));
        var normalizedBase = Path.GetFullPath(_basePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Blocked path traversal attempt: {StorageKey}", storageKey);
            throw new ArgumentException("Storage key không hợp lệ.");
        }

        return fullPath;
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var filePath = ResolveSafePath(storageKey);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("File deleted: {FilePath}", filePath);
        }
        return Task.CompletedTask;
    }

    public Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var filePath = ResolveSafePath(storageKey);
        if (!File.Exists(filePath))
            return Task.FromResult<(Stream, string, string)?>(null);

        var ext = Path.GetExtension(storageKey).ToLowerInvariant();
        var contentType = ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        var fileName = Path.GetFileName(storageKey);
        return Task.FromResult<(Stream, string, string)?>((stream, contentType, fileName));
    }
}
