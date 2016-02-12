using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct2D;

namespace MIDITrailer
{
    interface Renderable
    {
        void Render(RenderTarget target);
    }
}
