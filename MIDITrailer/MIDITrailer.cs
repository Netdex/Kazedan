using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
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
// ReSharper disable AccessToDisposedClosure

namespace MIDITrailer
{
    class MIDITrailer
    {
        private readonly Queue<Event> backlog = new Queue<Event>();
        private readonly List<Note> notes = new List<Note>();
        private readonly Note[,] lastPlayed = new Note[16, 128];
        private readonly int[] keyPressed = new int[128];
        private readonly int[] channelVolume = new int[16];
        private readonly int[] pitchwheel = new int[16];

        private RenderTarget renderTarget;
        private readonly Size SIZE = new Size(1600, 900);
        private Timer eventTimer;
        private bool Fancy = true;
        private const int DELAY = 900;

        private const string MIDIFile = @"D:\Music\midis\final countdown.mid";
        private OutputDevice outDevice;
        private Sequence sequence;
        private Sequencer sequencer;

        public MIDITrailer()
        {
            for (int i = 0; i < 16; i++)
            {
                channelVolume[i] = 127;
                pitchwheel[i] = 0x2000;
            }
        }

        public void Init()
        {
            #region init_gfx
            var form = new RenderForm("MIDITrailer");

            // Create swap chain description
            var swapChainDesc = new SwapChainDescription()
            {
                BufferCount = 2,
                Usage = Usage.RenderTargetOutput,
                OutputHandle = form.Handle,
                IsWindowed = true,
                ModeDescription = new ModeDescription(SIZE.Width, SIZE.Height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                Flags = SwapChainFlags.AllowModeSwitch,
                SwapEffect = SwapEffect.Discard,
            };

            Device device;
            SwapChain swapChain;
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.BgraSupport, swapChainDesc, out device, out swapChain);

            Surface backBuffer = Surface.FromSwapChain(swapChain, 0);

            SizeF dpi;
            using (var factory = new FactoryD2D())
            {
                dpi = factory.DesktopDpi;
                renderTarget = RenderTarget.FromDXGI(factory, backBuffer, new RenderTargetProperties()
                {
                    HorizontalDpi = dpi.Width,
                    VerticalDpi = dpi.Height,
                    MinimumFeatureLevel = SlimDX.Direct2D.FeatureLevel.Default,
                    PixelFormat = new PixelFormat(Format.R8G8B8A8_UNorm, AlphaMode.Ignore),
                    Type = RenderTargetType.Default,
                    Usage = RenderTargetUsage.None
                });
            }

            // Freaking antialiasing lagging up my programs
            if (!Fancy)
                renderTarget.AntialiasMode = AntialiasMode.Aliased;

            using (var factory = swapChain.GetParent<FactoryDXGI>())
                factory.SetWindowAssociation(form.Handle, WindowAssociationFlags.IgnoreAltEnter);

            form.KeyDown += (o, e) =>
            {
                if (e.Alt && e.KeyCode == Keys.Enter)
                    swapChain.IsFullScreen = !swapChain.IsFullScreen;
            };
            form.FormClosing += MIDITrailer_FormClosing;

            form.Size = new Size((int)(SIZE.Width / (dpi.Width / 96f)), (int)(SIZE.Height / (dpi.Height / 96f)));
            form.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            brushes = new Brush[colors.Length];
            for (int i = 0; i < colors.Length; i++)
                brushes[i] = new SolidColorBrush(renderTarget, new Color4(colors[i]));
            keyboardGradient = new LinearGradientBrush(renderTarget,
                new GradientStopCollection(renderTarget, new[] {
                    new GradientStop()
                    { Color = new Color4(Color.White),Position = 0 },
                    new GradientStop()
                    { Color = new Color4(Color.DarkGray), Position = 1 }
                }),
                new LinearGradientBrushProperties()
                {
                    StartPoint = new PointF(0, renderTarget.Size.Height),
                    EndPoint = new PointF(0, renderTarget.Size.Height - KEY_HEIGHT)
                });
            backgroundGradient = new LinearGradientBrush(renderTarget,
                new GradientStopCollection(renderTarget, new[] {
                    new GradientStop()
                    { Color = new Color4(Color.DimGray), Position = 0 },
                    new GradientStop()
                    { Color = new Color4(Color.DarkGray), Position = 1 }
                }),
                new LinearGradientBrushProperties()
                {
                    StartPoint = new PointF(0, renderTarget.Size.Height),
                    EndPoint = new PointF(0, 0)
                });
            using (var factory = new Factory())
                debugFormat = new TextFormat(factory, "Consolas", FontWeight.Normal, SlimDX.DirectWrite.FontStyle.Normal, FontStretch.Normal, 18, "en-us");
            #endregion

            #region init_timers
            eventTimer = new Timer(5) { Enabled = true };
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

            Load();
            MessagePump.Run(form, () =>
            {
                UpdateNotePositions();
                Paint(renderTarget);
                swapChain.Present(1, PresentFlags.None);
            });

            renderTarget.Dispose();
            swapChain.Dispose();
            device.Dispose();
        }

