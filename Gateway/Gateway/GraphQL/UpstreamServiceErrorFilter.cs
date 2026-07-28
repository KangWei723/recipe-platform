using Gateway.Exceptions;
using HotChocolate.Execution;

namespace Gateway.GraphQL;

// Mirrors the upstream services' own error mapping (UpstreamServiceException
// -> 502) as a typed GraphQL error instead of a generic unhandled-exception
// message.
public class UpstreamServiceErrorFilter : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is UpstreamServiceException upstreamException)
        {
            return error
                .WithMessage(upstreamException.Message)
                .WithCode("UPSTREAM_SERVICE_ERROR");
        }

        return error;
    }
}
