using System.Collections.Generic;

namespace Kazedan.Construct
{
    class NoteManager
    {
        public readonly Queue<Event> Backlog = new Queue<Event>();
        public readonly List<Note> Notes = new List<Note>();
        public readonly Note[,] LastPlayed = new Note[16, 128];

        public const int ReturnToFancyDelay = 3000;
        public const int ForcedFastThreshold = 1750;

        public bool UserEnabledFancy = true;
        public bool RenderFancy = true;

        public void Reset()
        {
            lock (Notes)
            {
                Notes.Clear();
            }
            Backlog.Clear();
        }
    }
}
