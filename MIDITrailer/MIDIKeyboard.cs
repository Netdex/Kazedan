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
    class MIDIKeyboard : Renderable
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

        public void Render(RenderTarget target)
        {
            target.FillRectangle(KeyboardGradient, new RectangleF(0, KeyboardY, target.Size.Width, KEY_HEIGHT));
            target.DrawLine(DefaultBrushes[1], 0, KeyboardY, target.Size.Width, KeyboardY, 1f);
            for (int i = 0; i < 128; i++)
            {
                float keyX = i * KeyWidth;
                if (IsBlack[i % 12])
                {
                    if (KeyPressed[i] > 0)
                    {
                        target.FillRectangle(DefaultBrushes[4], new RectangleF(keyX, KeyboardY, KeyWidth, BLACK_KEY_HEIGHT));
                    }
                    else
                    {
                        target.FillRectangle(DefaultBrushes[2], new RectangleF(keyX, KeyboardY, KeyWidth, BLACK_KEY_HEIGHT));
                        target.FillRectangle(DefaultBrushes[1], new RectangleF(keyX, KeyboardY + BLACK_KEY_HEIGHT * 4f / 5, KeyWidth, BLACK_KEY_HEIGHT / 5f));
                    }
                }
                else
                {
                    if (KeyPressed[i] > 0)
                    {
                        target.FillRectangle(DefaultBrushes[3], new RectangleF(keyX, KeyboardY, KeyWidth, KEY_HEIGHT));
                        if (IsBlack[(i + 1) % 12])
                            target.FillRectangle(DefaultBrushes[3], new RectangleF(keyX + KeyWidth, KeyboardY + BLACK_KEY_HEIGHT, KeyWidth / 2, KEY_HEIGHT - BLACK_KEY_HEIGHT));
                        if (IsBlack[(i + 11) % 12])
                            target.FillRectangle(DefaultBrushes[3], new RectangleF(keyX - KeyWidth / 2, KeyboardY + BLACK_KEY_HEIGHT, KeyWidth / 2, KEY_HEIGHT - BLACK_KEY_HEIGHT));
                    }
                    else
                    {
                        target.FillRectangle(DefaultBrushes[2], new RectangleF(keyX, KeyboardY + KEY_HEIGHT * 7f / 8, KeyWidth, KEY_HEIGHT / 8f));
                        if (IsBlack[(i + 1) % 12])
                            target.FillRectangle(DefaultBrushes[2], new RectangleF(keyX + KeyWidth, KeyboardY + KEY_HEIGHT * 7f / 8, KeyWidth, KEY_HEIGHT / 8f));
                        if (IsBlack[(i + 11) % 12])
                            target.FillRectangle(DefaultBrushes[2], new RectangleF(keyX - KeyWidth / 2, KeyboardY + KEY_HEIGHT * 7f / 8, KeyWidth, KEY_HEIGHT / 8f));
                    }
                }
                if (IsBlack[i % 12])
                    target.DrawLine(DefaultBrushes[1], keyX + KeyWidth / 2, KeyboardY + BLACK_KEY_HEIGHT, i * KeyWidth + KeyWidth / 2, target.Size.Height, 1f);
                else if (!IsBlack[(i + 11) % 12])
                    target.DrawLine(DefaultBrushes[1], keyX, KeyboardY, keyX, target.Size.Height, 1f);
            }
        }
    }
}
