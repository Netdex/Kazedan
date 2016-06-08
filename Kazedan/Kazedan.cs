using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Kazedan.Construct;
using Kazedan.Graphics;
using SlimDX;
using SlimDX.Direct2D;
using SlimDX.Direct3D11;
using SlimDX.DXGI;
using SlimDX.Windows;
using Device = SlimDX.Direct3D11.Device;
using FactoryD2D = SlimDX.Direct2D.Factory;
using FactoryDXGI = SlimDX.DXGI.Factory;
using Format = SlimDX.DXGI.Format;
using PresentFlags = SlimDX.DXGI.PresentFlags;
using Surface = SlimDX.DXGI.Surface;
using SwapChain = SlimDX.DXGI.SwapChain;
using SwapEffect = SlimDX.DXGI.SwapEffect;
using Usage = SlimDX.DXGI.Usage;

namespace Kazedan
{
    class Kazedan
    {
        private RenderTarget renderTarget;
        private RenderForm Form;

        public static MIDISequencer Sequencer;

        private long LastTick = Environment.TickCount;
        public static long Elapsed;
        private long LastSample = Environment.TickCount;
        private const long SampleRate = 1000;

        public Kazedan()
        {
            Sequencer = new MIDISequencer();
        }

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        public void Init()
        {
            #region init_gfx
            Form = new RenderForm("Kazedan");
            var factory = new FactoryD2D();
            SizeF dpi = factory.DesktopDpi;

            var swapChainDesc = new SwapChainDescription()
            {
                BufferCount = 2,
                Usage = Usage.RenderTargetOutput,
                OutputHandle = Form.Handle,
                IsWindowed = true,
                ModeDescription = new ModeDescription((int)(GFXResources.Bounds.Width * (dpi.Width / 96f)), (int)(GFXResources.Bounds.Height * (dpi.Height / 96f)), new Rational(60, 1), Format.R8G8B8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                Flags = SwapChainFlags.AllowModeSwitch,
                SwapEffect = SwapEffect.Discard,
            };

            Device device;
            SwapChain swapChain;
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, swapChainDesc, out device, out swapChain);

            Surface backBuffer = Surface.FromSwapChain(swapChain, 0);

            renderTarget = RenderTarget.FromDXGI(factory, backBuffer, new RenderTargetProperties()
            {
                HorizontalDpi = dpi.Width,
                VerticalDpi = dpi.Height,
                MinimumFeatureLevel = SlimDX.Direct2D.FeatureLevel.Default,
                PixelFormat = new PixelFormat(Format.R8G8B8A8_UNorm, AlphaMode.Ignore),
                Type = RenderTargetType.Default,
                Usage = RenderTargetUsage.None
            });
            factory.Dispose();

            // Freaking antialiasing lagging up my programs
            renderTarget.AntialiasMode = AntialiasMode.Aliased;
            renderTarget.TextAntialiasMode = TextAntialiasMode.Grayscale;
            
            using (var DXGIFactory = swapChain.GetParent<FactoryDXGI>())
                DXGIFactory.SetWindowAssociation(Form.Handle, WindowAssociationFlags.IgnoreAltEnter);

            Form.ClientSize = GFXResources.Bounds;
            Form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Form.Icon = Properties.Resources.KazedanIcon;
            #endregion

            GFXResources.Init(renderTarget);

            Form.KeyDown += (o, e) =>
            {
                Keys key = e.KeyCode;
                switch (key)
                {
                    case Keys.F11:
                        swapChain.IsFullScreen = !swapChain.IsFullScreen;
                        break;
                    case Keys.F:
                        Sequencer.NoteManager.UserEnabledFancy = !Sequencer.NoteManager.UserEnabledFancy;
                        Sequencer.NoteManager.RenderFancy = Sequencer.NoteManager.UserEnabledFancy;
                        break;
                    case Keys.Up:
                        Sequencer.Delay += 100;
                        Sequencer.Reset();
                        break;
                    case Keys.Down:
                        if (Sequencer.Delay >= 100)
                        {
                            Sequencer.Delay -= 100;
                            Sequencer.Reset();
                        }
                        break;
                    case Keys.Left:
                        if (GFXResources.NoteOffset > 0)
                            GFXResources.NoteOffset--;
                        break;
                    case Keys.Right:
                        if (GFXResources.NoteOffset < 128 - GFXResources.NoteCount)
                            GFXResources.NoteOffset++;
                        break;
                    case Keys.D:
                        Sequencer.ShowDebug = !Sequencer.ShowDebug;
                        break;
                    case Keys.Space:
                        if (Sequencer.Stopped)
                            Sequencer.Start();
                        else
                            Sequencer.Stop();
                        break;
                }
            };

            Thread loadThread = new Thread(Load);
            loadThread.Start();

            Thread controlThread = new Thread(() =>
            {
                loadThread.Join();
                Application.EnableVisualStyles();
                Application.Run(new KZControl(Sequencer));
            });
            controlThread.SetApartmentState(ApartmentState.STA);
            controlThread.Start();

            MessagePump.Run(Form, () =>
            {
                // Do sequencer tick
                if (!Sequencer.Stopped)
                {
                    Sequencer.UpdateNotePositions();
                    Sequencer.UpdateRenderer();
                }
                Paint(renderTarget);

                // Calculate profiling information
                long tick = Environment.TickCount;
                if (tick - LastSample >= SampleRate)
                {
                    Elapsed = tick - LastTick;
                    LastSample = tick;
                }
                LastTick = tick;
                swapChain.Present(1, PresentFlags.None);
            });

            renderTarget.Dispose();
            swapChain.Dispose();
            device.Dispose();
            Sequencer.Dispose();
        }

        private void Load()
        {
            Sequencer.Init();
        }

        public void Paint(RenderTarget target)
        {
            target.BeginDraw();
            target.Transform = Matrix3x2.Identity;
            // Render scene
            Sequencer.Render(target);
            target.EndDraw();
        }

        public string ToSI(double d, string format = null)
        {
            char[] incPrefixes = new[] { 'k', 'M', 'G', 'T', 'P', 'E', 'Z', 'Y' };
            char[] decPrefixes = new[] { 'm', '\u03bc', 'n', 'p', 'f', 'a', 'z', 'y' };

            int degree = (int)Math.Floor(Math.Log10(Math.Abs(d)) / 3);
            double scaled = d * Math.Pow(1000, -degree);

            char? prefix = null;
            switch (Math.Sign(degree))
            {
                case 1: prefix = incPrefixes[degree - 1]; break;
                case -1: prefix = decPrefixes[-degree - 1]; break;
            }

            return scaled.ToString(format) + prefix;
        }

        public static void Main(string[] args)
        {
            Kazedan program = new Kazedan();
            program.Init();
        }
    }
}
