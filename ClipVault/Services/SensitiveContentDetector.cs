using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ClipVault.Services;

public readonly record struct SensitiveDetectionResult(bool IsSensitive, string Reason);

public sealed class SensitiveContentDetector
{
    private static readonly Regex JwtRegex = new(
        @"^[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_=+/]*$",
        RegexOptions.Compiled);

    private static readonly Regex KnownSecretPrefixRegex = new(
        @"^(?:ghp_|gho_|ghu_|ghs_|github_pat_|glpat-|sk_live_|sk_test_|rk_live_|rk_test_|pk_live_|pk_test_|SG\.)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex HexTokenRegex = new(
        @"^[A-Fa-f0-9]{32,}$",
        RegexOptions.Compiled);

    private static readonly Regex Base64LikeTokenRegex = new(
        @"^[A-Za-z0-9+/=_-]{24,}$",
        RegexOptions.Compiled);

    public SensitiveDetectionResult Evaluate(string? rawText)
    {
        string text = Normalize(rawText);

        if (string.IsNullOrWhiteSpace(text) || text.Length < 6)
            return default;

        if (LooksLikeCredentialAssignment(text))
            return new SensitiveDetectionResult(true, "Credential assignment");

        if (LooksLikeBearerToken(text))
            return new SensitiveDetectionResult(true, "Bearer token");

        if (JwtRegex.IsMatch(text))
            return new SensitiveDetectionResult(true, "JWT");

        if (KnownSecretPrefixRegex.IsMatch(text))
            return new SensitiveDetectionResult(true, "Known secret prefix");

        if (LooksLikeHighEntropySecret(text))
            return new SensitiveDetectionResult(true, "High-entropy token");

        return default;
    }

    private static bool LooksLikeCredentialAssignment(string text)
    {
        string lowered = text.ToLowerInvariant();

        return (lowered.Contains("password=") ||
                lowered.Contains("pwd=") ||
                lowered.Contains("passphrase=") ||
                lowered.Contains("secret=") ||
                lowered.Contains("token=") ||
                lowered.Contains("api_key=") ||
                lowered.Contains("apikey="))
               && text.Any(char.IsLetterOrDigit);
    }

    private static bool LooksLikeBearerToken(string text)
    {
        if (!text.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        string token = text[7..].Trim();
        return token.Length >= 16 && !token.Contains(' ');
    }

    private static bool LooksLikeHighEntropySecret(string text)
    {
        if (Uri.TryCreate(text, UriKind.Absolute, out _))
            return false;

        if (text.Contains(' ') || text.Contains('\n') || text.Contains('\t'))
            return false;

        if (HexTokenRegex.IsMatch(text))
            return true;

        int characterClassCount = 0;
        if (text.Any(char.IsLower)) characterClassCount++;
        if (text.Any(char.IsUpper)) characterClassCount++;
        if (text.Any(char.IsDigit)) characterClassCount++;
        if (text.Any(ch => !char.IsLetterOrDigit(ch))) characterClassCount++;

        if (text.Length >= 12 && characterClassCount >= 3 && text.Any(ch => !char.IsLetterOrDigit(ch)))
            return true;

        if (text.Length >= 24 && Base64LikeTokenRegex.IsMatch(text))
            return true;

        return false;
    }

    private static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return text.Replace("\r\n", "\n").Trim();
    }
}
