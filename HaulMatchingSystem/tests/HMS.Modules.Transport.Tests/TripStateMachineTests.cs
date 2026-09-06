using HMS.Modules.Transport.Core.StateMachines;
using HMS.Shared.Core.Enums;

namespace HMS.Modules.Transport.Tests;

/// <summary>
/// Comprehensive tests for <see cref="TripStateMachine"/>.
/// Covers legacy statuses, new driver trip management statuses, and terminal states.
/// </summary>
public class TripStateMachineTests
{
    #region Legacy Active transitions

    [Fact]
    public void Active_CanTransition_To_Completed()
    {
        Assert.True(TripStateMachine.CanTransition(TripStatus.Active, TripStatus.Completed));
    }

    [Fact]
    public void Active_CanTransition_To_Breakdown()
    {
        Assert.True(TripStateMachine.CanTransition(TripStatus.Active, TripStatus.Breakdown));
    }

    [Theory]
    [InlineData(TripStatus.Ready)]
    [InlineData(TripStatus.InProgress)]
    [InlineData(TripStatus.Cancelled)]
    public void Active_CannotTransition_To_Unallowed(TripStatus target)
    {
        // Active→Scheduled is now allowed (legacy backward-compat), so use
        // genuinely disallowed targets: Ready / InProgress / Cancelled.
        Assert.False(TripStateMachine.CanTransition(TripStatus.Active, target));
    }

    #endregion

    #region Scheduled transitions (new)

    [Fact]
    public void Scheduled_CanTransition_To_Ready()
    {
        Assert.True(TripStateMachine.CanTransition(TripStatus.Scheduled, TripStatus.Ready));
    }

    [Fact]
    public void Scheduled_CanTransition_To_Cancelled()
    {
        Assert.True(TripStateMachine.CanTransition(TripStatus.Scheduled, TripStatus.Cancelled));
    }

