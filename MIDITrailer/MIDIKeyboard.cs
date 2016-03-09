using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct2D;

using static MIDITrailer.GFXResources;

namespace MIDITrailer
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

        public void Render(RenderTarget target)
        {
            target.FillRectangle(KeyboardGradient, new RectangleF(0, KeyboardY, target.Size.Width, KeyHeight));
            target.DrawLine(DefaultBrushes[1], 0, KeyboardY, target.Size.Width, KeyboardY, 1f);
            for (int i = NoteOffset; i < NoteOffset + NoteCount; i++)
            {
                float keyX = i * KeyWidth - NoteOffset * KeyWidth;
                if (IsBlack[i % 12])
                {
                    if (KeyPressed[i] > 0)
                    {
                        target.FillRectangle(DefaultBrushes[4], new RectangleF(keyX, KeyboardY, KeyWidth, BlackKeyHeight));
                    }
                    else
                    {
                        target.FillRectangle(DefaultBrushes[2], new RectangleF(keyX, KeyboardY, KeyWidth, BlackKeyHeight));
                        target.FillRectangle(DefaultBrushes[1], new RectangleF(keyX, KeyboardY + BlackKeyHeight * 4f / 5, KeyWidth, BlackKeyHeight / 5f));
                    }
                }
                else
                {
                    if (KeyPressed[i] > 0)
                    {
                        target.FillRectangle(DefaultBrushes[3], new RectangleF(keyX, KeyboardY, KeyWidth, KeyHeight));
                        if (IsBlack[(i + 1) % 12])
                            target.FillRectangle(DefaultBrushes[3], new RectangleF(keyX + KeyWidth, KeyboardY + BlackKeyHeight, KeyWidth / 2, KeyHeight - BlackKeyHeight));
                        if (IsBlack[(i + 11) % 12])
                            target.FillRectangle(DefaultBrushes[3], new RectangleF(keyX - KeyWidth / 2, KeyboardY + BlackKeyHeight, KeyWidth / 2, KeyHeight - BlackKeyHeight));
                    }
                    else
                    {
                        target.FillRectangle(DefaultBrushes[6], new RectangleF(keyX, KeyboardY + KeyHeight * 7f / 8, KeyWidth, KeyHeight / 8f));
                        if (IsBlack[(i + 1) % 12])
                            target.FillRectangle(DefaultBrushes[6], new RectangleF(keyX + KeyWidth, KeyboardY + KeyHeight * 7f / 8, KeyWidth, KeyHeight / 8f));
                        if (IsBlack[(i + 11) % 12])
                            target.FillRectangle(DefaultBrushes[6], new RectangleF(keyX - KeyWidth / 2, KeyboardY + KeyHeight * 7f / 8, KeyWidth, KeyHeight / 8f));
                    }
                }
                if (IsBlack[i % 12])
                    target.DrawLine(DefaultBrushes[1], keyX + KeyWidth / 2, KeyboardY + BlackKeyHeight, keyX + KeyWidth / 2, target.Size.Height, 1f);
                else if (!IsBlack[(i + 11) % 12])
                    target.DrawLine(DefaultBrushes[1], keyX, KeyboardY, keyX, target.Size.Height, 1f);
            }
        }
    }
}
