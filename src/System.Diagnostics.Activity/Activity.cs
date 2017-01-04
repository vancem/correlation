using System.Collections.Generic;
using System.Threading;

namespace System.Diagnostics
{
    // TODO: Consider renaming to DiagnosticActivity 
    public class Activity
    {
        /// <summary>
        /// An operation name is a COARSEST name that is useful grouping/filtering. 
        /// The name is typically a compile time constant.   Names of Rest APIs are 
        /// reasonable, but arguments (e.g. specific accounts etc), should not be in
        /// the name but rather in the tags.  
        /// </summary>
        public string OperationName { get; }

        /// <summary>
        /// This is an ID that is specific to a particular request.   Filtering
        /// to a particular ID insures that you get only one request that matches.  
        /// It is typically assigned the system itself. 
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// The time that operation started.  Typcially when Start() is called 
        /// (but you can pass a value to Start() if necessary.  This use UTC (Greenwitch Mean Time)
        /// </summary>
        public DateTime StartTimeUtc { get; private set; }

        /// <summary>
        /// If the Activity has ended (Stop was called) then this is the delta
        /// between start and end.   If the activity is not ended then this is 
        /// TimeSpan.Zero.  
        /// </summary>
        public TimeSpan Duration { get; private set; }

        /// <summary>
        /// If the Activity that created this activity is  from the same process you can get 
        /// that Activit with Parent.   However this can be null if the Activity has no
        /// parent (a root activity) or if the Parent is from outside the process.  (see ParentId for more)
        /// </summary>
        public Activity Parent { get; private set; }

        /// <summary>
        /// If the parent for this activity comes from outside the process, the activity
        /// does not have a Parent Activity but MAY have a ParentId (which was serialized from
        /// from the parent) .   This accessor fetches the parent ID if it exists at all.  
        /// Note this can be null if this is a root Activity (it has no parent)
        /// </summary>
        public string ParentId { get; private set; }

        /// <summary>
        /// Tags are string-string key-value pairs that represent information that will
        /// be logged along with the Activity to the logging system.   This infomration
        /// however is NOT passed on to the children of this activity.  (see Baggage)
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Tags
        {
            get
            {
                if (ParentId != null)
                    yield return new KeyValuePair<string, string>("ParentId", ParentId);
                for (var tags = _tags; tags != null; tags = tags.Next)
                    yield return tags.keyValue;
            }
        }

        /// <summary>
        /// Tags are string-string key-value pairs that represent information that will
        /// be passed along to children of this activity.   Baggage is serialized 
        /// when requests leave the process (along with the ID).   Typically Baggage is
        /// used to do fine-grained control over logging of the activty and any children.  
        /// In general, if you are not using the data at runtime, you should be using Tags 
        /// instead. 
        /// </summary> 
        public IEnumerable<KeyValuePair<string, string>> Baggage
        {
            get
            {
                for (var activity = this; activity != null; activity = activity.Parent)
                    for (var baggage = activity._baggage; baggage != null; baggage = baggage.Next)
                        yield return baggage.keyValue;
            }
        }

        /// <summary>
        /// Returns the value of the key-value pair added to the activity with 'WithBaggage'.
        /// Returns null if that key does does not exist.  
        /// </summary>
        public string GetBaggageItem(string key)
        {
            foreach (var keyValue in Baggage)
                if (key == keyValue.Key)
                    return keyValue.Value;
            return null;
        }

        /* Constructors  Builder methods */

        /// <summary>
        /// Note that Activity has a 'builder' pattern, where you call the constructor, a number of 'With*' APIs and then
        /// call 'Activity.Start' to build the activity.   You MUST call Start before using it 
        /// </summary>
        /// <param name="operationName"></param>
        public Activity(string operationName)
        {
            OperationName = operationName;
        }

        /// <summary>
        /// Update the Activity to have a tag with an additional 'key' and value 'value'.
        /// This shows up in the 'Tags' eumeration.   It is meant for information that
        /// is useful to log but not needed for runtime control (for the latter, use Baggage)
        /// Returns 'this' for convinient chaining.
        /// </summary>
        public Activity WithTag(string key, string value)
        {
            // TODO what to do about duplicates?
            _tags = new KeyValueListNode() { keyValue = new KeyValuePair<string, string>(key, value), Next = _tags };
            return this;
        }

        /// <summary>
        /// Update the Activity to have baggage with an additional 'key' and value 'value'.
        /// This shows up in the 'Baggage' eumeration as well as the 'GetBaggageItem' API.
        /// Baggage is mean for information that is needed for runtime control.   For information 
        /// that is simply useful to show up in the log with the activity use Tags.   
        /// Returns 'this' for convinient chaining.
        /// </summary>
        public Activity WithBaggage(string key, string value)
        {
            _baggage = new KeyValueListNode() { keyValue = new KeyValuePair<string, string>(key, value), Next = _baggage };
            return this;
        }

