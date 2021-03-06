﻿using System;
using System.Diagnostics;
using Microsoft.Extensions.Correlation.Internal;

namespace Microsoft.Extensions.Correlation
{
    public class CorrelationHttpInstrumentation
    {
        public static IDisposable Enable(CorrelationConfigurationOptions options)
        {
            if (options.InstrumentOutgoingRequests)
            {
                var observer = new HttpDiagnosticListenerObserver(
                    new EndpointFilter(options.EndpointFilter.Endpoints, options.EndpointFilter.Allow),
                    options.Headers);
                return DiagnosticListener.AllListeners.Subscribe(delegate(DiagnosticListener listener)
                {
                    if (listener.Name == "HttpHandlerDiagnosticListener")
                        listener.Subscribe(observer);
                });
            }
            return new NoopDisposable();
        }

        private class NoopDisposable : IDisposable
        {
            public void Dispose() {}
        }
    }
}