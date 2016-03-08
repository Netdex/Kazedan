using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MIDITrailer
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
