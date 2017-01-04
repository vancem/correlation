using System.Linq;
using Xunit;

namespace System.Diagnostics.Tests
{
    public class ActivityTests
    {
        [Fact]
        public void DefaultActivity()
        {
            string activityName = "activity";
            var activity = new Activity(activityName);
            Assert.Equal(activityName, activity.OperationName);
            Assert.Null(activity.Id);
            Assert.Equal(TimeSpan.Zero, activity.Duration);            
            Assert.Null(activity.Parent);
            Assert.Null(activity.ParentId);
            Assert.Equal(0, activity.Baggage.ToList().Count);
            Assert.Equal(0, activity.Tags.ToList().Count);
        }

        [Fact]
        public void Baggage()
        {
            var activity = new Activity("activity");
            Assert.Null(activity.GetBaggageItem("some baggage"));

            Assert.Equal(activity, activity.WithBaggage("some baggage", "value"));
            Assert.Equal("value", activity.GetBaggageItem("some baggage"));

            var baggage = activity.Baggage.ToList();
            Assert.Equal(1, baggage.Count);
        }

        [Fact]
        public void Tags()
        {
            var activity = new Activity("activity");

            Assert.Equal(activity, activity.WithTag("some tag", "value"));

            var tags = activity.Tags.ToList();
            Assert.Equal(1, tags.Count);
            Assert.Equal(tags[0].Key, "some tag");
            Assert.Equal(tags[0].Value, "value");

            Assert.Equal(activity, activity.WithParentId("1"));
            Assert.Equal(2, activity.Tags.ToList().Count);
            Assert.Equal("1", activity.ParentId);
        }

        [Fact]
        public void StartStop()
        {
            var activity = new Activity("activity");
            Assert.Equal(null, Activity.Current);
            Activity.Start(activity);
            Assert.Equal(activity, Activity.Current);
            Assert.Null(activity.Parent);
            Assert.NotNull(activity.Id);
            Assert.NotEqual(default(DateTime), activity.StartTimeUtc);

            Activity.Stop(activity);
            Assert.Equal(null, Activity.Current);
        }

        [Fact]
        public void StartStopWithTimestamp()
        {
            var startTime = DateTime.UtcNow.AddSeconds(-1);
            var activity = new Activity("activity")
                .WithStartTime(startTime);

            Activity.Start(activity);
            Assert.Equal(startTime, activity.StartTimeUtc);

            var stopTime = DateTime.UtcNow;
            Activity.Stop(activity, stopTime);
            Assert.Equal(stopTime - startTime, activity.Duration);
        }


        [Fact]
        public void ParentChild()
        {
            var parent = new Activity("parent")
                .WithBaggage("id1", "baggage from parent")
                .WithTag("tag1", "tag from parent");

            Activity.Start(parent);
            Assert.Equal(parent, Activity.Current);

            var child = new Activity("child");
            Activity.Start(child);
            Assert.Equal(parent, child.Parent);
            Assert.Equal(parent.Id, child.ParentId);

            //baggage from parent
            Assert.Equal("baggage from parent", child.GetBaggageItem("id1"));

            //no tags from parent
            var childTags = child.Tags.ToList();
            Assert.Equal(1, childTags.Count);
            Assert.Equal(child.ParentId, childTags[0].Value);

            Activity.Stop(child);
            Assert.Equal(parent, Activity.Current);

            Activity.Stop(parent);
            Assert.NotEqual(TimeSpan.Zero, parent.Duration);
            Assert.Equal(null, Activity.Current);
        }

        [Fact]
        public void StopParent()
        {
            var parent = new Activity("parent");
            Activity.Start(parent);
            var child = new Activity("child");
            Activity.Start(child);

            Activity.Stop(parent);
            Assert.Equal(null, Activity.Current);
        }

        [Fact]
        public void StartTwice()
        {
            var activity = new Activity("");
            Activity.Start(activity);
            Assert.Throws<InvalidOperationException>(() => Activity.Start(activity));
        }

        [Fact]
        public void StopNotStarted()
        {
            Assert.Throws<InvalidOperationException>(() => Activity.Stop(new Activity("")));
        }
    }
}
