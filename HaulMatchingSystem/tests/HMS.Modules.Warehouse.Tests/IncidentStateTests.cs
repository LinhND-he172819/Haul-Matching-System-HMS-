using HMS.Modules.Warehouse.Application.Services;
using Xunit;

namespace HMS.Modules.Warehouse.Tests;

/// <summary>
/// Unit tests for Incident state machine (EnsureValidTransition).
/// Tests the allowed transitions:
///   Open → InProgress
///   Open → Rejected
///   InProgress → Resolved
/// And rejects invalid transitions:
///   Rejected → InProgress, Resolved → anything, etc.
/// </summary>
public class IncidentStateTests
{
    // ─── Valid Transitions ─────────────────────────────────────────

    [Theory]
    [InlineData("Open", "InProgress")]
    [InlineData("Open", "Rejected")]
    [InlineData("InProgress", "Resolved")]
    public void EnsureValidTransition_ValidTransition_DoesNotThrow(string from, string to)
    {
        var ex = Record.Exception(() => IncidentService.EnsureValidTransition(from, to));
        Assert.Null(ex);
    }

    // ─── Invalid Transitions ───────────────────────────────────────

    [Theory]
    [InlineData("Open", "Resolved")]       // Cannot skip InProgress
    [InlineData("Open", "Open")]           // No-op transition
    [InlineData("InProgress", "Rejected")] // Cannot reject in-progress
    [InlineData("InProgress", "Open")]     // Cannot go back to Open
    [InlineData("InProgress", "InProgress")] // No-op transition
    [InlineData("Rejected", "InProgress")] // Cannot reopen rejected
    [InlineData("Rejected", "Resolved")]   // Cannot resolve rejected
    [InlineData("Rejected", "Rejected")]   // No-op transition
    [InlineData("Rejected", "Open")]       // Cannot reopen rejected
    [InlineData("Resolved", "InProgress")] // Cannot go back to InProgress
    [InlineData("Resolved", "Open")]       // Cannot reopen resolved
    [InlineData("Resolved", "Rejected")]   // Cannot reject resolved
    [InlineData("Resolved", "Resolved")]   // No-op transition
    public void EnsureValidTransition_InvalidTransition_ThrowsInvalidOperation(string from, string to)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => IncidentService.EnsureValidTransition(from, to));
        Assert.Contains("Chuyển trạng thái không hợp lệ", ex.Message);
    }

    // ─── Unknown Status ────────────────────────────────────────────

    [Fact]
    public void EnsureValidTransition_UnknownStatus_ThrowsInvalidOperation()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => IncidentService.EnsureValidTransition("Unknown", "InProgress"));
        Assert.Contains("Chuyển trạng thái không hợp lệ", ex.Message);
    }
}
