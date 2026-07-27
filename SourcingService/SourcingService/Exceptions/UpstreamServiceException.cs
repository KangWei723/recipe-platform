namespace SourcingService.Exceptions;

// Thrown when a call to a sourcing provider's API fails outright. Timeouts are
// not exceptions here -- the aggregator races each provider against a
// deadline and turns an expired deadline into a "TimedOut" diagnostic instead
// of letting it bubble up as an error.
public class UpstreamServiceException(string message, Exception? inner = null)
    : Exception(message, inner);
