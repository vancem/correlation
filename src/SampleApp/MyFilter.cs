using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace SampleApp
{
    internal class SamplingMiddleware
    {
        private readonly RequestDelegate next;

        public SamplingMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public Task Invoke(HttpContext context)
        {
            Activity.Current?.WithBaggage("isSampled", bool.TrueString);
            return next.Invoke(context);
        }
    }
}