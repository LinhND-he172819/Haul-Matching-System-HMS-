using HMS.Shared.Core.Enums;
using HMS.Shared.Core.Sms;

namespace HMS.Shared.Core.Tests;

/// <summary>
/// Tests for <see cref="SmsNotificationPolicy"/> — centralized SMS trigger policy.
/// </summary>
public class SmsNotificationPolicyTests
{
    #region ShouldNotifyCustomer (enum)

    [Theory]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.PendingDeposit, ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.In_Warehouse, ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.Matched, ShipmentStatus.In_Transit)]
    [InlineData(ShipmentStatus.In_Transit, ShipmentStatus.Delivered)]
    [InlineData(ShipmentStatus.Delivered, ShipmentStatus.Completed)]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.Cancelled)]
    [InlineData(ShipmentStatus.PendingReview, ShipmentStatus.Cancelled)]
    public void ShouldNotifyCustomer_CustomerFacingTransition_ReturnsTrue(ShipmentStatus oldStatus, ShipmentStatus newStatus)
    {
        Assert.True(SmsNotificationPolicy.ShouldNotifyCustomer(oldStatus, newStatus));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.Matched, ShipmentStatus.Matched)]
    [InlineData(ShipmentStatus.In_Transit, ShipmentStatus.In_Transit)]
    public void ShouldNotifyCustomer_SameStatus_ReturnsFalse(ShipmentStatus oldStatus, ShipmentStatus newStatus)
    {
        Assert.False(SmsNotificationPolicy.ShouldNotifyCustomer(oldStatus, newStatus));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.PendingReview)]
    [InlineData(ShipmentStatus.Draft, ShipmentStatus.PendingDeposit)]
    [InlineData(ShipmentStatus.PendingDeposit, ShipmentStatus.In_Warehouse)]
    public void ShouldNotifyCustomer_NonCustomerFacingTransition_ReturnsFalse(ShipmentStatus oldStatus, ShipmentStatus newStatus)
    {
        Assert.False(SmsNotificationPolicy.ShouldNotifyCustomer(oldStatus, newStatus));
    }

    #endregion

    #region ShouldNotifyCustomer (string)

    [Fact]
    public void ShouldNotifyCustomer_StringMatched_ReturnsTrue()
    {
        Assert.True(SmsNotificationPolicy.ShouldNotifyCustomer("Draft", "Matched"));
    }

    [Fact]
    public void ShouldNotifyCustomer_StringInTransit_ReturnsTrue()
    {
        Assert.True(SmsNotificationPolicy.ShouldNotifyCustomer("Matched", "In_Transit"));
    }

    [Fact]
    public void ShouldNotifyCustomer_StringInvalidStatus_ReturnsFalse()
    {
        Assert.False(SmsNotificationPolicy.ShouldNotifyCustomer("Invalid", "AlsoInvalid"));
    }

    [Fact]
    public void ShouldNotifyCustomer_StringSameStatus_ReturnsFalse()
    {
        Assert.False(SmsNotificationPolicy.ShouldNotifyCustomer("Matched", "Matched"));
    }

    #endregion

    #region GetMessageType

    [Theory]
    [InlineData(ShipmentStatus.Matched, SmsMessageType.ShipmentMatched)]
    [InlineData(ShipmentStatus.In_Transit, SmsMessageType.ShipmentInTransit)]
    [InlineData(ShipmentStatus.Delivered, SmsMessageType.ShipmentDelivered)]
    [InlineData(ShipmentStatus.Completed, SmsMessageType.ShipmentCompleted)]
    [InlineData(ShipmentStatus.Cancelled, SmsMessageType.ShipmentCancelled)]
    public void GetMessageType_CustomerFacingStatus_ReturnsCorrectType(ShipmentStatus status, SmsMessageType expected)
    {
        Assert.Equal(expected, SmsNotificationPolicy.GetMessageType(status));
    }

    [Theory]
    [InlineData(ShipmentStatus.Draft)]
    [InlineData(ShipmentStatus.PendingReview)]
    [InlineData(ShipmentStatus.PendingDeposit)]
    [InlineData(ShipmentStatus.In_Warehouse)]
    public void GetMessageType_NonCustomerFacingStatus_ReturnsNull(ShipmentStatus status)
    {
        Assert.Null(SmsNotificationPolicy.GetMessageType(status));
    }

    #endregion
}

