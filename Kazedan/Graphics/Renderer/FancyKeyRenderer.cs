using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct2D;

namespace Kazedan.Graphics.Renderer
{
    class FancyKeyRenderer : KeyRenderer
    {
        public override void Render(RenderTarget target, int[] KeyPressed)
        {
            target.FillRectangle(GFXResources.KeyboardGradient,
                new RectangleF
                (
                    0,
                    GFXResources.KeyboardY,
                    target.Size.Width,
                    GFXResources.KeyHeight
                )
            );
            target.DrawLine(GFXResources.DefaultBrushes[1], 0, GFXResources.KeyboardY, target.Size.Width, GFXResources.KeyboardY, 1f);
            for (int i = GFXResources.NoteOffset; i < GFXResources.NoteOffset + GFXResources.NoteCount; i++)
            {
                float keyX = i * GFXResources.KeyWidth - GFXResources.NoteOffset * GFXResources.KeyWidth;
                if (GFXResources.IsBlack[i % 12])
                {
                    if (KeyPressed[i] > 0)                      /* Black key which is pressed */
                    {
                        target.FillRectangle
                        (
                            // Fill the inside red
                            GFXResources.DefaultBrushes[4],
                            new RectangleF
                            (
                                keyX,
                                GFXResources.KeyboardY,
                                GFXResources.KeyWidth,
                                GFXResources.BlackKeyHeight
                            )
                        );
                    }
                    else                                        /* Black key which is not pressed */
                    {
                        // Fill the top gray
                        target.FillRectangle
                        (
                            GFXResources.DefaultBrushes[2],
                            new RectangleF
                            (
                                keyX,
                                GFXResources.KeyboardY,
                                GFXResources.KeyWidth,
                                GFXResources.BlackKeyHeight
                            )
                        );
                        // Fill the bezel black
                        target.FillRectangle
                        (
                            GFXResources.DefaultBrushes[1],
                            new RectangleF
                            (
                                keyX,
                                GFXResources.KeyboardY + GFXResources.BlackKeyHeight * 4f / 5,
                                GFXResources.KeyWidth,
                                GFXResources.BlackKeyHeight / 5f
                            )
                        );
                    }
                }
                else
                {
                    if (KeyPressed[i] > 0)                      /* White key which is pressed */
                    {
                        // Fill the middle white part red
                        target.FillRectangle
                        (
                            GFXResources.DefaultBrushes[3],
                            new RectangleF
                            (
                                keyX,
                                GFXResources.KeyboardY,
                                GFXResources.KeyWidth,
                                GFXResources.KeyHeight
                            )
                        );
                        if (GFXResources.IsBlack[(i + 1) % 12])
                        {
                            // Fill the next half section red if the next note is black
                            target.FillRectangle
                            (
                                GFXResources.DefaultBrushes[3],
                                new RectangleF
                                (
                                    keyX + GFXResources.KeyWidth,
                                    GFXResources.KeyboardY + GFXResources.BlackKeyHeight,
                                    GFXResources.KeyWidth / 2,
                                    GFXResources.KeyHeight - GFXResources.BlackKeyHeight
                                )
                            );
                        }
                        if (GFXResources.IsBlack[(i + 11) % 12])
                        {
                            // Fill the previous half section red if the previous note is black 
                            target.FillRectangle
                            (
                                GFXResources.DefaultBrushes[3],
                                new RectangleF
                                (
                                    keyX - GFXResources.KeyWidth / 2,
                                    GFXResources.KeyboardY + GFXResources.BlackKeyHeight,
                                    GFXResources.KeyWidth / 2,
                                    GFXResources.KeyHeight - GFXResources.BlackKeyHeight
                                )
                            );
                        }
                    }
                    else                                        /* White note which is not pressed */
                    {
                        // Fill the note bezel gray
                        target.FillRectangle
                        (
                            GFXResources.DefaultBrushes[6],
                            new RectangleF
                            (
                                keyX,
                                GFXResources.KeyboardY + GFXResources.KeyHeight * 7f / 8,
                                GFXResources.KeyWidth,
                                GFXResources.KeyHeight / 8f
                            )
                        );
                        if (GFXResources.IsBlack[(i + 1) % 12])
                        {
                            // If the next note is black fill the next half section white
                            target.FillRectangle
                            (
                                GFXResources.DefaultBrushes[6],
                                new RectangleF
                                (
                                    keyX + GFXResources.KeyWidth,
                                    GFXResources.KeyboardY + GFXResources.KeyHeight * 7f / 8,
                                    GFXResources.KeyWidth,
                                    GFXResources.KeyHeight / 8f
                                )
                            );
                        }
                        if (GFXResources.IsBlack[(i + 11) % 12])
                        {
                            // If the previous note is black fill the previous half section white
                            target.FillRectangle
                            (
                                GFXResources.DefaultBrushes[6],
                                new RectangleF
                                (
                                    keyX - GFXResources.KeyWidth / 2,
                                    GFXResources.KeyboardY + GFXResources.KeyHeight * 7f / 8,
                                    GFXResources.KeyWidth,
                                    GFXResources.KeyHeight / 8f
                                )
                            );
                        }
                    }
                }
            }
            for (int i = GFXResources.NoteOffset; i < GFXResources.NoteOffset + GFXResources.NoteCount; i++)
            {
                float keyX = i * GFXResources.KeyWidth - GFXResources.NoteOffset * GFXResources.KeyWidth;
                // Draw note separator lines
                if (GFXResources.IsBlack[i % 12])
                {
                    target.DrawLine
                    (
                        GFXResources.DefaultBrushes[1],
                        keyX + GFXResources.KeyWidth / 2,
                        GFXResources.KeyboardY + GFXResources.BlackKeyHeight,
                        keyX + GFXResources.KeyWidth / 2,
                        target.Size.Height,
                        1f
                    );
                }
                else if (!GFXResources.IsBlack[(i + 11) % 12])
                {
                    target.DrawLine
                    (
                        GFXResources.DefaultBrushes[1],
                        keyX,
                        GFXResources.KeyboardY,
                        keyX,
                        target.Size.Height,
                        1f
                    );
                }
            }
        }
    }
}
