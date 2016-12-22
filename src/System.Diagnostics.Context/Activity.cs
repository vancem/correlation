using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System.Diagnostics
{
    public class Activity : IDisposable
    {
        public string OperationName { get; private set; }
        public string ID { get; private set; }
        public DateTime StartTime { get; private set; }
        public TimeSpan Duration { get; private set; }
        public Activity Parent { get; private set; }
        public IEnumerable<KeyValuePair<string, string>> Tags { get { return _tags; } }
        public IEnumerable<KeyValuePair<string, string>> Baggage { get { return _baggage; } }

        // Moderately Advanced APIs, only need when 
        /// <summary>
        /// The only thing you HAVE to provide to create a activity is the OperationName, however 
        /// the ID, StartTime and ParentID are filled in from context (Current). 
        /// Normally 
        /// </summary>     
        public Activity(string operationName, string parentID = null)
        {
        }

        public Activity WithTag(string key, string value)
        {
            _tags.AddFirst(new KeyValuePair<string, string>(key, value));
            return this;
        }
        public Activity WithBaggage(string key, string value)
        {
            _baggage.AddFirst(new KeyValuePair<string, string>(key, value));
            return this;
        }

        public void Start(DateTime startTime = default(DateTime))
        {
            // TODO
        }
        public void Stop(TimeSpan duration = default(TimeSpan))
        {
            Duration = duration;
            // TODO
        }

        // Advanced APIs
        /// <summary>
        /// Generates a int that starting at 0 that is incremented each time this routine
        /// is called, thus generating a value unique in the context of this activity.   Useful
        /// for creating child IDs.   
        /// </summary>
        /// <returns></returns>
        public int GenerateChildID()
        {
            // TODO Interlocked?
            int ret = _nextChildID++;
            return ret;
        }
        public string GetBaggageItem(string key)
        {
            foreach (var keyValue in _baggage)
                if (key == keyValue.Key)
                    return keyValue.Value;
            return null;
        }
        public override string ToString()
        {
            return $"operation: {OperationName}, id: {ID}, StartTime {StartTime:o}";
        }

        // Static Functions 
        public static Activity Start(string operationName)
        {
            throw new NotImplementedException();
        }
        public static void Start(Activity activity)
        {
            throw new NotImplementedException();
        }

        public static void Stop(string operationName)
        {
            // Ultimately call SetCurrent
        }
        public static void Stop(Activity activity)
        {
            // Ultimately call SetCurrent
            throw new NotImplementedException();
        }

        public static void SetCurrnet(Activity incommingActivity)
        {
            var changing = CurrentChanging;
            if (changing != null)
                changing(incommingActivity);
            _current.Value = incommingActivity;
        }

        /// <summary>
        /// Returns the current operation (Activity) for the current thread.  This flows 
        /// across async calls.   Can return null if CurrnetActivity not enabled.  
        /// </summary>
        public static Activity Current
        {
            get { return _current.Value; }
        }

        /// <summary>
        /// Because keeping 'Current' up to date uses resources, it is off by default and
        /// you have to explicitly turn it on by setting this variable.  
        /// </summary>
        public static bool CurrentEnabled { get; set; }

        /// TODO REMOVE?  we probably don't need this.   
        /// <summary>
        /// Called when Current is changing.  Is passed the NEW value.  Current has NOT been updated
        /// yet so you can observe its value if desired.  
        /// </summary>
        public static event Action<Activity> CurrentChanging;

        #region private 

        public void Dispose()
        {
            Stop();
        }

        LinkedList<KeyValuePair<string, string>> _tags;
        LinkedList<KeyValuePair<string, string>> _baggage;
        int _nextChildID;

        private static readonly AsyncLocal<Activity> _current = new AsyncLocal<Activity>();


        #endregion
    }
}


// TODO Remove this.  
// This represents code that needs to be modified elsewhere 
#if true 
namespace Other
{
    using System;
    using System.Net.Http;
    using System.Diagnostics;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Primitives;

    static class OtherCode
    {
        //**************************************************************************
        // Things to put into DiagnosticSource 
        static void Start(this DiagnosticSource self, string activityName, object args)
        {
            Activity.Start(activityName);
            self.Write(activityName, args);
        }
        static void Stop(this DiagnosticSource self, string activityName, object args)
        {
            Activity.Stop(activityName);
            self.Write(activityName + "Stop", args);
        }

        //**************************************************************************
        // Things to put into stopwatch
        static DateTime _stopwatchStartTime;        // TODO must iniialize.  

        static DateTime FromStopwatchTicks(long ticks)
        {
            return _stopwatchStartTime.AddSeconds(ticks + Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        }

        static DateTime StopwatchNow()
        {
            return FromStopwatchTicks(Stopwatch.GetTimestamp());
        }

        // Things to put into System.Net.Http 
        public static string ActivityIdHeaderName = "x-ms-request-id";
        public static string BaggageHeaderName = "x-ms-hasvals";
        public static string BaggageHeaderPrefix = "x-ms-v-";

        // What happens on incomming and outgoing requests  
        static void OnHttpOutgoingSend(HttpRequestMessage request)
        {
            // TODO Filtering                if (filter.Validate(request.RequestUri))
            if (!Activity.CurrentEnabled)
                return;

            // TODO NOW how to filter out uninteresting targets 
            var curActivity = Activity.Current;
            if (curActivity != null)
            {
                // Send the ID
                request.Headers.Add(ActivityIdHeaderName, curActivity.ID);
                // Send the Baggage  
                if (curActivity.Baggage != null)
                {
                    foreach (var keyValue in curActivity.Baggage)
                        request.Headers.Add(keyValue.Key, keyValue.Value);
                }
            }
        }

        // Thngs to put into ASP.NET 
        // This goes into an incomming request (Folds into CorrelationMiddleware
        static void OnHttpIncommingReceive(HttpRequest request)
        {
            IHeaderDictionary headers = request.Headers;
            StringValues idStringValues = headers[ActivityIdHeaderName];
            if (idStringValues.Count == 0)
                return;

            string parentID = idStringValues[0];

            Activity incommingActivity = new Activity("IncommingHttpRequest", parentID);
            if (headers.ContainsKey(BaggageHeaderName))
            {
                foreach (var keyValue in headers)
                {
                    if (keyValue.Key.StartsWith(BaggageHeaderPrefix))
                        incommingActivity.WithBaggage(keyValue.Key, keyValue.Value[0]);
                }
            }

            // Make the current activity the one that is associated with this HTTP request.  
            // This is NOT a start because logically the parent of this is NOT Activity.Current
            // but the code that created the HTTP request.  
            Activity.SetCurrnet(incommingActivity);
        }
    }
}
#endif