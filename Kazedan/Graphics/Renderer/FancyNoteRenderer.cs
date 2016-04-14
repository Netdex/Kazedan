using System.Collections.Generic;
using Kazedan.Construct;
using SlimDX.Direct2D;

namespace Kazedan.Graphics.Renderer
{
    class FancyNoteRenderer : NoteRenderer
    {
        public override void Render(RenderTarget target, List<Construct.Note> notes, MIDIKeyboard keyboard)
        {
            foreach (Construct.Note n in notes)
            {
                if (n.Key > GFXResources.NoteOffset && n.Key < GFXResources.NoteOffset + GFXResources.NoteCount)
                {
                    // Calculate pitchbend offset to give notes a sliding effect
                    float wheelOffset = (keyboard.Pitchwheel[n.Channel] - 8192) / 8192f * 2 * GFXResources.KeyWidth;
                    float bottom = n.Position + n.Length;
                    float left = n.Key * GFXResources.KeyWidth + (bottom >= GFXResources.KeyboardY ? wheelOffset : 0) - GFXResources.NoteOffset * GFXResources.KeyWidth;
                    GFXResources.NoteRoundRect.Left = left;
                    GFXResources.NoteRoundRect.Top = n.Position;
                    GFXResources.NoteRoundRect.Right = left + GFXResources.KeyWidth;
                    GFXResources.NoteRoundRect.Bottom = bottom;

                    float alpha = n.Velocity / 127f * (keyboard.ChannelVolume[n.Channel] / 127f);
                    alpha *= alpha; // Square the alpha so differences are more visible and scale quadratically
                    var gradientBrush = GFXResources.ChannelGradientBrushes[n.Channel];
                    gradientBrush.Opacity = alpha;
                    GFXResources.GradientPoint.X = GFXResources.NoteRoundRect.Left;
                    gradientBrush.StartPoint = GFXResources.GradientPoint;
                    GFXResources.GradientPoint.X = GFXResources.NoteRoundRect.Right;
                    gradientBrush.EndPoint = GFXResources.GradientPoint;
                    target.FillRoundedRectangle(GFXResources.ChannelGradientBrushes[n.Channel], GFXResources.NoteRoundRect);
                }
            }
        }
    }
}
