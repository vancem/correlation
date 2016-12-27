// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

namespace Microsoft.Extensions.Correlation.Internal
{
    internal class HttpDiagnosticListenerObserver : IObserver<KeyValuePair<string, object>>
    {
        public HttpDiagnosticListenerObserver()
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                var request = (HttpRequestMessage) value.Value.GetProperty("Request");
                if (request == null)
                    return;
                object timestampObj = value.Value.GetProperty("Timestamp");
                Activity.Start("OutgoingHttpRequest");
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var response = (HttpResponseMessage)value.Value.GetProperty("Response");
                if (response == null)
                    return;
                object timestampObj = value.Value.GetProperty("TimeStamp");
                Activity.Stop("OutgoingHttpRequest");
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