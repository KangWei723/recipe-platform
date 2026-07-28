namespace Gateway.Exceptions;

// Thrown when a call to one of the upstream REST services (Recipe, Pantry,
// Substitution, Sourcing) fails, times out, or returns an unexpected body.
// Caught by GraphQL/UpstreamServiceErrorFilter and surfaced as a GraphQL
// error rather than an unhandled exception.
public class UpstreamServiceException(string message, Exception? inner = null)
    : Exception(message, inner);
