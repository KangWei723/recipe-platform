namespace RecipeService.Domain;

// How a unit's quantity should be entered/displayed: imperial spoon/cup/weight
// measures and count-style units are how home cooks naturally think in halves
// and quarters ("2 1/2 tbsp", "1/2 onion"); metric units are decimal by
// convention ("250.5g", never "250 1/2g").
public enum QuantityInputStyle
{
    FractionalFriendly,
    DecimalOnly
}

public record MeasurementUnit(string Code, string Label, QuantityInputStyle Style);

// Single source of truth for the ingredient default-unit catalog: recipe-service owns it
// (via CreateIngredientRequest/UpdateIngredientRequest validation and the /api/ingredients/units
// endpoint) and every other consumer -- Gateway's GraphQL schema, the web-client dropdown and
// its quantity-input behavior -- reads it from there instead of keeping its own copy.
public static class MeasurementUnits
{
    public static readonly IReadOnlyList<MeasurementUnit> All =
    [
        new("tsp", "tsp (teaspoon)", QuantityInputStyle.FractionalFriendly),
        new("tbsp", "tbsp (tablespoon)", QuantityInputStyle.FractionalFriendly),
        new("cup", "cup", QuantityInputStyle.FractionalFriendly),
        new("fl oz", "fl oz (fluid ounce)", QuantityInputStyle.FractionalFriendly),
        new("oz", "oz (ounce)", QuantityInputStyle.FractionalFriendly),
        new("lb", "lb (pound)", QuantityInputStyle.FractionalFriendly),
        new("whole", "whole / count", QuantityInputStyle.FractionalFriendly),
        new("clove", "clove", QuantityInputStyle.FractionalFriendly),
        new("g", "g (gram)", QuantityInputStyle.DecimalOnly),
        new("kg", "kg (kilogram)", QuantityInputStyle.DecimalOnly),
        new("ml", "ml (milliliter)", QuantityInputStyle.DecimalOnly),
        new("l", "l (liter)", QuantityInputStyle.DecimalOnly),
        new("pinch", "pinch", QuantityInputStyle.DecimalOnly),
    ];

    private static readonly HashSet<string> ValidCodes =
        All.Select(u => u.Code).ToHashSet(StringComparer.Ordinal);

    public static bool IsValid(string code) => ValidCodes.Contains(code);
}
