﻿using System.Collections.Generic;
using System.Diagnostics.Context;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SampleApp
{
    public class MyFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            //add custom tage
            Span.Current.AddTag("userid", "set user id");

            //add custom context
            bool sampled = isSampled(context.HttpContext.Request);
            Span.Current.SetBaggageItem("isSampled", sampled.ToString());
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
        }

        //this is an example of custom context
        private bool isSampled(HttpRequest request)
        {
            //decide if request should be sampled or not
            return true;
        }
    }
}
