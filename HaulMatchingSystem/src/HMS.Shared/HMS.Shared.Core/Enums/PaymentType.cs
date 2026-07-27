namespace HMS.Shared.Core.Enums;

/// <summary>
/// Type of payment transaction.
/// </summary>
public enum PaymentType
{
    Deposit = 0,
    FinalPayment = 1,
    AdditionalCharge = 2,
    Refund = 3
}
