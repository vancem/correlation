namespace System.Diagnostics
{
    // These should go in Microsoft.Diagnostics.DiagnosticSource.dll.   
    // TODO should these be real methods are left as extension methods?
    public static class DiagnosticSourceExtensions
    {
        public static Activity Start(this DiagnosticSource self, Activity activity, object args)
        {
            Activity.Start(activity);
            self.Write(activity.OperationName + "Start", args);
            return activity;
        }

        public static Activity Start(this DiagnosticSource self, string activityName, object args)
        {
            return self.Start(new Activity(activityName), args);
        }

        public static void Stop(this DiagnosticSource self, Activity activity, object args, DateTime stopTimeUtc = default(DateTime))
        {
            self.Write(activity.OperationName + "Stop", args);
            Activity.Stop(activity, stopTimeUtc);
        }
    }
}
