using HMS.Shared.Core.Enums;

namespace HMS.Shared.Core.Tests;

/// <summary>
/// Integration-style tests validating full Customer shipment lifecycle flows
/// through the ShipmentTransitionGuard + business rule interactions.
/// </summary>
public class ShipmentCustomerDriverLifecycleTests
{
    #region Customer "Đơn hàng của tôi" Lifecycle — Full happy path

    [Fact]
    public void CustomerLifecycle_DraftToPendingReview_ToPendingDeposit_ToMatched()
    {
        // Customer creates draft → submits → pending review
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.PendingReview));

        // Staff reviews, sends quotation → pending deposit
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.PendingReview, ShipmentStatus.PendingDeposit));

        // Customer pays deposit → matched
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.PendingDeposit, ShipmentStatus.Matched));

        // Driver picks up → in transit
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Matched, ShipmentStatus.In_Transit));

        // Driver delivers → delivered
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Delivered));
    }

    #endregion

    #region Customer Cancel at Various Stages

    [Fact]
    public void CustomerCancel_FromDraft_Works()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.Cancelled));
    }

    [Fact]
    public void CustomerCancel_FromPendingReview_Works()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.PendingReview, ShipmentStatus.Cancelled));
    }

    [Fact]
    public void CustomerCancel_FromPendingDeposit_Works()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.PendingDeposit, ShipmentStatus.Cancelled));
    }

    [Fact]
    public void CustomerCannotCancel_FromInTransit()
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.In_Transit, ShipmentStatus.Cancelled));
    }

    [Fact]
    public void CannotCancel_FromDelivered()
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Delivered, ShipmentStatus.Cancelled));
    }

    [Fact]
    public void CannotCancel_FromCompleted()
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Completed, ShipmentStatus.Cancelled));
    }

    #endregion

    #region Cross-cutting: Cannot skip states

    [Fact]
    public void CannotSkipFromDraft_ToInTransit_Directly()
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.In_Transit));
    }

    [Fact]
    public void CannotSkipFromDraft_ToDelivered_Directly()
    {
        Assert.False(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.Delivered));
    }

    #endregion

    #region Error scenario: Shipment delivery failure and recovery

    [Fact]
    public void ShipmentDeliveryFailed_CanRecoverOrReturn()
    {
        var status = ShipmentStatus.In_Transit;

        // In transit → delivery failed
        Assert.True(ShipmentTransitionGuard.CanTransition(status, ShipmentStatus.Delivery_Failed));
        status = ShipmentStatus.Delivery_Failed;

        // Recovery options: return to hub or pending rescue
        Assert.True(ShipmentTransitionGuard.CanTransition(status, ShipmentStatus.Returned_To_Hub));
        // OR
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Delivery_Failed, ShipmentStatus.Pending_Rescue));
    }

    #endregion

    #region ShipmentProposal: Draft → PendingReview → PendingDeposit → Matched validation

    [Fact]
    public void ProposalFlow_DraftToPendingReviewRequiresShipmentState()
    {
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.PendingReview));
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.In_Warehouse));
        Assert.True(ShipmentTransitionGuard.CanTransition(ShipmentStatus.Draft, ShipmentStatus.Matched)); // DirectPickup
    }

    [Fact]
    public void PendingReviewOnlyAllowsPendingDepositOrCancelled()
    {
        var allowed = ShipmentTransitionGuard.GetAllowedTransitions(ShipmentStatus.PendingReview);
        Assert.Equal(2, allowed.Count);
        Assert.Contains(ShipmentStatus.PendingDeposit, allowed);
        Assert.Contains(ShipmentStatus.Cancelled, allowed);
    }

    [Fact]
    public void PendingDepositOnlyAllowsMatchedOrCancelled()
    {
        var allowed = ShipmentTransitionGuard.GetAllowedTransitions(ShipmentStatus.PendingDeposit);
        Assert.Equal(3, allowed.Count);
        Assert.Contains(ShipmentStatus.Matched, allowed);
        Assert.Contains(ShipmentStatus.PendingReview, allowed);
        Assert.Contains(ShipmentStatus.Cancelled, allowed);
    }

    #endregion
}
