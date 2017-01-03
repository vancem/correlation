using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Correlation;
using System.Diagnostics;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class CorrelationMiddleware
    {
        private static DiagnosticListener httpListener = new DiagnosticListener("Microsoft.AspNetCore.Http");
        private readonly RequestDelegate next;
        private readonly CorrelationConfigurationOptions.HeaderOptions headerMap;

        public CorrelationMiddleware(RequestDelegate next, CorrelationConfigurationOptions.HeaderOptions headerMap)
        {
            this.next = next;
            this.headerMap = headerMap;
        }

        public async Task Invoke(HttpContext context)
        {
            if (httpListener.IsEnabled("Http_InStart"))
            {
                var activity = new Activity("Http_In");

                // Transfer ID and baggage.  
                foreach (var header in context.Request.Headers)
                {
                    if (header.Key == headerMap.ActivityIdHeaderName)           // Check for x-ms-request-id
                        activity.WithParentId(header.Value);
                    // TODO FIX NOW - Consider removing the mapping (GetBaggageKey) call.  
                    else if (header.Key == headerMap.CorrelationIdHeaderName)   // Check for x-ms-correlation
                        activity.WithBaggage(headerMap.GetBaggageKey(headerMap.CorrelationIdHeaderName), activity.Id);
                    else                                                        // Transfer any other x-baggage-* 
                    {
                        var baggageKey = headerMap.GetBaggageKey(header.Key);
                        if (baggageKey != null)
                            activity.WithBaggage(baggageKey, header.Value);
                    }
                }

                // Start the activity represending this incomming HTTP request.  
                Activity.Start(activity);   // The WithParentId call above will make the request the parent if it was present. 
                httpListener.Write("Http_InStart", context);
                await next.Invoke(context);  // TODO EXCEPTIONS
                httpListener.Write("Http_InStop", context);
                Activity.Stop(activity);
            }
            else
                await next.Invoke(context);
        }
    }
}
