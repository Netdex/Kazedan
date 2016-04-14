using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SlimDX.Direct2D;

namespace Kazedan.Graphics.Renderer
{
    abstract class KeyRenderer
    {
        public abstract void Render(RenderTarget target, int[] KeyPressed);
    }
}
