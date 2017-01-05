using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Correlation;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class CorrelationMiddleware
    {
        private static readonly DiagnosticListener httpListener = new DiagnosticListener("Microsoft.AspNetCore.Http");
        private readonly RequestDelegate next;

        public CorrelationMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (httpListener.IsEnabled("Http_InStart"))
            {
                var activity = new Activity("Http_In");

                // Transfer ID and baggage.  
                foreach (var header in context.Request.Headers)
                {
                    if (header.Key == HttpHeaderConstants.ActivityIdHeaderName) // Check for x-ms-request-id
                        activity.WithParentId(header.Value);
                    else if (header.Key == HttpHeaderConstants.CorrelationIdHeaderName || header.Key.StartsWith(HttpHeaderConstants.BaggageHeaderPrefix))
                        activity.WithBaggage(header.Key, header.Value);
                }

                // Start the activity represending this incomming HTTP request.  
                httpListener.Start(activity, context);
                if (!context.Request.Headers.ContainsKey(HttpHeaderConstants.CorrelationIdHeaderName))
                    activity.WithBaggage(HttpHeaderConstants.CorrelationIdHeaderName, Guid.NewGuid().ToString());

                try
                {
                    await next.Invoke(context);
                }
                finally
                {
                    httpListener.Stop(activity, context);
                }
            }
            else
                await next.Invoke(context);
        }
    }
}
