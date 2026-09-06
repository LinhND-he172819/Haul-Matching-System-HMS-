using HMS.Shared.Core.Sms;

namespace HMS.Shared.Core.Tests;

/// <summary>
/// Tests for <see cref="PhoneHelper"/> — Vietnamese phone number normalization, validation, and masking.
/// </summary>
public class SmsPhoneHelperTests
{
    #region Normalize

    [Theory]
    [InlineData("0912345678", "+84912345678")]
    [InlineData("0987654321", "+84987654321")]
    [InlineData("0345678901", "+84345678901")]
    public void Normalize_10DigitLocalNumber_AddsCountryCode(string input, string expected)
    {
        Assert.Equal(expected, PhoneHelper.Normalize(input));
    }

    [Theory]
    [InlineData("84912345678", "+84912345678")]
    [InlineData("84987654321", "+84987654321")]
    public void Normalize_WithCountryCodePrefix84_AddsPlus(string input, string expected)
    {
        Assert.Equal(expected, PhoneHelper.Normalize(input));
    }

    [Theory]
    [InlineData("+84912345678", "+84912345678")]
    [InlineData("+84987654321", "+84987654321")]
    public void Normalize_AlreadyInternational_ReturnsSame(string input, string expected)
    {
        Assert.Equal(expected, PhoneHelper.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NullOrEmpty_ReturnsNull(string? input)
    {
        Assert.Null(PhoneHelper.Normalize(input));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("abcdefghij")]
    public void Normalize_TooShort_ReturnsNull(string input)
    {
        Assert.Null(PhoneHelper.Normalize(input));
    }

    #endregion

    #region IsValid

    [Fact]
    public void IsValid_NormalizedPhone_ReturnsTrue()
    {
        Assert.True(PhoneHelper.IsValid("+84912345678"));
    }

    [Fact]
    public void IsValid_LocalFormat_ReturnsTrue()
    {
        Assert.True(PhoneHelper.IsValid("0912345678"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    public void IsValid_InvalidPhone_ReturnsFalse(string? input)
    {
        Assert.False(PhoneHelper.IsValid(input));
    }

    #endregion

    #region Mask

    [Theory]
    [InlineData("+84912345678", "+849****5678")]
    [InlineData("+84987654321", "+849****4321")]
    public void Mask_NormalPhone_ShowsFirstFourAndLastFour(string input, string expected)
    {
        Assert.Equal(expected, PhoneHelper.Mask(input));
    }

    [Theory]
    [InlineData(null, "(null)")]
    [InlineData("", "(null)")]
    public void Mask_NullOrEmpty_ReturnsNullPlaceholder(string? input, string expected)
    {
        Assert.Equal(expected, PhoneHelper.Mask(input));
    }

    [Fact]
    public void Mask_ShortInvalidPhone_ReturnsStars()
    {
        // Short/unnormalizable phone gets masked to ****
        var result = PhoneHelper.Mask("+84912");
        Assert.Equal("****", result);
    }

    #endregion
}
