// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;

namespace Microsoft.Extensions.Correlation.Internal
{
    internal class HttpDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly EndpointFilter filter;
        private readonly CorrelationConfigurationOptions.HeaderOptions headerMap;
        private readonly DiagnosticListener listener;
        public HttpDiagnosticListenerObserver(EndpointFilter filter, CorrelationConfigurationOptions.HeaderOptions headerMap,
            DiagnosticListener listener)
        {
            this.filter = filter;
            this.headerMap = headerMap;
            this.listener = listener;
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            //Request and Response events handling id done for 2 reasons
            //1. inject headers
            //2. We need to notify user about outgoing request;  user may want to log outgoing requests because 
            //  - downstream service logs may not be available (it's external dependency or not instumented service)
            //  - user may want to create visualization for operation flow and need parent-child relationship between requests:
            //       e.g. service calls other service multiple times within the same operation (because of retries or business logic)
            //       so activity id should be logged on this service and downstream service to uniquely map one request to another, having the same correlation id
            //  - user is interested in client time difference with server time

            if (value.Key == "System.Net.Http.Request")
            {
                var request = (HttpRequestMessage)value.Value.GetProperty("Request");

                if (request != null)
                {
                    if (filter.Validate(request.RequestUri))
                    {
                        //we start new activity here
                        var activity = new Activity("Http_Out");
                        listener.Start(activity, value.Value);

                        // Attach our ID and Baggage to the outgoing Http Request.
                        request.Headers.Add(headerMap.ActivityIdHeaderName, activity.Id);
                        foreach (var baggage in activity.Baggage)
                            request.Headers.Add(headerMap.GetHeaderName(baggage.Key), baggage.Value);

                        // TODO FIX NOW.
                        // There seems to be a bug in the AsyncLocals where an AsyncLocal set 
                        // in an async method 'leaks' into its caller (which is logically a
                        // separate task.   For now we don't modify the current activity
                        // That is we set it back to the parent agressively.  
                        listener.Stop("Http_Out", value.Value);
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var response = (HttpResponseMessage)value.Value.GetProperty("Response");
                if (response != null)
                {
                    if (filter.Validate(response.RequestMessage.RequestUri))
                    {
                        // TODO FIX NOW 
                        // We want to put the activity back to before the Outgoing http request activity
                        // but we already did this agressively above to work around a bug in the 
                        // async local implementation. 
                        // listener.Stop("Http_Out", value.Value);
                    }
                }
            }
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }

    internal static class PropertyExtensions
    {
        public static object GetProperty(this object _this, string propertyName)
        {
            return _this.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(_this);
        }
    }
}