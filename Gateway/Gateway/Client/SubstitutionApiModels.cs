namespace Gateway.Client;

// Minimal client-side mirrors of substitution-service's JSON response
// contracts. See RecipeApiModels.cs for why these are duplicated rather
// than shared.

public record RankedSubstituteDto(
    string SubstituteName,
    double Ratio,
    IReadOnlyList<string> Contexts,
    double Confidence
);
