using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SlimDX;
using SlimDX.Direct2D;
using SlimDX.DirectWrite;
using Brush = SlimDX.Direct2D.Brush;
using Factory = SlimDX.DirectWrite.Factory;

namespace MIDITrailer
{
    class GFXResources
    {
        public static readonly Size Bounds = new Size(1280, 720);
        public static int KEY_HEIGHT = 80;
        public static int BLACK_KEY_HEIGHT = 40;
        public static int NoteOffset = 21;
        public static int NoteCount = 88;

        public static readonly bool[] IsBlack = { false, true, false, true, false, false, true, false, true, false, true, false };
        public static float KeyboardY => Bounds.Height - KEY_HEIGHT;
        public static float KeyWidth => 1.0f * Bounds.Width / NoteCount;

        public static readonly Color[] ChannelColors = {
            Color.Red,          Color.HotPink,      Color.Yellow,
            Color.Green,        Color.Blue,         Color.Indigo,
            Color.SteelBlue,    Color.Pink,         Color.OrangeRed,
            Color.GreenYellow,  Color.Lime,         Color.Cyan,
            Color.Purple,       Color.DarkViolet,   Color.Bisque,
            Color.Brown
        };
        public static readonly Color[] DefaultColors = {
            Color.White,        Color.Black,        Color.FromArgb(30, 30, 30),
            Color.IndianRed,    Color.Red,          Color.FromArgb(100, 10, 200, 10)
        };

        public static Brush[] ChannelBrushes;
        public static Brush[] DefaultBrushes;
        public static LinearGradientBrush[] ChannelGradientBrushes;

        public static LinearGradientBrush KeyboardGradient;
        public static LinearGradientBrush BackgroundGradient;

        public static TextFormat DebugFormat;
        public static TextFormat SmallFormat;
        public static TextFormat HugeFormat;

        public static readonly RectangleF DebugRectangle = new RectangleF(10, 10, 500, 0);
        public static readonly RectangleF FullRectangle = new RectangleF(0, 0, Bounds.Width, Bounds.Height);
        public static RoundedRectangle NoteRoundRect = new RoundedRectangle
        {
            RadiusX = 3,
            RadiusY = 3
        };
        public static RectangleF NoteRect;
        public static PointF GradientPoint;
        public static readonly RectangleF ProgressBarBounds =
            new RectangleF(20 + DebugRectangle.X + DebugRectangle.Width, 20,
                Bounds.Width - 40 - DebugRectangle.X - DebugRectangle.Width, 20);

        public static void Init(RenderTarget renderTarget)
        {
            // Generate common brushes
            DefaultBrushes = new Brush[DefaultColors.Length];
            for (int i = 0; i < DefaultColors.Length; i++)
                DefaultBrushes[i] = new SolidColorBrush(renderTarget, DefaultColors[i]);
            ChannelBrushes = new Brush[ChannelColors.Length];
            for (int i = 0; i < ChannelColors.Length; i++)
                ChannelBrushes[i] = new SolidColorBrush(renderTarget, ChannelColors[i]);

            // Generate common gradients
            KeyboardGradient = new LinearGradientBrush(renderTarget,
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
            BackgroundGradient = new LinearGradientBrush(renderTarget,
                new GradientStopCollection(renderTarget, new[] {
                    new GradientStop()
                    { Color = Color.Black, Position = 1f },
                    new GradientStop()
                    { Color = Color.FromArgb(30, 30, 30), Position = 0f }
                }),
                new LinearGradientBrushProperties()
                {
                    StartPoint = new PointF(0, renderTarget.Size.Height),
                    EndPoint = new PointF(0, 0)
                });
            ChannelGradientBrushes = new LinearGradientBrush[ChannelColors.Length];
            for (int i = 0; i < ChannelGradientBrushes.Length; i++)
            {
                ChannelGradientBrushes[i] = new LinearGradientBrush(renderTarget,
                new GradientStopCollection(renderTarget, new[] {
                    new GradientStop()
                    { Color = ChannelColors[i], Position = 1f },
                    new GradientStop()
                    { Color = ControlPaint.Light(ChannelColors[i], .8f), Position = 0f }
                }),
                new LinearGradientBrushProperties()
                {
                    StartPoint = new PointF(0, renderTarget.Size.Height),
                    EndPoint = new PointF(0, 0)
                });
            }
            // Generate common fonts
            using (var textFactory = new Factory())
            {
                DebugFormat = new TextFormat(textFactory, "Consolas", FontWeight.Bold,
                    SlimDX.DirectWrite.FontStyle.Normal, FontStretch.Normal, 12, "en-us");
                SmallFormat = new TextFormat(textFactory, "Consolas", FontWeight.UltraBold,
                    SlimDX.DirectWrite.FontStyle.Normal, FontStretch.Normal, 10, "en-us");
                HugeFormat = new TextFormat(textFactory, "Consolas", FontWeight.UltraBold,
                   SlimDX.DirectWrite.FontStyle.Normal, FontStretch.Normal, 50, "en-us")
                {
                    TextAlignment = TextAlignment.Center,
                    ParagraphAlignment = ParagraphAlignment.Center
                };
            }
        }
    }
}
