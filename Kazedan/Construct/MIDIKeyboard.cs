using System;
using System.Drawing;
using Kazedan.Graphics;
using SlimDX.Direct2D;

namespace Kazedan.Construct
{
    class MIDIKeyboard
    {
        public int[] KeyPressed { get; }
        public int[] ChannelVolume { get; }
        public int[] Pitchwheel { get; }

        public MIDIKeyboard()
        {
            KeyPressed = new int[128];
            ChannelVolume = new int[16];
            Pitchwheel = new int[16];

            for (int i = 0; i < 16; i++)
            {
                ChannelVolume[i] = 127;
                Pitchwheel[i] = 0x2000;
            }
        }

        public void Reset()
        {
            Array.Clear(KeyPressed, 0, KeyPressed.Length);
        }
    }
}
