using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
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
        private readonly Note[,] lastPlayed = new Note[16,128];

        private const int DELAY = 2;

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
                    if (lastPlayed[channel,key] != null)
                        lastPlayed[channel,key].Playing = false;
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
                    lastPlayed[channel,key] = n;
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
            sequence.LoadAsync("D:/Music/midis/necrofantasia.mid");
        }

        const int KEY_HEIGHT = 40;
        const int BLACK_KEY_HEIGHT = 20;
        readonly bool[] Black = { false, true, false, true, false, false, true, false, true, false, true, false };

        private readonly Color[] colors = { Color.Red, Color.Orange, Color.Yellow, Color.Green, Color.Blue, Color.Indigo, Color.Violet,
                                            Color.Pink, Color.OrangeRed, Color.GreenYellow, Color.Lime, Color.Cyan, Color.Purple, Color.DarkViolet };

        protected override void OnPaint(PaintEventArgs args)
        {
            Graphics g = args.Graphics;
            g.Clear(Color.Gray);

            float kw = SIZE.Width / 128.0f;
            g.DrawString(notes.Count + "", new Font(FontFamily.GenericMonospace, 10), Brushes.Black, 10, 10);

            lock (notes)
            {
                foreach (Note n in notes)
                {
                    g.FillRectangle(new SolidBrush(colors[n.Channel]), n.Key * kw, n.Position, kw, n.Length);
                    g.DrawRectangle(Pens.Black, n.Key * kw, n.Position, kw, n.Length);
                }
            }

            g.FillRectangle(Brushes.White, 0, SIZE.Height - KEY_HEIGHT, SIZE.Width, KEY_HEIGHT);
            for (int i = 0; i < 128; i++)
            {
                if (Black[i % 12])
                    g.FillRectangle(keyPressed[i] > 0 ? Brushes.Red : Brushes.Black, i * kw, SIZE.Height - KEY_HEIGHT, kw,
                        BLACK_KEY_HEIGHT);
                else
                    if (keyPressed[i] > 0)
                    g.FillRectangle(Brushes.Red, i * kw, SIZE.Height - KEY_HEIGHT, kw, KEY_HEIGHT);
                g.DrawRectangle(Pens.Black, i * kw, SIZE.Height - KEY_HEIGHT, kw, KEY_HEIGHT);
            }
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
            long now = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            float speed = 1.0f * (SIZE.Height - KEY_HEIGHT) / (DELAY * 1000.0f);
            lock (notes)
            {
                for (int i = 0; i < notes.Count; i++)
                {
                    Note n = notes[i];
                    if (!n.Playing)
                        n.Position = (now - n.Time) * speed - n.Length;
                    else
                        n.Length = (now - n.Time) * speed;
                    if (n.Position > SIZE.Height - KEY_HEIGHT)
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

        private void paintTimer_Tick(object sender, EventArgs e)
        {
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
