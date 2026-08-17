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

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storageKey);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("File deleted: {FilePath}", filePath);
        }
        return Task.CompletedTask;
    }

    public Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_basePath, storageKey);
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
