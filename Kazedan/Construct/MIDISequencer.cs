using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Kazedan.Graphics;
using Kazedan.Graphics.Renderer;
using Sanford.Multimedia.Midi;
using SlimDX.Direct2D;
using SlimDX.DirectWrite;
using Timer = System.Timers.Timer;

namespace Kazedan.Construct
{
    class MIDISequencer : IDisposable
    {
        public int Delay { get; set; } = 1000;
        public bool ShowDebug { get; set; } = true;
        private int Loading { get; set; } = -1;
        private long LastFancyTick { get; set; }
        public bool Stopped { get; private set; } = true;
        public bool Initialized { get; private set; }
        private readonly Stopwatch Stopwatch;

        public string MIDIFile = @"Loading...";

        private OutputDevice outDevice;
        private Sequence sequence;
        private Sequencer sequencer;

        private Timer eventTimer;

        public MIDIKeyboard Keyboard { get; set; }
        public NoteManager NoteManager { get; set; }
        private readonly NoteRenderer[] NoteRenderers = { new FastNoteRenderer(), new FancyNoteRenderer() };
        private readonly KeyRenderer[] KeyRenderers = { new FastKeyRenderer(), new FancyKeyRenderer() };

        public MIDISequencer()
        {
            Keyboard = new MIDIKeyboard();
            NoteManager = new NoteManager();
            Stopwatch = Stopwatch.StartNew();
        }