        private void Load()
        {
            outDevice = new OutputDevice(0);
            sequencer = new Sequencer();
            sequence = new Sequence();

            sequencer.ChannelMessagePlayed += delegate (object o, ChannelMessageEventArgs args)
            {
                ChannelCommand cmd = args.Message.Command;
                int channel = args.Message.MidiChannel;
                int data1 = args.Message.Data1;
                int data2 = args.Message.Data2;
                if (cmd == ChannelCommand.NoteOff || data2 == 0)
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
                        if (cmd == ChannelCommand.NoteOff || data2 == 0)
                        {
                            if (keyPressed[data1] > 0)
                                keyPressed[data1]--;
                        }
                        else if (cmd == ChannelCommand.NoteOn)
                            keyPressed[data1]++;
                        else if (cmd == ChannelCommand.Controller)
                        {
                            if (data1 == 0x07)
                            {
                                channelVolume[channel] = data2;
                            }
                        }
                        else if (cmd == ChannelCommand.PitchWheel)
                        {
                            int pitchValue = Get14BitValue(data1, data2);
                            pitchwheel[channel] = pitchValue;
                        }
                    }, DELAY));
                }
            };
            sequencer.SysExMessagePlayed += delegate (object o, SysExMessageEventArgs args)
            {
                lock (backlog)
                    backlog.Enqueue(new Event(() => outDevice.Send(args.Message), DELAY));
            };
            sequencer.Chased += delegate (object o, ChasedEventArgs args)
            {
                foreach (ChannelMessage message in args.Messages)
                    lock (backlog)
                        backlog.Enqueue(new Event(() => outDevice.Send(message), DELAY));
            };
            sequencer.Stopped += delegate (object o, StoppedEventArgs args)
            {
                foreach (ChannelMessage message in args.Messages)
                    lock (backlog)
                        backlog.Enqueue(new Event(() => outDevice.Send(message), DELAY));
            };
            sequence.LoadCompleted += delegate (object o, AsyncCompletedEventArgs args)
            {
                sequencer.Sequence = sequence;
                sequencer.Start();
            };
            sequence.LoadAsync(MIDIFile);
        }

        const int KEY_HEIGHT = 40;
        const int BLACK_KEY_HEIGHT = 20;
        readonly bool[] isBlack = { false, true, false, true, false, false, true, false, true, false, true, false };
        private static readonly Color[] colors = {
            Color.Red,          Color.Orange,       Color.Yellow,
            Color.Green,        Color.Blue,         Color.Indigo,
            Color.Violet,       Color.Pink,         Color.OrangeRed,
            Color.GreenYellow,  Color.Lime,         Color.Cyan,
            Color.Purple,       Color.DarkViolet,   Color.Bisque,
            Color.Brown,        Color.White,        Color.Black,
            Color.FromArgb(30, 30, 30),             Color.IndianRed};

        private static Brush[] brushes;
        private LinearGradientBrush keyboardGradient;
        private LinearGradientBrush backgroundGradient;
        private TextFormat debugFormat;

        public void Paint(RenderTarget target)
        {
            target.BeginDraw();
            target.Transform = Matrix3x2.Identity;
            target.FillRectangle(backgroundGradient, new RectangleF(PointF.Empty, target.Size));

            float kw = target.Size.Width / 128.0f;
            float keyboardY = target.Size.Height - KEY_HEIGHT;
            lock (notes)
            {
                foreach (Note n in notes)
                {
                    float wheelOffset = (pitchwheel[n.Channel] - 8192) / 8192f * 2 * kw;
                    RectangleF rect = new RectangleF(n.Key * kw + wheelOffset, n.Position, kw, n.Length);
                    if (Fancy)
                    {
                        float alpha = n.Velocity / 127f * (channelVolume[n.Channel] / 127f);
                        using (var brush = new SolidColorBrush(target, new Color4(colors[n.Channel]) { Alpha = alpha }))
                            target.FillRectangle(brush, rect);
                        using (var brush = new SolidColorBrush(target, new Color4(ControlPaint.Dark(colors[n.Channel])) { Alpha = alpha }))
                            target.DrawRectangle(brush, rect, 3f);
                    }
                    else
                    {
                        target.FillRectangle(brushes[n.Channel], rect);
                        target.DrawRectangle(brushes[17], rect, 1f);
                    }
                }
            }

            target.FillRectangle(keyboardGradient, new RectangleF(0, keyboardY, target.Size.Width, KEY_HEIGHT));
            target.DrawLine(brushes[17], 0, keyboardY, target.Size.Width, keyboardY, 1f);
            for (int i = 0; i < 128; i++)
            {
                if (isBlack[i % 12])
                {
                    if (keyPressed[i] > 0)
                    {
                        target.FillRectangle(brushes[0], new RectangleF(i * kw, keyboardY, kw, BLACK_KEY_HEIGHT));
                    }
                    else
                    {
                        target.FillRectangle(brushes[18], new RectangleF(i * kw, keyboardY, kw, BLACK_KEY_HEIGHT));
                        target.FillRectangle(brushes[17], new RectangleF(i * kw, keyboardY + BLACK_KEY_HEIGHT * 4f / 5, kw, BLACK_KEY_HEIGHT / 5f));
                    }
                }
                else
                {
                    if (keyPressed[i] > 0)
                    {
                        target.FillRectangle(brushes[19], new RectangleF(i * kw, keyboardY, kw, KEY_HEIGHT));
                        if (isBlack[(i + 1) % 12])
                            target.FillRectangle(brushes[19], new RectangleF(i * kw + kw, keyboardY + BLACK_KEY_HEIGHT, kw / 2, KEY_HEIGHT - BLACK_KEY_HEIGHT));
                        if (isBlack[(i + 11) % 12])
                            target.FillRectangle(brushes[19], new RectangleF(i * kw - kw / 2, keyboardY + BLACK_KEY_HEIGHT, kw / 2, KEY_HEIGHT - BLACK_KEY_HEIGHT));
                    }
                    else
                    {
                        target.FillRectangle(brushes[18], new RectangleF(i * kw, keyboardY + KEY_HEIGHT * 7f / 8, kw, KEY_HEIGHT / 8f));
                        if (isBlack[(i + 1) % 12])
                            target.FillRectangle(brushes[18], new RectangleF(i * kw + kw, keyboardY + KEY_HEIGHT * 7f / 8, kw, KEY_HEIGHT / 8f));
                        if (isBlack[(i + 11) % 12])
                            target.FillRectangle(brushes[18], new RectangleF(i * kw - kw / 2, keyboardY + KEY_HEIGHT * 7f / 8, kw, KEY_HEIGHT / 8f));
                    }
                }
                if (isBlack[i % 12])
                    target.DrawLine(brushes[17], i * kw + kw / 2, keyboardY + BLACK_KEY_HEIGHT, i * kw + kw / 2, target.Size.Height, 1f);
                else if (!isBlack[(i + 11) % 12])
                    target.DrawLine(brushes[17], i * kw, keyboardY, i * kw, target.Size.Height, 1f);
            }

            string[] debug =
            {
                "note_count: " + notes.Count,
            };

            for (int i = 0; i < debug.Length; i++)
                target.DrawText(debug[i], debugFormat, new Rectangle(10, 10 + i * 15, 400, 0), brushes[17], DrawTextOptions.None, MeasuringMethod.Natural);
            target.EndDraw();
        }

        public void UpdateNotePositions()
        {
            int keyboardY = (int)(renderTarget.Size.Height - KEY_HEIGHT);
            long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            float speed = 1.0f * keyboardY / DELAY;
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

        public static int Get14BitValue(int nLowerPart, int nHigherPart)
        {
            return (nLowerPart & 0x7F) | ((nHigherPart & 0x7F) << 7);
        }

        private void MIDITrailer_FormClosing(object sender, FormClosingEventArgs e)
        {
            sequence.Dispose();
            sequencer.Stop();
            sequencer.Dispose();
            outDevice.Close();
            outDevice.Dispose();
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
