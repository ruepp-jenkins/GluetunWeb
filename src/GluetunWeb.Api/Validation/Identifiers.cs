using System.Text.RegularExpressions;

namespace GluetunWeb.Api.Validation;

/// <summary>
/// Connection identifiers must match ^[a-zA-Z0-9-]+$ (also enforced client-side). The value is
/// reused verbatim as part of Docker container names, so the character set is deliberately strict.
/// </summary>
public static partial class Identifiers
{
    [GeneratedRegex("^[a-zA-Z0-9-]+$")]
    private static partial Regex IdentifierRegex();

    public const int MaxLength = 63;

    public static bool IsValid(string? value)
        => !string.IsNullOrEmpty(value)
           && value.Length <= MaxLength
           && IdentifierRegex().IsMatch(value);

    /// <summary>Returns an error message when invalid, or null when valid.</summary>
    public static string? Validate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Identifier is required.";
        if (value.Length > MaxLength)
            return $"Identifier must be at most {MaxLength} characters.";
        if (!IdentifierRegex().IsMatch(value))
            return "Identifier may only contain letters, digits, and hyphens (a-z, A-Z, 0-9, -).";
        return null;
    }
}