/// <summary>
/// Tests for <see cref="SmsMessageTemplates"/> — SMS message generation.
/// </summary>
public class SmsMessageTemplatesTests
{
    [Fact]
    public void QuotationAvailable_ContainsShipmentCode()
    {
        var msg = SmsMessageTemplates.QuotationAvailable("SHP-20250818-0001", 500000);
        Assert.Contains("SHP-20250818-0001", msg);
        Assert.Contains("500,000", msg);
    }

    [Fact]
    public void ShipmentMatched_ContainsShipmentCode()
    {
        var msg = SmsMessageTemplates.ShipmentMatched("SHP-20250818-0002");
        Assert.Contains("SHP-20250818-0002", msg);
    }

    [Fact]
    public void ShipmentInTransit_ContainsShipmentCode()
    {
        var msg = SmsMessageTemplates.ShipmentInTransit("SHP-20250818-0003");
        Assert.Contains("SHP-20250818-0003", msg);
    }

    [Fact]
    public void ShipmentDelivered_ContainsShipmentCode()
    {
        var msg = SmsMessageTemplates.ShipmentDelivered("SHP-20250818-0004");
        Assert.Contains("SHP-20250818-0004", msg);
    }

    [Fact]
    public void ShipmentCompleted_ContainsShipmentCode()
    {
        var msg = SmsMessageTemplates.ShipmentCompleted("SHP-20250818-0005");
        Assert.Contains("SHP-20250818-0005", msg);
    }

    [Fact]
    public void ShipmentCancelled_ContainsShipmentCode()
    {
        var msg = SmsMessageTemplates.ShipmentCancelled("SHP-20250818-0006");
        Assert.Contains("SHP-20250818-0006", msg);
    }

    [Fact]
    public void BuildMessage_QuotationAvailable_MatchesDirectCall()
    {
        var msg = SmsMessageTemplates.BuildMessage(SmsMessageType.QuotationAvailable, new SmsMessageParams
        {
            ShipmentCode = "SHP-20250818-0001",
            ShippingFee = 1000000,
            Currency = "VND"
        });
        Assert.Contains("SHP-20250818-0001", msg);
        Assert.Contains("1,000,000", msg);
        Assert.Contains("VND", msg);
    }

    [Fact]
    public void BuildMessage_ShipmentMatched_MatchesDirectCall()
    {
        var msg = SmsMessageTemplates.BuildMessage(SmsMessageType.ShipmentMatched, new SmsMessageParams
        {
            ShipmentCode = "SHP-20250818-0002"
        });
        Assert.Contains("SHP-20250818-0002", msg);
    }

    [Theory]
    [InlineData(SmsMessageType.ShipmentMatched)]
    [InlineData(SmsMessageType.ShipmentInTransit)]
    [InlineData(SmsMessageType.ShipmentDelivered)]
    [InlineData(SmsMessageType.ShipmentCompleted)]
    [InlineData(SmsMessageType.ShipmentCancelled)]
    [InlineData(SmsMessageType.ShipmentPendingDeposit)]
    [InlineData(SmsMessageType.QuotationAvailable)]
    public void BuildMessage_AllTypes_ContainsHmsPrefix(SmsMessageType type)
    {
        var msg = SmsMessageTemplates.BuildMessage(type, new SmsMessageParams
        {
            ShipmentCode = "SHP-TEST",
            ShippingFee = 100000,
            Currency = "VND"
        });
        Assert.StartsWith("HMS:", msg);
    }
}

/// <summary>
/// Tests for <see cref="SmsSendResult"/>.
/// </summary>
public class SmsSendResultTests
{
    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        var result = SmsSendResult.Ok("provider-123");
        Assert.True(result.Success);
        Assert.Equal("provider-123", result.ProviderMessageId);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Fail_CreatesFailedResult()
    {
        var result = SmsSendResult.Fail("TIMEOUT", "Connection timed out");
        Assert.False(result.Success);
        Assert.Equal("TIMEOUT", result.ErrorCode);
        Assert.Equal("Connection timed out", result.ErrorMessage);
    }
}
