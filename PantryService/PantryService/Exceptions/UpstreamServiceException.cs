namespace PantryService.Exceptions;

// Thrown when a call to another service (currently just recipe-service) fails
// or times out. Mapped to 502 Bad Gateway so callers can tell "your request
// was bad" apart from "a downstream dependency broke".
public class UpstreamServiceException(string message, Exception? inner = null)
    : Exception(message, inner);
