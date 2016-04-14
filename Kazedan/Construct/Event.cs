using System;

namespace Kazedan.Construct
{
    class Event
    {
        public long StartTime { get; set; }
        public Action Method { get; set; }

        public Event(Action method, long now, int delay)
        {
            Method = method;
            StartTime = now + delay;
        }
    }
}
