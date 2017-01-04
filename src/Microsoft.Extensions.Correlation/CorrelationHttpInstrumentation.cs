using System;
using System.Diagnostics;
using Microsoft.Extensions.Correlation.Internal;

namespace Microsoft.Extensions.Correlation
{
    public class CorrelationHttpInstrumentation
    {
        public static IDisposable Enable()
        {
            return DiagnosticListener.AllListeners.Subscribe(delegate (DiagnosticListener listener)
            {
                if (listener.Name == "HttpHandlerDiagnosticListener")
                {
                    var observer = new HttpDiagnosticListenerObserver(new DiagnosticListener("HttpActivityListener"));
                    listener.Subscribe(observer);
                }
            });
        }
    }
}