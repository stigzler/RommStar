using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Primitives
{
    public enum RommApiFailureReason
    {
        None,
        InvalidConfiguration,  // Missing URL or Token
        Timeout,               // 5-second safety limit hit
        ServerNotFound,        // DNS failure, invalid IP, or host completely offline
        Unauthorized,          // HTTP 401: Invalid API Key/Token
        Forbidden,             // HTTP 403: Token lacks permission
        EndpointNotFound,      // HTTP 404: Base URL or path is incorrect
        UnknownServerError,    // HTTP 500 range errors
        UnexpectedError        // Catch-all structural exceptions
    }
}