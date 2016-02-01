using System;
using System.Collections.Generic;
using System.ComponentModel;
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
using PresentFlags = SlimDX.DXGI.PresentFlags;
using Surface = SlimDX.DXGI.Surface;
using SwapChain = SlimDX.DXGI.SwapChain;
using SwapEffect = SlimDX.DXGI.SwapEffect;
using Timer = System.Timers.Timer;
using Usage = SlimDX.DXGI.Usage;

namespace MIDITrailer
{
    class MIDITrailer
    {
        private OutputDevice outDevice;
        private Sequence sequence;
        private Sequencer sequencer;

        private readonly Queue<Event> backlog = new Queue<Event>();
        private readonly List<Note> notes = new List<Note>();
        private readonly Note[,] lastPlayed = new Note[16, 128];

        private const int DELAY = 2;

        private readonly Size SIZE = new Size(1600, 900);
        private readonly int[] keyPressed = new int[128];

        private Timer eventTimer;
        private Timer timer;

        private RenderTarget renderTarget;

        public MIDITrailer()
        {
            eventTimer = new Timer(5) { Enabled = true };
            timer = new Timer(10) { Enabled = true };
            eventTimer.Elapsed += delegate (object sender, ElapsedEventArgs args)
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
            timer.Elapsed += delegate (object sender, ElapsedEventArgs args)
            {
                int keyboardY = (int)(renderTarget.Size.Height - KEY_HEIGHT);
                long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                float speed = 1.0f * (keyboardY) / (DELAY * 1000.0f);
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
            };
        }

        public void Init()
        {
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

            // Create swap chain and Direct3D device
            // The BgraSupport flag is needed for Direct2D compatibility otherwise RenderTarget.FromDXGI will fail!
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
            Load();
            MessagePump.Run(form, () =>
            {
                Paint(renderTarget);
                swapChain.Present(0, PresentFlags.None);
                Thread.Sleep(10);
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
                int key = args.Message.Data1;
                int vel = args.Message.Data2;
                if (cmd == ChannelCommand.NoteOff || vel == 0)
                {
                    if (lastPlayed[channel, key] != null)
                    {
                        Note n = lastPlayed[channel, key];
                        n.Playing = false;
                        if (n.Length == 0)
                            lock (notes)
                                notes.Remove(n);
                    }
                }
                else if (cmd == ChannelCommand.NoteOn)
                {
                    Note n = new Note()
                    {
                        Key = key,
                        Length = 0,
                        Playing = true,
                        Position = 0,
                        Time = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond,
                        Channel = channel,
                        Velocity = vel
                    };
                    lock (notes)
                    {
                        notes.Add(n);
                    }
                    lastPlayed[channel, key] = n;
                }
                lock (backlog)
                {
                    backlog.Enqueue(new Event(delegate
                    {
                        outDevice.Send(args.Message);
                        if (cmd == ChannelCommand.NoteOff || vel == 0)
                            keyPressed[key]--;
                        else if (cmd == ChannelCommand.NoteOn)
                            keyPressed[key]++;
                    }, DELAY));
                }
            };
            sequencer.SysExMessagePlayed += delegate (object o, SysExMessageEventArgs args)
            {
                outDevice.Send(args.Message);
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
            sequence.LoadAsync("D:/Music/midis/tetrisA2.mid");
        }

        const int KEY_HEIGHT = 40;
        const int BLACK_KEY_HEIGHT = 20;
        readonly bool[] isBlack = { false, true, false, true, false, false, true, false, true, false, true, false };
        private static readonly Color[] colors = {
            Color.Red, Color.Orange, Color.Yellow,
            Color.Green, Color.Blue, Color.Indigo,
            Color.Violet, Color.Pink, Color.OrangeRed,
            Color.GreenYellow, Color.Lime, Color.Cyan,
            Color.Purple, Color.DarkViolet, Color.Bisque, Color.Brown, Color.White, Color.Black };

        private static Brush[] brushes;

        private readonly Font debugFont = new Font(FontFamily.GenericMonospace, 10, FontStyle.Bold);

        public void Paint(RenderTarget target)
        {
            target.BeginDraw();
            target.Transform = Matrix3x2.Identity;
            target.Clear(Color.Gray);

            int kw = (int)(target.Size.Width / 128.0f);
            int keyboardY = (int)(target.Size.Height - KEY_HEIGHT);
            lock (notes)
            {
                foreach (Note n in notes)
                {
                    Rectangle rect = new Rectangle(n.Key * kw, (int)n.Position, kw, (int)n.Length);
                    target.FillRectangle(brushes[n.Channel], rect);
                    target.DrawRectangle(brushes[17], rect);
                }
            }

            target.FillRectangle(brushes[16], new RectangleF(0, keyboardY, target.Size.Width, KEY_HEIGHT));
            for (int i = 0; i < 128; i++)
            {
                if (isBlack[i%12])
                {
                    target.FillRectangle(keyPressed[i] > 0 ? brushes[0] : brushes[17],
                        new Rectangle(i*kw, keyboardY, kw, BLACK_KEY_HEIGHT));
                }
                else
                {
                    if (keyPressed[i] > 0)
                        target.FillRectangle(brushes[0], new Rectangle(i*kw, keyboardY, kw, KEY_HEIGHT));
                }
            }
            for (int i = 0; i < 128; i++)
            {
                target.DrawLine(brushes[17], i * kw, keyboardY, i * kw, target.Size.Height, 1f);
            }
            string[] debug = {"note_count: " + notes.Count};
            TextFormat textFormat;
            using (var factory = new Factory())
            {
                textFormat = new TextFormat(factory,
                    "Consolas",
                    FontWeight.Normal,
                    SlimDX.DirectWrite.FontStyle.Normal,
                    FontStretch.Normal,
                    20,
                    "en-us");
            }
            
            for (int i = 0; i < debug.Length; i++)
                target.DrawText(debug[i], textFormat, new Rectangle(10,10 + i * 15,400,0), brushes[17], DrawTextOptions.None, MeasuringMethod.Natural);
            target.EndDraw();
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
            StartTime = DateTime.Now + TimeSpan.FromSeconds(delay);
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
