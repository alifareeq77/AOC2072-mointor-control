using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace MonitorControl
{
    static class Icons
    {
        public static readonly Geometry Sun = Make(
            "M17,12 A5,5 0 1 1 7,12 A5,5 0 1 1 17,12 Z M12,1 L12,3 M12,21 L12,23 M4.22,4.22 L5.64,5.64 " +
            "M18.36,18.36 L19.78,19.78 M1,12 L3,12 M21,12 L23,12 M4.22,19.78 L5.64,18.36 M18.36,5.64 L19.78,4.22");
        public static readonly Geometry ContrastRing = Make("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 Z");
        public static readonly Geometry ContrastHalf = Make("M12,3 A9,9 0 0 1 12,21 Z");

        static Geometry Make(string data)
        {
            var g = Geometry.Parse(data);
            g.Freeze();
            return g;
        }
    }

    // A Control Center style slider: a rounded pill whose filled part ends in the knob.
    class PillSlider : FrameworkElement
    {
        public static readonly DependencyProperty RatioProperty = DependencyProperty.Register(
            "Ratio", typeof(double), typeof(PillSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        int level;
        int maximum = 100;

        public Geometry StrokeIcon;
        public Geometry FillIcon;

        // Raised only for changes the user makes, not for SetLevel.
        public event EventHandler ValueChanged;

        public PillSlider()
        {
            Height = 28;
            Cursor = Cursors.Hand;
            Focusable = true;
            FocusVisualStyle = null;
        }

        public int Value { get { return level; } }
        public bool IsDragging { get; private set; }

        public int Maximum
        {
            get { return maximum; }
            set
            {
                maximum = Math.Max(1, value);
                SetLevel(level, false);
            }
        }

        public void SetLevel(int v, bool animate)
        {
            level = Math.Max(0, Math.Min(maximum, v));
            double target = (double)level / maximum;
            if (animate)
            {
                var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(260))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                BeginAnimation(RatioProperty, anim);
            }
            else
            {
                BeginAnimation(RatioProperty, null);
                SetValue(RatioProperty, target);
            }
        }

        void UserSet(int v)
        {
            v = Math.Max(0, Math.Min(maximum, v));
            if (v == level) return;
            SetLevel(v, false);
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        void SetFromPoint(double x)
        {
            double r = ActualHeight / 2;
            double span = ActualWidth - ActualHeight;
            if (span <= 0) return;
            double ratio = Math.Max(0, Math.Min(1, (x - r) / span));
            UserSet((int)Math.Round(ratio * maximum));
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            Focus();
            IsDragging = CaptureMouse();
            SetFromPoint(e.GetPosition(this).X);
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (IsDragging) SetFromPoint(e.GetPosition(this).X);
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            if (IsDragging) ReleaseMouseCapture();
        }

        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            IsDragging = false;
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            int step = Math.Max(1, maximum / 50);
            UserSet(level + (e.Delta > 0 ? step : -step));
            e.Handled = true;
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int big = Math.Max(1, maximum / 10);
            switch (e.Key)
            {
                case Key.Left: case Key.Down: UserSet(level - 1); break;
                case Key.Right: case Key.Up: UserSet(level + 1); break;
                case Key.PageDown: UserSet(level - big); break;
                case Key.PageUp: UserSet(level + big); break;
                case Key.Home: UserSet(0); break;
                case Key.End: UserSet(maximum); break;
                default: return;
            }
            e.Handled = true;
        }

        Brush Res(string key)
        {
            return TryFindResource(key) as Brush ?? Brushes.Gray;
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight, r = h / 2;
            if (w < h) return;
            double fill = h + (double)GetValue(RatioProperty) * (w - h);
            Brush fillBrush = Res("SliderFill");

            dc.DrawRoundedRectangle(Res("TrackBg"), null, new Rect(0, 0, w, h), r, r);
            dc.DrawRoundedRectangle(fillBrush, null, new Rect(0, 0, fill, h), r, r);
            // A soft edge under the end of the fill so the knob reads as its own piece.
            dc.DrawEllipse(Res("KnobShadow"), null, new Point(fill - r + 0.6, r + 0.6), r - 0.5, r - 0.5);
            dc.DrawEllipse(fillBrush, null, new Point(fill - r, r), r - 1, r - 1);
            dc.DrawRoundedRectangle(null, new Pen(Res("Hairline"), 1), new Rect(0.5, 0.5, w - 1, h - 1), r - 0.5, r - 0.5);

            const double size = 14;
            Brush ink = Res("SliderIcon");
            dc.PushTransform(new TranslateTransform(r - size / 2 + 1, r - size / 2));
            dc.PushTransform(new ScaleTransform(size / 24, size / 24));
            if (FillIcon != null) dc.DrawGeometry(ink, null, FillIcon);
            if (StrokeIcon != null)
                dc.DrawGeometry(null, new Pen(ink, 2.2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }, StrokeIcon);
            dc.Pop();
            dc.Pop();
        }
    }
}
