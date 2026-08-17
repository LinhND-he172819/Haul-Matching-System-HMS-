namespace HMS.Shared.Core.Interfaces;

/// <summary>
/// Abstraction for file storage. Implementations can use local disk, S3, Azure Blob, etc.
/// Business logic must depend on this interface, never on a concrete implementation.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Saves a file and returns a unique storage key that can be used to retrieve it later.
    /// The implementation must generate a safe, unique filename (e.g. GUID-based).
    /// </summary>
    Task<string> SaveAsync(Stream fileStream, string originalFileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file by its storage key.
    /// </summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>
    /// Opens a read-only stream for the file identified by the storage key.
    /// Returns null if the file does not exist.
    /// </summary>
    Task<(Stream Stream, string ContentType, string FileName)?> OpenReadAsync(string storageKey, CancellationToken ct = default);
}
