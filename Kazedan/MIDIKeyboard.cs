using System;
using System.Drawing;
using SlimDX.Direct2D;

namespace Kazedan
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
            target.FillRectangle(GFXResources.KeyboardGradient, new RectangleF(0, GFXResources.KeyboardY, target.Size.Width, GFXResources.KeyHeight));
            target.DrawLine(GFXResources.DefaultBrushes[1], 0, GFXResources.KeyboardY, target.Size.Width, GFXResources.KeyboardY, 1f);
            for (int i = GFXResources.NoteOffset; i < GFXResources.NoteOffset + GFXResources.NoteCount; i++)
            {
                float keyX = i * GFXResources.KeyWidth - GFXResources.NoteOffset * GFXResources.KeyWidth;
                if (GFXResources.IsBlack[i % 12])
                {
                    if (KeyPressed[i] > 0)
                    {
                        target.FillRectangle(GFXResources.DefaultBrushes[4], new RectangleF(keyX, GFXResources.KeyboardY, GFXResources.KeyWidth, GFXResources.BlackKeyHeight));
                    }
                    else
                    {
                        target.FillRectangle(GFXResources.DefaultBrushes[2], new RectangleF(keyX, GFXResources.KeyboardY, GFXResources.KeyWidth, GFXResources.BlackKeyHeight));
                        target.FillRectangle(GFXResources.DefaultBrushes[1], new RectangleF(keyX, GFXResources.KeyboardY + GFXResources.BlackKeyHeight * 4f / 5, GFXResources.KeyWidth, GFXResources.BlackKeyHeight / 5f));
                    }
                }
                else
                {
                    if (KeyPressed[i] > 0)
                    {
                        target.FillRectangle(GFXResources.DefaultBrushes[3], new RectangleF(keyX, GFXResources.KeyboardY, GFXResources.KeyWidth, GFXResources.KeyHeight));
                        if (GFXResources.IsBlack[(i + 1) % 12])
                            target.FillRectangle(GFXResources.DefaultBrushes[3], new RectangleF(keyX + GFXResources.KeyWidth, GFXResources.KeyboardY + GFXResources.BlackKeyHeight, GFXResources.KeyWidth / 2, GFXResources.KeyHeight - GFXResources.BlackKeyHeight));
                        if (GFXResources.IsBlack[(i + 11) % 12])
                            target.FillRectangle(GFXResources.DefaultBrushes[3], new RectangleF(keyX - GFXResources.KeyWidth / 2, GFXResources.KeyboardY + GFXResources.BlackKeyHeight, GFXResources.KeyWidth / 2, GFXResources.KeyHeight - GFXResources.BlackKeyHeight));
                    }
                    else
                    {
                        target.FillRectangle(GFXResources.DefaultBrushes[6], new RectangleF(keyX, GFXResources.KeyboardY + GFXResources.KeyHeight * 7f / 8, GFXResources.KeyWidth, GFXResources.KeyHeight / 8f));
                        if (GFXResources.IsBlack[(i + 1) % 12])
                            target.FillRectangle(GFXResources.DefaultBrushes[6], new RectangleF(keyX + GFXResources.KeyWidth, GFXResources.KeyboardY + GFXResources.KeyHeight * 7f / 8, GFXResources.KeyWidth, GFXResources.KeyHeight / 8f));
                        if (GFXResources.IsBlack[(i + 11) % 12])
                            target.FillRectangle(GFXResources.DefaultBrushes[6], new RectangleF(keyX - GFXResources.KeyWidth / 2, GFXResources.KeyboardY + GFXResources.KeyHeight * 7f / 8, GFXResources.KeyWidth, GFXResources.KeyHeight / 8f));
                    }
                }
                if (GFXResources.IsBlack[i % 12])
                    target.DrawLine(GFXResources.DefaultBrushes[1], keyX + GFXResources.KeyWidth / 2, GFXResources.KeyboardY + GFXResources.BlackKeyHeight, keyX + GFXResources.KeyWidth / 2, target.Size.Height, 1f);
                else if (!GFXResources.IsBlack[(i + 11) % 12])
                    target.DrawLine(GFXResources.DefaultBrushes[1], keyX, GFXResources.KeyboardY, keyX, target.Size.Height, 1f);
            }
        }
    }
}
