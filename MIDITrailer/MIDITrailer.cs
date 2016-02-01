using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Sanford.Multimedia.Midi;

namespace MIDITrailer
{
    public partial class MIDITrailer : Form
    {

        private OutputDevice outDevice;
        private Sequence sequence;
        private Sequencer sequencer;

        private readonly Queue<Event> backlog = new Queue<Event>();
        private readonly List<Note> notes = new List<Note>();
        private readonly Note[,] lastPlayed = new Note[16, 128];

        private const int DELAY = 3;

        private readonly Size SIZE = new Size(1024, 768);
        private readonly int[] keyPressed = new int[128];

        public MIDITrailer()
        {
            InitializeComponent();
            this.SetStyle(ControlStyles.DoubleBuffer, true);
            this.SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            this.SetStyle(ControlStyles.UserPaint, true);
        }


        private void MIDITrailer_Load(object sender, EventArgs e)
        {
            ClientSize = SIZE;
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
            sequence.LoadAsync("D:/Music/midis/thelostemotionmidi.mid");
        }

        const int KEY_HEIGHT = 40;
        const int BLACK_KEY_HEIGHT = 20;
        readonly bool[] isBlack = { false, true, false, true, false, false, true, false, true, false, true, false };
        private static readonly Brush[] brushes = { Brushes.Red, Brushes.Orange, Brushes.Yellow, Brushes.Green, Brushes.Blue, Brushes.Indigo, Brushes.Violet,
                                            Brushes.Pink, Brushes.OrangeRed, Brushes.GreenYellow, Brushes.Lime, Brushes.Cyan, Brushes.Purple, Brushes.DarkViolet, Brushes.Bisque, Brushes.Brown };

        private readonly Font debugFont = new Font(FontFamily.GenericMonospace, 10, FontStyle.Bold);

        protected override void OnPaint(PaintEventArgs args)
        {
            Graphics g = args.Graphics;
            g.InterpolationMode = InterpolationMode.Low;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.SmoothingMode = SmoothingMode.HighSpeed;
            g.TextRenderingHint = TextRenderingHint.SystemDefault;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            g.Clear(Color.Gray);

            int kw = (int)(SIZE.Width / 128.0f);
            int keyboardY = SIZE.Height - KEY_HEIGHT;
            lock (notes)
            {
                foreach (Note n in notes)
                {
                    Rectangle rect = new Rectangle(n.Key * kw, (int)n.Position, kw, (int)n.Length);
                    g.FillRectangle(brushes[n.Channel], rect);
                    g.DrawRectangle(Pens.Black, rect);
                }
            }

            g.FillRectangle(Brushes.White, 0, keyboardY, SIZE.Width, KEY_HEIGHT);
            for (int i = 0; i < 128; i++)
            {
                Rectangle rect = new Rectangle(i * kw, keyboardY, kw, KEY_HEIGHT);
                if (isBlack[i % 12])
                    g.FillRectangle(keyPressed[i] > 0 ? Brushes.Red : Brushes.Black, i * kw, keyboardY, kw, BLACK_KEY_HEIGHT);
                else
                    if (keyPressed[i] > 0)
                    g.FillRectangle(Brushes.Red, rect);
                g.DrawRectangle(Pens.Black, rect);
            }

            string[] debug =
            {
                "       fps: " + fps,
                "note_count: " + notes.Count
            };
            for(int i = 0; i < debug.Length; i++)
                g.DrawString(debug[i], debugFont, Brushes.Black, 10, 10 + 15 * i);
        }

        private void MIDITrailer_FormClosing(object sender, FormClosingEventArgs e)
        {
            sequence.Dispose();
            sequencer.Stop();
            sequencer.Dispose();
            outDevice.Close();
            outDevice.Dispose();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            int keyboardY = SIZE.Height - KEY_HEIGHT;
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
        }

        private void eventTimer_Tick(object sender, EventArgs e)
        {
            lock (backlog)
            {
                while (backlog.Any() && backlog.First().StartTime <= DateTime.Now)
                {
                    Event ev = backlog.Dequeue();
                    ev.Method();
                }
            }
        }

        private long lastFrame = -1;
        private int fps;

        private void paintTimer_Tick(object sender, EventArgs e)
        {
            long thisFrame = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            long diff = thisFrame - lastFrame;
            fps = (int)(1000.0 / diff);
            lastFrame = thisFrame;
            Refresh();
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
