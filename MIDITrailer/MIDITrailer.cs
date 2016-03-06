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
        private readonly Queue<Event> backlog = new Queue<Event>();
        private readonly List<Note> notes = new List<Note>();
        private readonly Note[,] lastPlayed = new Note[16, 128];

        private RenderTarget renderTarget;

        private Timer eventTimer;

        private const int RETURN_TO_FANCY_DELAY = 5000;
        private const int AUTO_FAST = 1750;
        private int Delay = 700;

        private bool UserFancy = true;
        private bool Fancy = true;
        private bool ShowDebug = true;

        private int Loading = -1;
        private long LastFancyTick = 0;

        private const string MIDIFile = @"D:\Music\midis\banomico.mid";
        private OutputDevice outDevice;
        private Sequence sequence;
        private Sequencer sequencer;

        private readonly MIDIKeyboard Keyboard;

        public MIDITrailer()
        {
            Keyboard = new MIDIKeyboard();
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
            renderTarget.TextAntialiasMode = TextAntialiasMode.Aliased;

            using (var DXGIFactory = swapChain.GetParent<FactoryDXGI>())
                DXGIFactory.SetWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAltEnter);

            form.ClientSize = Bounds;
            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            #endregion

            GFXResources.Init(renderTarget);

            #region init_timers
            eventTimer = new Timer(15) { Enabled = true };
            eventTimer.Elapsed += delegate
            {
                lock (backlog)
                {
                    while (backlog.Any() && backlog.First().StartTime <= DateTime.Now)
                    {
                        Event ev = backlog.Dequeue();
                        ev.Method();
                    }
                }
            };
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
                        Fancy = !Fancy;
                        UserFancy = !UserFancy;
                        break;
                    case Keys.Up:
                        Delay += 100;
                        notes.Clear();
                        backlog.Clear();
                        Array.Clear(Keyboard.KeyPressed, 0, Keyboard.KeyPressed.Length);
                        break;
                    case Keys.Down:
                        if (Delay >= 100)
                        {
                            Delay -= 100;
                            notes.Clear();
                            backlog.Clear();
                            Array.Clear(Keyboard.KeyPressed, 0, Keyboard.KeyPressed.Length);
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
                        ShowDebug = !ShowDebug;
                        break;
                }
            };

            Thread t = new Thread(Load);
            t.Start();

            MessagePump.Run(form, () =>
            {
                UpdateNotePositions();
                UpdateRenderer();
                Paint(renderTarget);
                swapChain.Present(1, PresentFlags.None);
            });

            renderTarget.Dispose();
            swapChain.Dispose();
            device.Dispose();
            outDevice.Close();
            outDevice.Dispose();
            sequencer.Stop();
            sequencer.Dispose();
            sequence?.Dispose();
        }

        private void Load()
        {
            Loading = 0;
            outDevice = new OutputDevice(0);
            sequencer = new Sequencer();
            sequence = new Sequence();

            sequencer.ChannelMessagePlayed += delegate (object o, ChannelMessageEventArgs args)
            {
                ChannelCommand cmd = args.Message.Command;
                int channel = args.Message.MidiChannel;
                int data1 = args.Message.Data1;
                int data2 = args.Message.Data2;
                if (cmd == ChannelCommand.NoteOff || (cmd == ChannelCommand.NoteOn && data2 == 0))
                {
                    if (lastPlayed[channel, data1] != null)
                    {
                        Note n = lastPlayed[channel, data1];
                        n.Playing = false;
                    }
                }
                else if (cmd == ChannelCommand.NoteOn)
                {
                    Note n = new Note()
                    {
                        Key = data1,
                        Length = 0,
                        Playing = true,
                        Position = 0,
                        Time = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond,
                        Channel = channel,
                        Velocity = data2
                    };
                    lock (notes)
                        notes.Add(n);
                    if (lastPlayed[channel, data1] != null)
                        lastPlayed[channel, data1].Playing = false;
                    lastPlayed[channel, data1] = n;
                }

                lock (backlog)
                {
                    backlog.Enqueue(new Event(delegate
                    {
                        outDevice.Send(args.Message);
                        if (cmd == ChannelCommand.NoteOff || (cmd == ChannelCommand.NoteOn && data2 == 0))
                        {
                            if (Keyboard.KeyPressed[data1] > 0)
                                Keyboard.KeyPressed[data1]--;
                        }
                        else if (cmd == ChannelCommand.NoteOn)
                        {
                            Keyboard.KeyPressed[data1]++;
                        }
                        else if (cmd == ChannelCommand.Controller)
                        {
                            if (data1 == 0x07)
                                Keyboard.ChannelVolume[channel] = data2;
                        }
                        else if (cmd == ChannelCommand.PitchWheel)
                        {
                            int pitchValue = Get14BitValue(data1, data2);
                            Keyboard.Pitchwheel[channel] = pitchValue;
                        }
                    }, Delay));
                }
            };
            sequencer.SysExMessagePlayed += delegate (object o, SysExMessageEventArgs args)
            {
                lock (backlog)
                    backlog.Enqueue(new Event(() => outDevice.Send(args.Message), Delay));
            };
            sequencer.Chased += delegate (object o, ChasedEventArgs args)
            {
                foreach (ChannelMessage message in args.Messages)
                    lock (backlog)
                        backlog.Enqueue(new Event(() => outDevice.Send(message), Delay));
            };
            sequencer.Stopped += delegate (object o, StoppedEventArgs args)
            {
                foreach (ChannelMessage message in args.Messages)
                    lock (backlog)
                        backlog.Enqueue(new Event(() => outDevice.Send(message), Delay));
            };
            sequence.LoadCompleted += delegate (object o, AsyncCompletedEventArgs args)
            {
                Loading = -1;
                sequencer.Sequence = sequence;
                sequencer.Start();
            };
            sequence.LoadProgressChanged += delegate (object sender, ProgressChangedEventArgs args)
            {
                Loading = args.ProgressPercentage;
            };
            sequence.LoadAsync(MIDIFile);
        }

        public void Paint(RenderTarget target)
        {
            target.BeginDraw();
            target.Transform = Matrix3x2.Identity;
            if (Fancy)
                target.FillRectangle(BackgroundGradient, new RectangleF(PointF.Empty, target.Size));
            else
                target.Clear(Color.Black);
            #region draw_notes
            lock (notes)
            {
                foreach (Note n in notes)
                {
                    float wheelOffset = (Keyboard.Pitchwheel[n.Channel] - 8192) / 8192f * 2 * KeyWidth;
                    float bottom = n.Position + n.Length;
                    float left = n.Key * KeyWidth + (bottom >= KeyboardY ? wheelOffset : 0) - NoteOffset * KeyWidth;
                    if (Fancy)
                    {
                        NoteRoundRect.Left = left;
                        NoteRoundRect.Top = n.Position;
                        NoteRoundRect.Right = left + KeyWidth;
                        NoteRoundRect.Bottom = bottom;

                        float alpha = n.Velocity / 127f * (Keyboard.ChannelVolume[n.Channel] / 127f);
                        alpha *= alpha;
                        var gradientBrush = ChannelGradientBrushes[n.Channel];
                        gradientBrush.Opacity = alpha;
                        GradientPoint.X = NoteRoundRect.Left;
                        gradientBrush.StartPoint = GradientPoint;
                        GradientPoint.X = NoteRoundRect.Right;
                        gradientBrush.EndPoint = GradientPoint;
                        target.FillRoundedRectangle(ChannelGradientBrushes[n.Channel], NoteRoundRect);
                    }
                    else
                    {
                        NoteRect.X = left;
                        NoteRect.Y = n.Position;
                        NoteRect.Width = KeyWidth;
                        NoteRect.Height = n.Length;
                        target.FillRectangle(ChannelBrushes[n.Channel], NoteRect);
                    }
                }
            }
            #endregion

            Keyboard.Render(target);

            // Draw time progress bar
            if (sequence?.GetLength() > 0)
            {
                float percentComplete = 1f * sequencer.Position / sequence.GetLength();
                target.FillRectangle(DefaultBrushes[5],
                    new RectangleF(ProgressBarBounds.X, ProgressBarBounds.Y, ProgressBarBounds.Width * percentComplete, ProgressBarBounds.Height));
                target.DrawRectangle(DefaultBrushes[2], ProgressBarBounds, .8f);
            }

            string[] debug;
            string usage = Application.ProductName + " " + Application.ProductVersion + " (c) " + Application.CompanyName;
            if (ShowDebug)
            {
                debug = new[]
                {
                    usage,
                    "       file: " + MIDIFile,
                    " note_count: " + notes.Count,
                    "   renderer: " + (Fancy ? "fancy" : UserFancy ? "forced-fast" : "fast"),
                    "       tick: " + (sequence == null ? "? / ?" : sequencer.Position + " / " + sequence.GetLength()),
                    "      delay: " + Delay,
                    "note_offset: " + NoteOffset,
                    " kbd_length: " + NoteCount,
                    "  key_width: " + KeyWidth
                };

            }
            else
            {
                debug = new[] { usage };
            }
            string debugText = debug.Aggregate("", (current, ss) => current + ss + '\n');
            target.DrawText(debugText, DebugFormat, DebugRectangle, DefaultBrushes[0], DrawTextOptions.None,
                MeasuringMethod.Natural);

            if (Loading == 0)
                target.DrawText("INITIALIZING MIDI DEVICES", HugeFormat, FullRectangle, DefaultBrushes[0], DrawTextOptions.None, MeasuringMethod.Natural);
            else if (Loading > 0)
                target.DrawText("LOADING " + Loading + "%", HugeFormat, FullRectangle, DefaultBrushes[0], DrawTextOptions.None, MeasuringMethod.Natural);
            target.EndDraw();
        }

        public void UpdateNotePositions()
        {
            int keyboardY = (int)(renderTarget.Size.Height - KEY_HEIGHT);
            long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            float speed = 1.0f * keyboardY / Delay;
            lock (notes)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    Note n = notes[i];
                    if (!n.Playing)
                        n.Position = (now - n.Time) * speed - n.Length;
                    else
                        n.Length = (now - n.Time) * speed;
                    if (n.Position > keyboardY)
                    {
                        notes.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public void UpdateRenderer()
        {
            if (notes.Count > AUTO_FAST)
            {
                if (Fancy)
                {
                    LastFancyTick = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                    Fancy = false;
                }
            }
            else
            {
                if (UserFancy)
                    if (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - LastFancyTick > RETURN_TO_FANCY_DELAY)
                        Fancy = true;
            }
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

        public static int Get14BitValue(int nLowerPart, int nHigherPart)
        {
            return (nLowerPart & 0x7F) | ((nHigherPart & 0x7F) << 7);
        }

        public static void Main(string[] args)
        {
            MIDITrailer program = new MIDITrailer();
            program.Init();
        }
    }

    class Event
    {
        public DateTime StartTime { get; set; }
        public Action Method { get; set; }

        public Event(Action method, int delay)
        {
            Method = method;
            StartTime = DateTime.Now + TimeSpan.FromMilliseconds(delay);
        }
    }

    class Note
    {
        public int Key { get; set; }
        public int Velocity { get; set; }
        public float Position { get; set; }
        public float Length { get; set; }
        public bool Playing { get; set; }
        public long Time { get; set; }
        public int Channel { get; set; }
    }

}
