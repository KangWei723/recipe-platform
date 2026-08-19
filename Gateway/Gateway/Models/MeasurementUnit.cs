namespace Gateway.Models;

public class MeasurementUnit
{
    public required string Code { get; init; }
    public required string Label { get; init; }
    public required bool IsFractionalFriendly { get; init; }
}
