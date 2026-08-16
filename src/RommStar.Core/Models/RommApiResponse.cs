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
        public string FailureToCSV()
        {
            StringBuilder sb = new StringBuilder($"Failure Reason: [{FailureReason}]. ");
            if (ExceptionMessage != null) sb.Append($"Exception Message: [{ExceptionMessage}].");
            if (HttpResponse != null) sb.Append(Environment.NewLine + $"Http Response: [{HttpResponse.ToString()}]. ");
            return sb.ToString();
        }


        public bool IsSuccess { get; init; }
        public RommApiFailureReason FailureReason { get; init; } = RommApiFailureReason.None;
        public HttpResponseMessage? HttpResponse { get; init; }
        public string? ExceptionMessage { get; init; }

        // Factory helpers to streamline creation inside the service plumbing
        public static RommApiResponse Success(HttpResponseMessage response) =>
            new() { IsSuccess = true, HttpResponse = response };

        public static RommApiResponse Fail(RommApiFailureReason reason, string? exceptionMessage = null, HttpResponseMessage? response = null) =>
                    new() { IsSuccess = false, FailureReason = reason, ExceptionMessage = exceptionMessage, HttpResponse = response };
    }

    // Generic version for typed data responses
    public class RommApiResponse<T> : RommApiResponse where T : class
    {
        public T? Data { get; init; }

        public static RommApiResponse<T> SuccessWithData(HttpResponseMessage response, T data) =>
            new() { IsSuccess = true, HttpResponse = response, Data = data };

        public static RommApiResponse<T> Fail(RommApiFailureReason reason, string? exceptionMessage = null) =>
            new() { IsSuccess = false, FailureReason = reason, ExceptionMessage = exceptionMessage };
    }
}