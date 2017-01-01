// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.Activity;
using System.Net.Http;
using System.Reflection;

namespace Microsoft.Extensions.Correlation.Internal
{
    internal class HttpDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        private readonly EndpointFilter filter;
        private readonly CorrelationConfigurationOptions.HeaderOptions headerMap;
        public HttpDiagnosticListenerObserver(EndpointFilter filter, CorrelationConfigurationOptions.HeaderOptions headerMap)
        {
            this.filter = filter;
            this.headerMap = headerMap;
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
                var request = (HttpRequestMessage) value.Value.GetProperty("Request");
                var timestamp = value.Value.GetProperty("Timestamp"); //long

                if (request != null && timestamp != null)
                {
                    if (filter.Validate(request.RequestUri))
                    {
                        //we start new activity here: it's parent will be Current.Id, it's Id will be generated
                        //new Id will become parent of incoming request on downstream service.
                        //new Id may be logged by user in activity starting/stopping event
                        //We should set Activity.Current to new activity
                        var activity = new Activity("Outgoing request")
                            .WithTag("Uri", request.RequestUri.ToString())
                            .WithTag("Method", request.Method.ToString());
                        activity.Start(DateTimeStopwatch.GetTime((long)timestamp));

                        request.Headers.Add(headerMap.ActivityIdHeaderName, activity.Id);
                        foreach (var baggage in activity.Baggage)
                        {
                            request.Headers.Add(headerMap.GetHeaderName(baggage.Key), baggage.Value);
                        }

                        request.Properties["activity"] = activity;
                        
                        // TODO FIX NOW.
                        // There seems to be a bug in the AsyncLocals where an AsyncLocal set 
                        // in an async method 'leaks' into its caller (which is logically a
                        // separate task.   For now we don't modify the current activity
                        // That is we set it back to the parent agressively.  
                        Activity.SetCurrent(activity.Parent);
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var response = (HttpResponseMessage) value.Value.GetProperty("Response");
                var timestamp = value.Value.GetProperty("TimeStamp"); //long

                if (response != null)
                {
                    if (filter.Validate(response.RequestMessage.RequestUri))
                    {
                        var activity = response.RequestMessage.Properties["activity"] as Activity;
                        // TODO FIX NOW 
                        // We want to put the activity back to before the Outgoing http request activity
                        // but we already did this agressively above to work around a bug in the 
                        // async local implementation. 
                        // Activity.SetCurrent(activity.Parent);    
                        if (activity != null)
                        {
                            activity.WithTag("StatusCode", response.StatusCode.ToString());
                            activity.Stop(DateTimeStopwatch.GetTime((long)timestamp) - activity.StartTimeUtc);
                        }
                    }
                }
            }
        }

        public void OnCompleted(){}

        public void OnError(Exception error){}
    }

    internal static class PropertyExtensions
    {
        public static object GetProperty(this object _this, string propertyName)
        {
            return _this.GetType().GetTypeInfo().GetDeclaredProperty(propertyName)?.GetValue(_this);
        }
    }
}