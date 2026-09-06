using System.Text.RegularExpressions;

namespace HMS.Shared.Core.Sms;

/// <summary>
/// Utility for normalizing Vietnamese phone numbers to international format.
/// Handles common formats used by Vietnamese customers.
/// </summary>
public static partial class PhoneHelper
{
    // Matches digits only (with optional leading +)
    [GeneratedRegex(@"^\+?(\d{9,15})$")]
    private static partial Regex DigitsOnly();

    /// <summary>
    /// Normalizes a phone number to E.164 format (+84...).
    /// Returns null if the input is null, empty, or invalid.
    /// </summary>
    /// <example>
    /// 0912345678 → +84912345678
    /// 84912345678 → +84912345678
    /// +84912345678 → +84912345678
    /// </example>
    public static string? Normalize(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        // Strip all non-digit characters except leading +
        var cleaned = phoneNumber.Trim();
        var hasPlus = cleaned.StartsWith('+');
        var digitsOnly = new string(cleaned.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length < 9 || digitsOnly.Length > 15)
            return null;

        // Already in international format with +84
        if (hasPlus && digitsOnly.StartsWith("84") && digitsOnly.Length >= 11)
            return $"+{digitsOnly}";

        // Starts with 84 (without +)
        if (digitsOnly.StartsWith("84") && digitsOnly.Length >= 11)
            return $"+{digitsOnly}";

        // Vietnamese local format: 0xxxxxxxxx (10 digits starting with 0)
        if (digitsOnly.StartsWith("0") && digitsOnly.Length == 10)
            return $"+84{digitsOnly[1..]}";

        // 9-digit number without leading 0 (rare but possible)
        if (digitsOnly.Length == 9 && !hasPlus)
            return null; // Cannot reliably determine country code

        // If it already has + and reasonable length, accept as-is
        if (hasPlus && digitsOnly.Length >= 9)
            return $"+{digitsOnly}";

        return null;
    }

    /// <summary>
    /// Validates that a phone number can be normalized.
    /// </summary>
    public static bool IsValid(string? phoneNumber)
    {
        return Normalize(phoneNumber) != null;
    }

    /// <summary>
    /// Masks a phone number for logging (PII protection).
    /// Example: +84912345678 → +84****5678
    /// </summary>
    public static string Mask(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return "(null)";

        var normalized = Normalize(phoneNumber);
        if (normalized == null || normalized.Length < 8)
            return "****";

        var visibleStart = normalized[..4];
        var visibleEnd = normalized[^4..];
        var masked = new string('*', normalized.Length - 8);
        return $"{visibleStart}{masked}{visibleEnd}";
    }
}
