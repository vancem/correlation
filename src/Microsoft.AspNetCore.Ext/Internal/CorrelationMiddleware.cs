using System.Diagnostics.Activity;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Correlation;

namespace Microsoft.AspNetCore.Ext.Internal
{
    internal class CorrelationMiddleware
    {
        private readonly RequestDelegate next;
        private readonly CorrelationConfigurationOptions.HeaderOptions headerMap;

        public CorrelationMiddleware(RequestDelegate next, CorrelationConfigurationOptions.HeaderOptions headerMap)
        {
            this.next = next;
            this.headerMap = headerMap;
        }

        public async Task Invoke(HttpContext context)
        {
            var activity = new Activity("Incoming request");
            foreach (var header in context.Request.Headers)
            {
                if (header.Key == headerMap.ActivityIdHeaderName)
                {
                    activity.WithParentId(header.Value);
                }
                else
                {
                    var baggageKey = headerMap.GetBaggageKey(header.Key);
                    if (baggageKey != null)
                        activity.WithBaggage(baggageKey, header.Value);
                }
            }

            if (!context.Request.Headers.Keys.Contains(headerMap.CorrelationIdHeaderName))
                activity.WithBaggage(headerMap.GetBaggageKey(headerMap.CorrelationIdHeaderName), activity.Id);

            activity.WithTag("Path", context.Request.Path)
                .WithTag("Method", context.Request.Method)
                .WithTag("RequestId", context.TraceIdentifier)
                .Start(DateTimeStopwatch.GetTime());
            //we start activity here with new Id and parentId from request header (if any)
            //there might be the case when user created his own activity with some baggage in custom middleware
            //so this activity has parent (which will be null in all cases except above one)
            using (activity)
            {
                await next.Invoke(context);
            }
        }
    }
}
