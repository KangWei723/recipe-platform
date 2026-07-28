namespace Gateway.Client;

public interface ISubstitutionServiceClient
{
    Task<IReadOnlyList<RankedSubstituteDto>> GetRankedSubstitutesAsync(
        string ingredientName, CancellationToken cancellationToken = default);
}
