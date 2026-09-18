namespace CatAdoption.Api.Models;

public static class CatStatuses
{
    public const string WaitingAdoption = "waiting-adoption";
    public const string InProgress = "in-progress";
    public const string Adopted = "adopted";

    public static readonly string[] All =
    [
        WaitingAdoption,
        InProgress,
        Adopted
    ];

    public static string Normalize(string? status)
    {
        if (TryNormalize(status, out var normalized))
        {
            return normalized;
        }

        return WaitingAdoption;
    }

    public static bool TryNormalize(string? status, out string normalized)
    {
        normalized = WaitingAdoption;

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        normalized = status.Trim().ToLowerInvariant() switch
        {
            Adopted => Adopted,
            InProgress or "in progress" or "in process of adoption" => InProgress,
            WaitingAdoption or "waiting adoption" or "available" => WaitingAdoption,
            _ => string.Empty
        };

        return normalized.Length > 0;
    }

    public static bool IsAllowed(string? status)
    {
        return TryNormalize(status, out var normalized) && Array.IndexOf(All, normalized) >= 0;
    }
}


