using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace System.Diagnostics
{
    public class Activity : IDisposable
    {
        /// <summary>
        /// An operation name is a COARSEST name that is useful grouping/filtering. 
        /// The name is typically a compile time constant.   Names of Rest APIs are 
        /// reasonable, but arguments (e.g. specific accounts etc), should not be in
        /// the name but rather in the tags.  
        /// </summary>
        public string OperationName { get; private set; }
        /// <summary>
        /// This is an ID that is specific to a particular request.   Filtering
        /// to a particular ID insures that you get only one request that matches.  
        /// It is typically assigned the system itself. 
        /// </summary>
        public string ID { get; private set; }
        /// <summary>
        /// The time that operation started.  Typcially when Start() is called 
        /// (but you can pass a value to Start() if necessary.  
        /// </summary>
        public DateTime StartTime { get; private set; }
        /// <summary>
        /// If the Activity has ended (Stop was called) then this is the delta
        /// between start and end.   If the activity is not ended then this is 
        /// TimeSpan.Zero.  
        /// </summary>
        public TimeSpan Duration { get; private set; }
        /// <summary>
        /// If this activity was created from (caused by) another activity than this is 
        /// the activity that created it.   It can be null if this is a 'root' 
        /// activity that has no logical parent.  
        /// </summary>
        public Activity Parent { get; private set; }

        /// <summary>
        /// Tags are string-string key-value pairs that represent information that will
        /// be logged along with the Activity to the logging system.   This infomration
        /// however is NOT passed on to the children of this activity.  (see Baggage)
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Tags { get { return _tags; } }
        /// <summary>
        /// Tags are string-string key-value pairs that represent information that will
        /// be passed along to children of this activity.   Baggage is serialized 
        /// when requests leave the process (along with the ID).   Typically Baggage is
        /// used to do fine-grained control over logging of the activty and any children.  
        /// In general, if you are not using the data at runtime, you should be using Tags 
        /// instead. 
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Baggage { get { return _baggage; } }

        // TODO do we need the ability to hang properties off the activity?  

        // Moderately Advanced APIs, only need when 
        /// <summary>
        /// The only thing you HAVE to provide to create a activity is the OperationName, however 
        /// the ID, StartTime and ParentID are filled in from context (Current). 
        /// After calling the desired 'With*' methods to update tags and baggage 
        /// and then call 'Start' to start it and 'Stop' to stop it.   It is expected 
        /// that both Start and Stop are called.  
        /// </summary>     
        public Activity(string operationName, string parentID = null)
        {
        }

        /// <summary>
        /// Update the current activity to have a tag with 'key' and value 'value'.
        /// This shows up in the 'tags' eumeration.  
        /// Returns 'this' for convinient chaining
        /// </summary>
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
            // throw new NotImplementedException();
        }

        public static void Stop(string operationName)
        {
            // Ultimately call SetCurrent
        }
        public static void Stop(Activity activity)
        {
            // Ultimately call SetCurrent
            // throw new NotImplementedException();
        }

        public static void SetCurrnet(Activity incommingActivity)
        {
            if (!CurrentEnabled)
                return;
            var changing = CurrentChanging;
            if (changing != null)
                changing(incommingActivity);
            _current.Value = incommingActivity;
        }

        /// <summary>
        /// Returns the current operation (Activity) for the current thread.  This flows 
        /// across async calls.   Can return null if CurrentEnabled is false.  
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


#if ISSUES 

Do Tags/Baggage include HTTP prefixes? 
What things can be null?
How to deal with parents that are off machine (where do we put parentID?

Work through Activity.CurrentEnabled. 
Can Current be null? (yes, if tracking is off)
Work through not using Current.  
Work through Filtering (do we need it?) 
Work through Sampling
Is Activity Complete? 
Work through multi-process scenario. 

#endif
