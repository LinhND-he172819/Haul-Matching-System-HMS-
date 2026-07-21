using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Exceptions;

namespace HMS.Shared.Core.Tests;

/// <summary>
/// Comprehensive tests for <see cref="ShipmentTransitionGuard"/>.
/// Covers every state in the shipment lifecycle.
/// </summary>
public class ShipmentTransitionGuardTests
{
    #region Draft transitions

    [Fact]
    public void Draft_CanTransition_To_InWarehouse()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.In_Warehouse));
    }

    [Fact]
    public void Draft_CanTransition_To_Cancelled()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.Cancelled));
    }

    [Theory]
    [InlineData(ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Delivery_Failed)]
    public void Draft_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, target));
    }

    #endregion

    #region InWarehouse transitions

    [Fact]
    public void InWarehouse_CanTransition_To_Matched()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Warehouse, ShipmentStatus.Matched));
    }

    [Fact]
    public void InWarehouse_CanTransition_To_Cancelled()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Warehouse, ShipmentStatus.Cancelled));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Delivered)]
    public void InWarehouse_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Warehouse, target));
    }

    #endregion

    #region Matched transitions

    [Fact]
    public void Matched_CanTransition_To_InTransit()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Matched, ShipmentStatus.In_Transit));
    }

    [Fact]
    public void Matched_CanTransition_To_Cancelled()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Matched, ShipmentStatus.Cancelled));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.Delivered)]
    public void Matched_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Matched, target));
    }

    #endregion

    #region InTransit transitions

    [Fact]
    public void InTransit_CanTransition_To_Delivered()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Delivered));
    }

    [Fact]
    public void InTransit_CanTransition_To_DeliveryFailed()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Delivery_Failed));
    }

    [Fact]
    public void InTransit_CanTransition_To_ArrivedAtDestinationHub()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Arrived_At_Destination_Hub));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void InTransit_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Transit, target));
    }

    #endregion

    #region DeliveryFailed transitions

    [Fact]
    public void DeliveryFailed_CanTransition_To_ReturnedToHub()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Delivery_Failed, ShipmentStatus.Returned_To_Hub));
    }

    [Fact]
    public void DeliveryFailed_CanTransition_To_PendingRescue()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Delivery_Failed, ShipmentStatus.Pending_Rescue));
    }

    [Fact]
    public void DeliveryFailed_CanTransition_To_ForcedReturn()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Delivery_Failed, ShipmentStatus.Forced_Return));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.Delivered)]
    public void DeliveryFailed_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Delivery_Failed, target));
    }

    #endregion

    #region PendingRescue transitions

    [Fact]
    public void PendingRescue_CanTransition_To_InTransit()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Pending_Rescue, ShipmentStatus.In_Transit));
    }

    [Fact]
    public void PendingRescue_CanTransition_To_ReturnedToHub()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Pending_Rescue, ShipmentStatus.Returned_To_Hub));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.Delivered)]
    public void PendingRescue_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Pending_Rescue, target));
    }

    #endregion

    #region ReturnedToHub transitions

    [Fact]
    public void ReturnedToHub_CanTransition_To_InWarehouse()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Returned_To_Hub, ShipmentStatus.In_Warehouse));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Delivered)]
    public void ReturnedToHub_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Returned_To_Hub, target));
    }

    #endregion

    #region ForcedReturn transitions

    [Fact]
    public void ForcedReturn_CanTransition_To_ReturnedToHub()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Forced_Return, ShipmentStatus.Returned_To_Hub));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.In_Transit)]
    public void ForcedReturn_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Forced_Return, target));
    }

    #endregion

    #region ArrivedAtDestinationHub transitions

    [Fact]
    public void ArrivedAtDestinationHub_CanTransition_To_Delivered()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Arrived_At_Destination_Hub, ShipmentStatus.Delivered));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void ArrivedAtDestinationHub_CannotTransition_To_Unallowed(ShipmentStatus target)
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Arrived_At_Destination_Hub, target));
    }

    #endregion

    #region Terminal states (Cancelled, Delivered) — no outgoing transitions

    [Theory]
    [InlineData(ShipmentStatus.Cancelled)]
    [InlineData(ShipmentStatus.Delivered)]
    public void TerminalState_CannotTransition_To_Any(ShipmentStatus terminal)
    {
        foreach (var target in Enum.GetValues<ShipmentStatus>())
        {
            Assert.False(ShipmentTransitionGuard.CanTransition(terminal, target),
                $"{terminal} should not transition to {target}");
        }
    }

    #endregion

    #region EnsureCanTransition — valid transitions pass

    [Theory]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.Cancelled)]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.In_Warehouse, ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.In_Warehouse, ShipmentStatus.Cancelled)]
    [InlineData(ShipmentStatus.Matched, ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Matched, ShipmentStatus.Cancelled)]
    [InlineData(ShipmentStatus.In_Transit, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.In_Transit, ShipmentStatus.Delivery_Failed)]
    [InlineData(ShipmentStatus.In_Transit, ShipmentStatus.Arrived_At_Destination_Hub)]
    [InlineData(ShipmentStatus.Delivery_Failed, ShipmentStatus.Returned_To_Hub)]
    [InlineData(ShipmentStatus.Delivery_Failed, ShipmentStatus.Pending_Rescue)]
    [InlineData(ShipmentStatus.Delivery_Failed, ShipmentStatus.Forced_Return)]
    [InlineData(ShipmentStatus.Pending_Rescue, ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Pending_Rescue, ShipmentStatus.Returned_To_Hub)]
    [InlineData(ShipmentStatus.Returned_To_Hub, ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.Forced_Return, ShipmentStatus.Returned_To_Hub)]
    [InlineData(ShipmentStatus.Arrived_At_Destination_Hub, ShipmentStatus.Delivered)]
    public void EnsureCanTransition_DoesNotThrow_ForValid(ShipmentStatus from, ShipmentStatus to)
    {
        var ex = Record.Exception(() => ShipmentTransitionGuard.EnsureCanTransition(from, to));
        Assert.Null(ex);
    }

    #endregion

    #region EnsureCanTransition — invalid transitions throw

    [Theory]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.In_Warehouse, ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Warehouse, ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Matched, ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.Matched, ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.In_Transit, ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Transit, ShipmentStatus.Cancelled)]
    [InlineData(ShipmentStatus.Delivered, ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.Cancelled, ShipmentStatus.Draft)]
    public void EnsureCanTransition_Throws_ForInvalid(ShipmentStatus from, ShipmentStatus to)
    {
        var ex = Assert.Throws<InvalidShipmentTransitionException>(
            () => ShipmentTransitionGuard.EnsureCanTransition(from, to));
        Assert.Equal(from, ex.FromStatus);
        Assert.Equal(to, ex.ToStatus);
    }

    #endregion

    #region EnsureCanTransition — same status throws

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.In_Warehouse)]
    [InlineData(ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Cancelled)]
    public void EnsureCanTransition_Throws_ForSameStatus(ShipmentStatus status)
    {
        var ex = Assert.Throws<InvalidShipmentTransitionException>(
            () => ShipmentTransitionGuard.EnsureCanTransition(status, status));
        Assert.Contains("already in this status", ex.Message);
    }

    #endregion

    #region GetAllowedTransitions

    [Fact]
    public void GetAllowedTransitions_Draft_ReturnsThree()
    {
        var allowed = ShipmentTransitionGuard.GetAllowedTransitions(ShipmentStatus.Draft);
        Assert.Equal(3, allowed.Count);
        Assert.Contains(ShipmentStatus.In_Warehouse, allowed);
        Assert.Contains(ShipmentStatus.Cancelled, allowed);
        Assert.Contains(ShipmentStatus.Matched, allowed);
    }

    [Fact]
    public void GetAllowedTransitions_InTransit_ReturnsThree()
    {
        var allowed = ShipmentTransitionGuard.GetAllowedTransitions(ShipmentStatus.In_Transit);
        Assert.Equal(3, allowed.Count);
        Assert.Contains(ShipmentStatus.Delivered, allowed);
        Assert.Contains(ShipmentStatus.Delivery_Failed, allowed);
        Assert.Contains(ShipmentStatus.Arrived_At_Destination_Hub, allowed);
    }

    [Fact]
    public void GetAllowedTransitions_Cancelled_ReturnsEmpty()
    {
        var allowed = ShipmentTransitionGuard.GetAllowedTransitions(ShipmentStatus.Cancelled);
        Assert.Empty(allowed);
    }

    [Fact]
    public void GetAllowedTransitions_Delivered_ReturnsEmpty()
    {
        var allowed = ShipmentTransitionGuard.GetAllowedTransitions(ShipmentStatus.Delivered);
        Assert.Empty(allowed);
    }

    #endregion

    #region Full lifecycle path tests

    [Fact]
    public void FullLifecycle_DraftToDelivered_ViaHub()
    {
        // Draft → In_Warehouse → Matched → In_Transit → Arrived_At_Destination_Hub → Delivered
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Draft, ShipmentStatus.In_Warehouse);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Warehouse, ShipmentStatus.Matched);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Matched, ShipmentStatus.In_Transit);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Arrived_At_Destination_Hub);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Arrived_At_Destination_Hub, ShipmentStatus.Delivered);
    }

    [Fact]
    public void FullLifecycle_DraftToDelivered_Direct()
    {
        // Draft → In_Warehouse → Matched → In_Transit → Delivered
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Draft, ShipmentStatus.In_Warehouse);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Warehouse, ShipmentStatus.Matched);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Matched, ShipmentStatus.In_Transit);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Delivered);
    }

    [Fact]
    public void FullLifecycle_FailedDelivery_Rescue_To_Delivered()
    {
        // In_Transit → Delivery_Failed → Pending_Rescue → In_Transit → Delivered
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Delivery_Failed);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Delivery_Failed, ShipmentStatus.Pending_Rescue);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Pending_Rescue, ShipmentStatus.In_Transit);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Delivered);
    }

    [Fact]
    public void FullLifecycle_FailedDelivery_ForcedReturn()
    {
        // In_Transit → Delivery_Failed → Forced_Return → Returned_To_Hub → In_Warehouse
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Delivery_Failed);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Delivery_Failed, ShipmentStatus.Forced_Return);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Forced_Return, ShipmentStatus.Returned_To_Hub);
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Returned_To_Hub, ShipmentStatus.In_Warehouse);
    }

    [Fact]
    public void FullLifecycle_CancelAtAnyPoint()
    {
        // Cancel from Draft
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Draft, ShipmentStatus.Cancelled);

        // Cancel from In_Warehouse
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.In_Warehouse, ShipmentStatus.Cancelled);

        // Cancel from Matched
        ShipmentTransitionGuard.EnsureCanTransition(ShipmentStatus.Matched, ShipmentStatus.Cancelled);
    }

    #endregion
}
