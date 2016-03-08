using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sanford.Multimedia.Midi;
using System.Timers;

using SlimDX;
using SlimDX.DXGI;
using SlimDX.Direct3D11;
using SlimDX.Direct2D;
using SlimDX.Direct3D9;
using SlimDX.DirectWrite;
using SlimDX.Windows;
using Brush = SlimDX.Direct2D.Brush;
using Device = SlimDX.Direct3D11.Device;
using Factory = SlimDX.DirectWrite.Factory;
using FactoryD2D = SlimDX.Direct2D.Factory;
using FactoryDXGI = SlimDX.DXGI.Factory;
using Font = System.Drawing.Font;
using FontFamily = System.Drawing.FontFamily;
using FontStyle = System.Drawing.FontStyle;
using FontWeight = SlimDX.DirectWrite.FontWeight;
using Format = SlimDX.DXGI.Format;
using LinearGradientBrush = SlimDX.Direct2D.LinearGradientBrush;
using PresentFlags = SlimDX.DXGI.PresentFlags;
using Surface = SlimDX.DXGI.Surface;
using SwapChain = SlimDX.DXGI.SwapChain;
using SwapEffect = SlimDX.DXGI.SwapEffect;
using Timer = System.Timers.Timer;
using Usage = SlimDX.DXGI.Usage;

using static MIDITrailer.GFXResources;

// ReSharper disable AccessToDisposedClosure
namespace MIDITrailer
{
    class MIDITrailer
    {
        private RenderTarget renderTarget;

        private const string MIDIFile = @"D:\Music\midis\Necrofantasia.mid";
        public static MIDISequencer Sequencer;

        private long LastTick = Environment.TickCount;
        public static long Elapsed = 0;
        private long LastSample = Environment.TickCount;
        private const long SampleRate = 1000;

        public MIDITrailer()
        {
            Sequencer = new MIDISequencer();
        }

        public void Init()
        {
            #region init_gfx
            var form = new RenderForm("MIDITrailer");

            var factory = new FactoryD2D();
            SizeF dpi = factory.DesktopDpi;
            // Create swap chain description
            var swapChainDesc = new SwapChainDescription()
            {
                BufferCount = 2,
                Usage = Usage.RenderTargetOutput,
                OutputHandle = form.Handle,
                IsWindowed = true,
                ModeDescription = new ModeDescription((int)(Bounds.Width * (dpi.Width / 96f)), (int)(Bounds.Height * (dpi.Height / 96f)), new Rational(60, 1), Format.R8G8B8A8_UNorm),
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
            renderTarget.TextAntialiasMode = TextAntialiasMode.Grayscale | TextAntialiasMode.Aliased;

            using (var DXGIFactory = swapChain.GetParent<FactoryDXGI>())
                DXGIFactory.SetWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAltEnter);

            form.ClientSize = Bounds;
            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            form.Icon = Properties.Resources.miditrailer;
            #endregion

            GFXResources.Init(renderTarget);

            #region init_timers

            #endregion

            form.KeyDown += (o, e) =>
            {
                Keys key = e.KeyCode;
                switch (key)
                {
                    case Keys.F11:
                        swapChain.IsFullScreen = !swapChain.IsFullScreen;
                        break;
                    case Keys.F:
                        Sequencer.NoteRenderer.UserEnabledFancy = !Sequencer.NoteRenderer.UserEnabledFancy;
                        Sequencer.NoteRenderer.RenderFancy = Sequencer.NoteRenderer.UserEnabledFancy;
                        break;
                    case Keys.Up:
                        Sequencer.Delay += 100;
                        Sequencer.Keyboard.Reset();
                        Sequencer.NoteRenderer.Reset();
                        break;
                    case Keys.Down:
                        if (Sequencer.Delay >= 100)
                        {
                            Sequencer.Delay -= 100;
                            Sequencer.Keyboard.Reset();
                            Sequencer.NoteRenderer.Reset();
                        }
                        break;
                    case Keys.Left:
                        if (NoteOffset > 0)
                            NoteOffset--;
                        break;
                    case Keys.Right:
                        if (NoteOffset < 128 - NoteCount)
                            NoteOffset++;
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

            Thread t = new Thread(Load);
            t.Start();

            MessagePump.Run(form, () =>
            {
                if (!Sequencer.Stopped)
                {
                    Sequencer.UpdateNotePositions();
                    Sequencer.UpdateRenderer();
                }
                Paint(renderTarget);

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
            Sequencer.Load(MIDIFile);
        }

        public void Reset()
        {
            Sequencer.Reset();
        }

        public void Paint(RenderTarget target)
        {
            target.BeginDraw();
            target.Transform = Matrix3x2.Identity;
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
            MIDITrailer program = new MIDITrailer();
            program.Init();
        }
    }
}
