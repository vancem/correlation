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
        public HttpDiagnosticListenerObserver(DiagnosticListener listener)
        {
            this.listener = listener;
            requestFetcher = new PropertyFetcher("Request");
            requestTimestampFetcher = new PropertyFetcher("Timestamp");
            responseFetcher = new PropertyFetcher("Response");
            responseTimestampFetcher = new PropertyFetcher("TimeStamp");
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                var request = (HttpRequestMessage)requestFetcher.Fetch(value.Value);
                var timestamp = (long)requestTimestampFetcher.Fetch(value.Value);

                if (request != null)
                {
                    if (listener.IsEnabled(request.RequestUri.ToString()))
                    {
                        //we start new activity here
                        var activity = new Activity("Http_Out")
                            .WithStartTime(DateTimeStopwatch.GetTime(timestamp));
                        listener.Start(activity, value.Value);

                        // Attach our ID and Baggage to the outgoing Http Request.
                        request.Headers.Add(HttpHeaderConstants.ActivityIdHeaderName, activity.Id);
                        foreach (var baggage in activity.Baggage)
                        {
                            request.Headers.Add(baggage.Key, baggage.Value);
                        }
                        // TODO FIX NOW.
                        // There seems to be a bug in the AsyncLocals where an AsyncLocal set 
                        // in an async method 'leaks' into its caller (which is logically a
                        // separate task.   For now we don't modify the current activity
                        // That is we set it back to the parent agressively.  
                        listener.Stop(activity, value.Value, DateTimeStopwatch.GetTime(timestamp));
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var response = (HttpResponseMessage)responseFetcher.Fetch(value.Value);
                var timestamp = (long)responseTimestampFetcher.Fetch(value.Value);
                if (response != null)
                {
                    if (listener.IsEnabled(response.RequestMessage.RequestUri.ToString()))
                    {
                        // TODO FIX NOW 
                        // We want to put the activity back to before the Outgoing http request activity
                        // but we already did this agressively above to work around a bug in the 
                        // async local implementation. 
                        // listener.Stop(value.Value, DateTimeStopwatch.GetTime(timestamp));
                    }
                }
            }
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }

        #region private
       
        private readonly DiagnosticListener listener;
        private readonly PropertyFetcher requestFetcher;
        private readonly PropertyFetcher responseFetcher;
        private readonly PropertyFetcher requestTimestampFetcher;
        private readonly PropertyFetcher responseTimestampFetcher;

        private class PropertyFetcher
        {
            public PropertyFetcher(string propertyName)
            {
                this.propertyName = propertyName;
            }

            public object Fetch(object obj)
            {
                if (innerFetcher == null)
                {
                    innerFetcher = PropertyFetch.FetcherForProperty(obj.GetType().GetTypeInfo().GetDeclaredProperty(propertyName));
                }

                return innerFetcher?.Fetch(obj);
            }

            #region private

            private PropertyFetch innerFetcher;
            private readonly string propertyName;
            
            //see https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/System/Diagnostics/DiagnosticSourceEventSource.cs
            class PropertyFetch
            {
                /// <summary>
                /// Create a property fetcher from a .NET Reflection PropertyInfo class that
                /// represents a property of a particular type.  
                /// </summary>
                public static PropertyFetch FetcherForProperty(PropertyInfo propertyInfo)
                {
                    if (propertyInfo == null)
                        return new PropertyFetch(); // returns null on any fetch.

                    var typedPropertyFetcher = typeof(TypedFetchProperty<,>);
                    var instantiatedTypedPropertyFetcher = typedPropertyFetcher.GetTypeInfo().MakeGenericType(
                        propertyInfo.DeclaringType, propertyInfo.PropertyType);
                    return (PropertyFetch) Activator.CreateInstance(instantiatedTypedPropertyFetcher, propertyInfo);
                }

                /// <summary>
                /// Given an object, fetch the property that this propertyFech represents. 
                /// </summary>
                public virtual object Fetch(object obj)
                {
                    return null;
                }

                #region private 

                private class TypedFetchProperty<TObject, TProperty> : PropertyFetch
                {
                    public TypedFetchProperty(PropertyInfo property)
                    {
                        _propertyFetch =
                            (Func<TObject, TProperty>)
                            property.GetMethod.CreateDelegate(typeof(Func<TObject, TProperty>));
                    }

                    public override object Fetch(object obj)
                    {
                        return _propertyFetch((TObject) obj);
                    }

                    private readonly Func<TObject, TProperty> _propertyFetch;
                }

                #endregion
            }

            #endregion
        }
        #endregion
    }
}