        public void Init()
        {
            // Make sure we don't initialize twice and create a disaster
            if (Initialized)
                return;
            Initialized = true;

            // Create timer for event management
            eventTimer = new Timer(15);
            eventTimer.Elapsed += delegate
            {
                lock (NoteManager.Backlog)
                {
                    while (NoteManager.Backlog.Any() && NoteManager.Backlog.First().StartTime <= Stopwatch.ElapsedMilliseconds)
                    {
                        Event ev = NoteManager.Backlog.Dequeue();
                        ev.Method();
                    }
                }
            };

            Loading = 0;
            // Create handles to MIDI devices
            outDevice = new OutputDevice(0);
            sequencer = new Sequencer();
            sequence = new Sequence();

            // Set custom event handlers for sequencer
            sequencer.ChannelMessagePlayed += delegate (object o, ChannelMessageEventArgs args)
            {
                ChannelCommand cmd = args.Message.Command;
                int channel = args.Message.MidiChannel;
                int data1 = args.Message.Data1;
                int data2 = args.Message.Data2;
                if (cmd == ChannelCommand.NoteOff || (cmd == ChannelCommand.NoteOn && data2 == 0))
                {
                    if (NoteManager.LastPlayed[channel, data1] != null)
                    {
                        Note n = NoteManager.LastPlayed[channel, data1];
                        n.Playing = false;
                    }
                }
                else if (cmd == ChannelCommand.NoteOn)
                {
                    Note n = new Note
                    {
                        Key = data1,
                        Length = 0,
                        Playing = true,
                        Position = 0,
                        Time = Stopwatch.ElapsedMilliseconds,
                        Channel = channel,
                        Velocity = data2
                    };
                    lock (NoteManager.Notes)
                        NoteManager.Notes.Add(n);
                    if (NoteManager.LastPlayed[channel, data1] != null)
                        NoteManager.LastPlayed[channel, data1].Playing = false;
                    NoteManager.LastPlayed[channel, data1] = n;
                }

                lock (NoteManager.Backlog)
                {
                    NoteManager.Backlog.Enqueue(new Event(delegate
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
                    }, Stopwatch.ElapsedMilliseconds, Delay));
                }
            };
            sequencer.SysExMessagePlayed += delegate (object o, SysExMessageEventArgs args)
            {
                lock (NoteManager.Backlog)
                    NoteManager.Backlog.Enqueue(new Event(() => outDevice.Send(args.Message), Stopwatch.ElapsedMilliseconds, Delay));
            };
            sequencer.Chased += delegate (object o, ChasedEventArgs args)
            {
                foreach (ChannelMessage message in args.Messages)
                    lock (NoteManager.Backlog)
                        NoteManager.Backlog.Enqueue(new Event(() => outDevice.Send(message), Stopwatch.ElapsedMilliseconds, Delay));
            };
            sequencer.Stopped += delegate (object o, StoppedEventArgs args)
            {
                foreach (ChannelMessage message in args.Messages)
                    lock (NoteManager.Backlog)
                        NoteManager.Backlog.Enqueue(new Event(() => outDevice.Send(message), Stopwatch.ElapsedMilliseconds, Delay));
            };
            sequence.LoadCompleted += delegate (object o, AsyncCompletedEventArgs args)
            {
                Loading = -1;
                if (args.Cancelled)
                {
                    MessageBox.Show("The operation was cancelled.", "MIDITrailer - Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }
                sequencer.Sequence = sequence;
                sequencer.Start();
            };
            sequence.LoadProgressChanged += delegate (object sender, ProgressChangedEventArgs args)
            {
                Loading = args.ProgressPercentage;
            };
            // Begin playing something
            Start();
        }

        public void Load(string file)
        {
            MIDIFile = file;
            sequence.LoadAsync(file);
        }

        public void Reset()
        {
            Keyboard.Reset();
            NoteManager.Reset();
            for (int i = 0; i < 16; i++)
                for (int j = 0; j < 128; j++)
                    outDevice.Send(new ChannelMessage(ChannelCommand.NoteOff, i, j));
        }

        public void Dispose()
        {
            outDevice.Close();
            outDevice.Dispose();
            sequencer.Stop();
            sequencer.Dispose();
            sequence?.Dispose();
        }

        public void Stop()
        {
            if (Stopped)
                return;
            eventTimer.Stop();
            sequencer.Stop();
            Stopwatch.Stop();
            Stopped = true;
        }

        public void Start()
        {
            if (!Stopped)
                return;

            eventTimer.Start();
            if (sequencer.Position > 0)
                sequencer.Continue();
            else
                sequencer.Start();

            Stopwatch.Start();
            Stopped = false;
        }

        public void Render(RenderTarget target)
        {
            // Fill background depending on render mode
            if (NoteManager.RenderFancy)
                target.FillRectangle(GFXResources.BackgroundGradient, new RectangleF(PointF.Empty, target.Size));
            else
                target.Clear(Color.Black);

            // Render notes and keyboard display
            lock (NoteManager.Notes)
            {
                if (NoteManager.RenderFancy)
                    NoteRenderers[1].Render(target, NoteManager.Notes, Keyboard);
                else
                    NoteRenderers[0].Render(target, NoteManager.Notes, Keyboard);
            }
            lock (Keyboard.KeyPressed)
            {
                if (NoteManager.RenderFancy)
                    KeyRenderers[1].Render(target, Keyboard.KeyPressed);
                else
                    KeyRenderers[0].Render(target, Keyboard.KeyPressed);
            }

            // Draw time progress bar
            if (sequence?.GetLength() > 0)
            {
                float percentComplete = 1f * sequencer.Position / sequence.GetLength();
                target.FillRectangle(GFXResources.DefaultBrushes[5],
                    new RectangleF(GFXResources.ProgressBarBounds.X, GFXResources.ProgressBarBounds.Y, GFXResources.ProgressBarBounds.Width * percentComplete, GFXResources.ProgressBarBounds.Height));
                target.DrawRectangle(GFXResources.DefaultBrushes[2], GFXResources.ProgressBarBounds, .8f);
            }

            // Render debug information
            string[] debug;
            string usage = Application.ProductName + " " + Application.ProductVersion + " (c) " + Application.CompanyName;
            if (ShowDebug)
            {
                debug = new[]
                {
                    usage,
                    "      file: " + MIDIFile,
                    "note_count: " + NoteManager.Notes.Count,
                    "  frames/s: " + (Kazedan.Elapsed == 0 ? "NaN" : 1000 / Kazedan.Elapsed + "") +" fps",
                    "  renderer: " + (NoteManager.RenderFancy ? "fancy" : NoteManager.UserEnabledFancy ? "forced-fast" : "fast"),
                    "  seq_tick: " + (sequence == null ? "? / ?" : sequencer.Position + " / " + sequence.GetLength()),
                    "     delay: " + Delay+"ms",
                    "       kbd: " + GFXResources.NoteCount + " key(s) +" + GFXResources.NoteOffset + " offset"
                };

            }
            else
            {
                debug = new[] { usage };
            }
            string debugText = debug.Aggregate("", (current, ss) => current + ss + '\n');
            target.DrawText(debugText, GFXResources.DebugFormat, GFXResources.DebugRectangle, GFXResources.DefaultBrushes[0], DrawTextOptions.None,
                MeasuringMethod.Natural);

            // Render large title text
            if (Loading == 0)
                target.DrawText("INITIALIZING MIDI DEVICES", GFXResources.HugeFormat, GFXResources.FullRectangle, GFXResources.DefaultBrushes[0], DrawTextOptions.None, MeasuringMethod.Natural);
            else if (Loading > 0 && Loading < 100)
                target.DrawText("LOADING " + Loading + "%", GFXResources.HugeFormat, GFXResources.FullRectangle, GFXResources.DefaultBrushes[0], DrawTextOptions.None, MeasuringMethod.Natural);
        }

        public void UpdateNotePositions()
        {
            int keyboardY = GFXResources.Bounds.Height - GFXResources.KeyHeight;
            long now = Stopwatch.ElapsedMilliseconds;
            float speed = 1.0f * keyboardY / Delay;
            // Update all note positions
            lock (NoteManager.Notes)
            {
                for (int i = 0; i < NoteManager.Notes.Count; i++)
                {
                    Note n = NoteManager.Notes[i];
                    if (!n.Playing)
                        n.Position = (now - n.Time) * speed - n.Length;
                    else
                        n.Length = (now - n.Time) * speed;
                    if (n.Position > keyboardY)
                    {
                        NoteManager.Notes.RemoveAt(i);
                        i--;
                    }
                }
            }
        }

        public void UpdateRenderer()
        {
            // Update forced-fast mode
            if (NoteManager.Notes.Count > NoteManager.ForcedFastThreshold)
            {
                LastFancyTick = Stopwatch.ElapsedMilliseconds;
                NoteManager.RenderFancy = false;
            }
            else
            {
                if (NoteManager.UserEnabledFancy)
                    if (Stopwatch.ElapsedMilliseconds - LastFancyTick > NoteManager.ReturnToFancyDelay)
                        NoteManager.RenderFancy = true;
            }
        }

        public static int Get14BitValue(int nLowerPart, int nHigherPart)
        {
            return (nLowerPart & 0x7F) | ((nHigherPart & 0x7F) << 7);
        }
    }
}