        /// <summary>
        /// Updates the Activity To indicate that the activity with ID 'parentID' 
        /// caused this activity.   This is only intended to be used at 'boundary' 
        /// scenarios where an activity from another process loggically started 
        /// this activity. The Parent ID shows up the Tags (as well as the ParentID 
        /// property), and can be used to reconstruct the causal tree.  
        /// Returns 'this' for convinient chaining.
        /// </summary>
        public Activity WithParentId(string parentId)
        {
            ParentId = parentId;
            return this;
        }

        /// <summary>
        /// Update the Activity to set start time in UTC (Greenwitch Mean Time)
        /// </summary>
        /// <param name="startTimeUtc"></param>
        /// <returns></returns>
        public Activity WithStartTime(DateTime startTimeUtc)
        {
            StartTimeUtc = startTimeUtc;
            return this;
        }

        /* Static Methods */
        /* Currently we have layering going on.   Activity methods do NOT depend on statics (in particular Activity.Current)
         * so any method that manipulates that variable is a static where we pass in the activity.   
         * If this becomes unintuitive, we shoudl reconsider, but I want to see how it goes as I think it will work out
         * as MOST callers should be calling DiagnosticSource.Start() and DiagnosticSource.Stop() */
        public static Activity Start(Activity activity)
        {
            if (activity.Id != null)
                throw new InvalidOperationException("Trying to start an Activity that was already started");

            if (activity.ParentId == null)
            {
                var parent = Current;
                if (parent != null)
                {
                    activity.ParentId = parent.Id;
                    activity.Parent = parent;
                }
            }

            if (activity.StartTimeUtc == default(DateTime))
                activity.StartTimeUtc = DateTime.UtcNow;

            activity.Id = activity.GenerateId();

            _current.Value = activity;
            return activity;
        }

        public static void Stop(Activity activity, DateTime stopTime = default(DateTime))
        {
            if (activity.Id == null)
                throw new InvalidOperationException("Trying to stop an Activity that was not started");

            if (!activity.isFinished)
            {
                activity.isFinished = true;
                if (stopTime == default(DateTime))
                    stopTime = DateTime.UtcNow;
                activity.Duration = stopTime - activity.StartTimeUtc;
                _current.Value = activity.Parent;
            }
        }

        /// <summary>
        /// Returns the current operation (Activity) for the current thread.  This flows 
        /// across async calls.
        /// </summary>
        public static Activity Current => _current.Value;

#region private 

        private string GenerateId()
        {
            string ret;
            if (Parent != null)
            {
                // Normal start within the process
                Debug.Assert(!string.IsNullOrEmpty(Parent.Id));
#if DEBUG
                ret = Parent.Id + "/" + OperationName + "_" + Interlocked.Increment(ref Parent._currentChildId);
#else           // To keep things short, we drop the operation name 
                ret = Parent.Id + "/" + Interlocked.Increment(ref Parent._currentChildId);
#endif
            }
            else if (ParentId != null)
            {
                // Start from outside the process (e.g. incoming HTTP)
                Debug.Assert(ParentId.Length != 0);
#if DEBUG
                ret = ParentId + "/" + OperationName + "_I_" + Interlocked.Increment(ref _currentRootId);
#else           // To keep things short, we drop the operation name 
                ret = ParentId + "/I_" + Interlocked.Increment(ref _currentRootId);
#endif
            }
            else
            {
                // A Root Activity (no parent).  
                if (_uniqPrefix == null)
                {
                    // Here we make an ID to represent the Process/AppDomain.   Ideally we use process ID but 
                    // it is unclear if we have that ID handy.   Currently we use low bits of high freq tick 
                    // as a unique random number (which is not bad, but loses randomness for startup scenarios).  
                    int uniqNum = (int)Stopwatch.GetTimestamp();
                    string uniqPrefix = $"//{Environment.MachineName}_{uniqNum:x}_";
                    Interlocked.CompareExchange(ref _uniqPrefix, uniqPrefix, null);
                }
#if DEBUG
                ret = _uniqPrefix + OperationName + "_" + Interlocked.Increment(ref _currentRootId);
#else           // To keep things short, we drop the operation name 
                ret = _uniqPrefix + Interlocked.Increment(ref _currentRootId);
#endif
            }
            // Useful place to place a conditional breakpoint.  
            return ret;
        }

        // Used to generate an ID 
        long _currentChildId;            // A unique number for all children of this activity.  
        static long _currentRootId;      // A unique number inside the appdomain.
        static string _uniqPrefix;      // A unique prefix that represents the machine/process/appdomain


        /// <summary>
        /// Having our own key-value linked list allows us to be more efficient  
        /// </summary>
        private class KeyValueListNode
        {
            public KeyValuePair<string, string> keyValue;
            public KeyValueListNode Next;
        }

        private KeyValueListNode _tags;
        private KeyValueListNode _baggage;
        //TODO: NETSTANDARD1.1
        private static readonly AsyncLocal<Activity> _current = new AsyncLocal<Activity>();
        private bool isFinished;
#endregion // private
    }
}
