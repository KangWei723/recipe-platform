using RecipeService.Exceptions;

namespace RecipeService.Domain;

// Application-level guardrails on recipes.tips/pairing -- the columns themselves (TEXT[]/TEXT)
// are unbounded; these exist to stop an admin from pasting in an unreasonably large list/string,
// same spirit as ImageUrlValidator.
public static class RecipeContentLimits
{
    public const int MaxTipCount = 10;
    public const int MaxTipLength = 300;
    public const int MaxPairingLength = 500;

    public static void EnsureValidTips(IReadOnlyList<string> tips)
    {
        if (tips.Count > MaxTipCount)
        {
            throw new ValidationException($"A recipe can have at most {MaxTipCount} tips");
        }

        foreach (var tip in tips)
        {
            if (tip.Length > MaxTipLength)
            {
                throw new ValidationException($"Each tip must be at most {MaxTipLength} characters");
            }
        }
    }

    public static void EnsureValidPairing(string? pairing)
    {
        if (pairing is not null && pairing.Length > MaxPairingLength)
        {
            throw new ValidationException($"Pairing note must be at most {MaxPairingLength} characters");
        }
    }
}
