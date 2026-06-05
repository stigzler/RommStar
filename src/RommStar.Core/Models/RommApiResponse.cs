using RommStar.Core.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RommStar.Core.Models
{
    public class RommApiResponse
    {
        public bool IsSuccess { get; init; }
        public RommApiFailureReason FailureReason { get; init; } = RommApiFailureReason.None;
        public HttpResponseMessage? HttpResponse { get; init; }
        public string? ExceptionMessage { get; init; }

        // Factory helpers to streamline creation inside the service plumbing
        public static RommApiResponse Success(HttpResponseMessage response) =>
            new() { IsSuccess = true, HttpResponse = response };

        public static RommApiResponse Fail(RommApiFailureReason reason, string? exceptionMessage = null) =>
            new() { IsSuccess = false, FailureReason = reason, ExceptionMessage = exceptionMessage };
    }
}