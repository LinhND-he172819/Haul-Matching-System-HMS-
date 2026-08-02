namespace HMS.Shared.Core.Exceptions;

/// <summary>
/// Thrown when a user attempts an operation they are not authorized to perform.
/// Maps to HTTP 403 Forbidden.
/// Use this instead of UnauthorizedAccessException to clearly express
/// object-level authorization / permission-denied semantics.
/// </summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException) { }
}
