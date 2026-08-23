namespace RecipeService.Domain;

public record IngredientCategory(string Code, string Label);

// Single source of truth for the ingredient category catalog, mirroring MeasurementUnits:
// recipe-service owns it (via CreateIngredientRequest/UpdateIngredientRequest validation and
// the /api/ingredients/categories endpoint) and every other consumer -- Gateway's GraphQL
// schema, the web-client dropdown -- reads it from there instead of keeping its own copy.
public static class IngredientCategories
{
    public static readonly IReadOnlyList<IngredientCategory> All =
    [
        new("vegetables", "Vegetables"),
        new("fruit", "Fruit"),
        new("meat_poultry", "Meat & Poultry"),
        new("seafood", "Seafood"),
        new("dairy_eggs", "Dairy & Eggs"),
        new("grains_pasta", "Grains & Pasta"),
        new("legumes_beans", "Legumes & Beans"),
        new("herbs", "Herbs"),
        new("spices_seasonings", "Spices & Seasonings"),
        new("baking_flour", "Baking & Flour"),
        new("oils_fats", "Oils & Fats"),
        new("sauces_condiments", "Sauces & Condiments"),
        new("canned_jarred", "Canned & Jarred"),
        new("nuts_seeds", "Nuts & Seeds"),
        new("other", "Other"),
    ];

    private static readonly HashSet<string> ValidCodes =
        All.Select(c => c.Code).ToHashSet(StringComparer.Ordinal);

    public static bool IsValid(string code) => ValidCodes.Contains(code);
}
