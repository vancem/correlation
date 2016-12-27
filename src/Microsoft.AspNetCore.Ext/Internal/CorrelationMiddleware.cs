using System;

using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Correlation;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class CorrelationMiddleware
    {
        static DiagnosticListener IncommingDiagnosticsSource = new DiagnosticListener("Microsoft.ASpNetCore.MiddleWare");
        private static string ActivityIdHeaderName = "x-ms-request-id";
        private static string BaggageHeaderName = "x-ms-hasvals";
        private static string BaggageHeaderPrefix = "x-ms-v-";

        private readonly RequestDelegate next;

        public CorrelationMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var request = context.Request;

            IHeaderDictionary headers = request.Headers;
            string parentID = headers[ActivityIdHeaderName];

            Activity incommingActivity = new Activity("IncommingHttpRequest", parentID);
            if (headers.ContainsKey(BaggageHeaderName))
            {
                foreach (var keyValue in headers)
                {
                    if (keyValue.Key.StartsWith(BaggageHeaderPrefix))
                        incommingActivity.WithBaggage(keyValue.Key, keyValue.Value[0]);
                }
            }

            // Make the current activity the one that is associated with this HTTP request.  
            // This is NOT a start because logically the parent of this is NOT Activity.Current
            // but the code that created the HTTP request.  
            Activity.SetCurrnet(incommingActivity);

            if (IncommingDiagnosticsSource.IsEnabled("IncommingHttpStart"))
            {
                IncommingDiagnosticsSource.Write("IncommingHttpStart",
                    new
                    {
                        Request = request,
                        Path = request.Path,
                        Method = request.Method,
                        RequestId = context.TraceIdentifier
                    });
            }
            await next.Invoke(context);

            if (IncommingDiagnosticsSource.IsEnabled("IncommingHttpStop"))
            {
                IncommingDiagnosticsSource.Write("IncommingHttpStop",
                    new
                    {
                        Request = request,
                        Path = request.Path,
                        Method = request.Method,
                        RequestId = context.TraceIdentifier
                    });
            }
            Activity.Stop(incommingActivity);

        }
    }
}
