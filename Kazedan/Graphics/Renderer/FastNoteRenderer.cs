using System.Collections.Generic;
using Kazedan.Construct;
using SlimDX.Direct2D;

namespace Kazedan.Graphics.Renderer
{
    class FastNoteRenderer : NoteRenderer
    {
        public override void Render(RenderTarget target, List<Note> notes, MIDIKeyboard keyboard)
        {
            foreach (Note n in notes)
            {
                if (n.Key >= GFXResources.NoteOffset && n.Key < GFXResources.NoteOffset + GFXResources.NoteCount && n.Length > 0 && n.Velocity > 0)
                {
                    // Calculate pitchbend offset to give notes a sliding effect
                    /* DISABLED FOR PERFORMANCE
                    float wheelOffset = (keyboard.Pitchwheel[n.Channel] - 8192) / 8192f * 2 * GFXResources.KeyWidth;
                    float bottom = n.Position + n.Length;
                    float left = n.Key * GFXResources.KeyWidth + (bottom >= GFXResources.KeyboardY ? wheelOffset : 0) - GFXResources.NoteOffset * GFXResources.KeyWidth;
                    */
                    float left = n.Key * GFXResources.KeyWidth - GFXResources.NoteOffset * GFXResources.KeyWidth;
                    GFXResources.NoteRect.X = left;
                    GFXResources.NoteRect.Y = n.Position;
                    GFXResources.NoteRect.Width = GFXResources.KeyWidth;
                    GFXResources.NoteRect.Height = n.Length;
                    target.FillRectangle(GFXResources.ChannelBrushes[n.Channel], GFXResources.NoteRect);
                }
            }
        }
    }
}