    [Theory]
    [InlineData(TripStatus.Active)]
    [InlineData(TripStatus.InProgress)]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Breakdown)]
    public void Scheduled_CannotTransition_To_Unallowed(TripStatus target)
    {
        Assert.False(TripStateMachine.CanTransition(TripStatus.Scheduled, target));
    }

    #endregion

    #region Ready transitions (new)

    [Fact]
    public void Ready_CanTransition_To_InProgress()
    {
        Assert.True(TripStateMachine.CanTransition(TripStatus.Ready, TripStatus.InProgress));
    }

    [Fact]
    public void Ready_CanTransition_To_Cancelled()
    {
        Assert.True(TripStateMachine.CanTransition(TripStatus.Ready, TripStatus.Cancelled));
    }

    [Theory]
    [InlineData(TripStatus.Active)]
    [InlineData(TripStatus.Scheduled)]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Breakdown)]
    public void Ready_CannotTransition_To_Unallowed(TripStatus target)
    {
        Assert.False(TripStateMachine.CanTransition(TripStatus.Ready, target));
    }

    #endregion

    #region InProgress transitions (new)

    [Fact]
    public void InProgress_CanTransition_To_Completed()
    {
        Assert.True(TripStateMachine.CanTransition(TripStatus.InProgress, TripStatus.Completed));
    }

    [Theory]
    [InlineData(TripStatus.Active)]
    [InlineData(TripStatus.Scheduled)]
    [InlineData(TripStatus.Ready)]
    [InlineData(TripStatus.Cancelled)]
    public void InProgress_CannotTransition_To_Unallowed(TripStatus target)
    {
        Assert.False(TripStateMachine.CanTransition(TripStatus.InProgress, target));
    }

    [Fact]
    public void InProgress_CanTransition_To_Breakdown()
    {
        // Trips can break down mid-transit (mechanical failure, accident, etc.)
        Assert.True(TripStateMachine.CanTransition(TripStatus.InProgress, TripStatus.Breakdown));
    }

    #endregion

    #region Terminal states — no outgoing transitions

    [Theory]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Cancelled)]
    public void TerminalState_CannotTransition_To_Any(TripStatus terminal)
    {
        foreach (var target in Enum.GetValues<TripStatus>())
        {
            if (target == terminal) continue;
            Assert.False(TripStateMachine.CanTransition(terminal, target),
                $"{terminal} should not transition to {target}");
        }
    }

    [Fact]
    public void Breakdown_CanTransition_To_Cancelled()
    {
        // Trips that broke down can be cancelled (operator decision).
        Assert.True(TripStateMachine.CanTransition(TripStatus.Breakdown, TripStatus.Cancelled));
    }

    [Theory]
    [InlineData(TripStatus.Completed)]
    [InlineData(TripStatus.Ready)]
    [InlineData(TripStatus.InProgress)]
    [InlineData(TripStatus.Scheduled)]
    [InlineData(TripStatus.Active)]
    public void Breakdown_CannotTransition_To_Others(TripStatus target)
    {
        Assert.False(TripStateMachine.CanTransition(TripStatus.Breakdown, target));
    }

    #endregion

    #region EnsureCanTransition — valid transitions pass

    [Theory]
    [InlineData(TripStatus.Active, TripStatus.Completed)]
    [InlineData(TripStatus.Active, TripStatus.Breakdown)]
    [InlineData(TripStatus.Scheduled, TripStatus.Ready)]
    [InlineData(TripStatus.Scheduled, TripStatus.Cancelled)]
    [InlineData(TripStatus.Ready, TripStatus.InProgress)]
    [InlineData(TripStatus.Ready, TripStatus.Cancelled)]
    [InlineData(TripStatus.InProgress, TripStatus.Completed)]
    public void EnsureCanTransition_DoesNotThrow_ForValid(TripStatus from, TripStatus to)
    {
        var exception = Record.Exception(() => TripStateMachine.EnsureCanTransition(from, to));
        Assert.Null(exception);
    }

    #endregion

    #region EnsureCanTransition — invalid transitions throw

    [Theory]
    [InlineData(TripStatus.Scheduled, TripStatus.Completed)]
    [InlineData(TripStatus.Ready, TripStatus.Completed)]
    [InlineData(TripStatus.InProgress, TripStatus.Ready)]
    [InlineData(TripStatus.Completed, TripStatus.InProgress)]
    [InlineData(TripStatus.Cancelled, TripStatus.Scheduled)]
    public void EnsureCanTransition_Throws_ForInvalid(TripStatus from, TripStatus to)
    {
        Assert.Throws<InvalidOperationException>(() => TripStateMachine.EnsureCanTransition(from, to));
    }

    #endregion

    #region EnsureCanTransition — same status throws

    [Theory]
    [InlineData(TripStatus.Scheduled)]
    [InlineData(TripStatus.Ready)]
    [InlineData(TripStatus.InProgress)]
    [InlineData(TripStatus.Completed)]
    public void EnsureCanTransition_Throws_ForSameStatus(TripStatus status)
    {
        Assert.Throws<InvalidOperationException>(() => TripStateMachine.EnsureCanTransition(status, status));
    }

    #endregion

    #region Full lifecycle: Scheduled → Completed

    [Fact]
    public void FullLifecycle_Scheduled_To_Completed()
    {
        // Scheduled → Ready
        Assert.True(TripStateMachine.CanTransition(TripStatus.Scheduled, TripStatus.Ready));
        // Ready → InProgress
        Assert.True(TripStateMachine.CanTransition(TripStatus.Ready, TripStatus.InProgress));
        // InProgress → Completed
        Assert.True(TripStateMachine.CanTransition(TripStatus.InProgress, TripStatus.Completed));
    }

    [Fact]
    public void FullLifecycle_Scheduled_To_Cancelled_At_Ready()
    {
        // Scheduled → Ready
        Assert.True(TripStateMachine.CanTransition(TripStatus.Scheduled, TripStatus.Ready));
        // Ready → Cancelled
        Assert.True(TripStateMachine.CanTransition(TripStatus.Ready, TripStatus.Cancelled));
    }

    #endregion

    #region Cannot skip states

    [Fact]
    public void CannotSkipFromScheduled_ToCompleted_Directly()
    {
        Assert.False(TripStateMachine.CanTransition(TripStatus.Scheduled, TripStatus.Completed));
    }

    [Fact]
    public void CannotSkipFromScheduled_ToInProgress_Directly()
    {
        Assert.False(TripStateMachine.CanTransition(TripStatus.Scheduled, TripStatus.InProgress));
    }

    #endregion

    #region Legacy Active + Breakdown scenario

    [Fact]
    public void LegacyActive_CanBreakdown_AndBreakdownIsTerminal()
    {
        var status = TripStatus.Active;
        Assert.True(TripStateMachine.CanTransition(status, TripStatus.Breakdown));
        status = TripStatus.Breakdown;

        Assert.False(TripStateMachine.CanTransition(status, TripStatus.Active));
        Assert.False(TripStateMachine.CanTransition(status, TripStatus.Completed));
        Assert.False(TripStateMachine.CanTransition(status, TripStatus.InProgress));
    }

    #endregion
}
