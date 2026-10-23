using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Threading;

namespace MonitorControl
{
    class Preset
    {
        public readonly string Name;
        public readonly int Percent;

        public Preset(string name, int percent)
        {
            Name = name;
            Percent = percent;
        }
    }

    // The popover that opens from the tray icon.
    class PanelWindow : Window
    {
        public static readonly Preset[] Presets =
        {
            new Preset("Night", 10),
            new Preset("Evening", 30),
            new Preset("Day", 60),
            new Preset("Max", 100),
        };

        static readonly IEasingFunction EaseOut = new CubicEase { EasingMode = EasingMode.EaseOut };

        readonly FrameworkElement root;
        readonly TranslateTransform rootShift = new TranslateTransform();
        readonly TextBlock monitorName, brightnessText, contrastText, inputText, statusText, confirmText;
        readonly Button monitorButton;
        readonly FrameworkElement monitorChevron;
        readonly Border segIndicator, confirmPanel, shadowCard;
        readonly Grid segGrid;
        readonly CheckBox loginSwitch;
        readonly PillSlider brightness, contrast;
        readonly List<Button> presetButtons = new List<Button>();
        readonly List<Button> inputButtons = new List<Button>();
        readonly DispatcherTimer pollTimer;

        List<DdcMonitor> monitors = new List<DdcMonitor>();
        DdcMonitor current;
        int currentInput = -1;
        int confirmTarget = -1;
        bool polling;
        bool syncingLogin;
        bool anchoredBottom = true;
        bool shuttingDown;
        DateTime hiddenAt = DateTime.MinValue;

        // Slider writes: the newest value wins, and only one write runs at a time.
        readonly object writeLock = new object();
        int? wantBrightness, wantContrast;
        bool writerBusy;

        public event EventHandler QuitRequested;

        public int CurrentInput { get { return currentInput; } }

        public PanelWindow()
        {
            Title = "Monitor Control";
            Width = 400;
            SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            UseLayoutRounding = true;

            using (var stream = Program.OpenResource("MainWindow.xaml"))
                root = (FrameworkElement)XamlReader.Load(stream);
            root.RenderTransform = rootShift;
            Content = root;

            monitorName = Find<TextBlock>("MonitorName");
            monitorButton = Find<Button>("MonitorButton");
            monitorChevron = Find<FrameworkElement>("MonitorChevron");
            brightnessText = Find<TextBlock>("BrightnessText");
            contrastText = Find<TextBlock>("ContrastText");
            inputText = Find<TextBlock>("InputText");
            statusText = Find<TextBlock>("StatusText");
            confirmText = Find<TextBlock>("ConfirmText");
            segIndicator = Find<Border>("SegIndicator");
            confirmPanel = Find<Border>("ConfirmPanel");
            shadowCard = Find<Border>("ShadowCard");
            segGrid = Find<Grid>("SegGrid");
            loginSwitch = Find<CheckBox>("LoginSwitch");

            brightness = new PillSlider { StrokeIcon = Icons.Sun };
            contrast = new PillSlider { StrokeIcon = Icons.ContrastRing, FillIcon = Icons.ContrastHalf };
            Find<ContentControl>("BrightnessHost").Content = brightness;
            Find<ContentControl>("ContrastHost").Content = contrast;
            brightness.ValueChanged += (s, e) => { QueueWrite(DdcMonitor.Brightness, brightness.Value); UpdateLabels(); };
            contrast.ValueChanged += (s, e) => { QueueWrite(DdcMonitor.Contrast, contrast.Value); UpdateLabels(); };

            var chipStyle = (Style)root.FindResource("Chip");
            var presetGrid = Find<UniformGrid>("PresetGrid");
            foreach (var p in Presets)
            {
                var preset = p;
                var b = new Button { Content = p.Name, Style = chipStyle, Margin = new Thickness(3, 0, 3, 0) };
                b.Click += (s, e) => ApplyPreset(preset.Percent);
                presetButtons.Add(b);
                presetGrid.Children.Add(b);
            }

            var segStyle = (Style)root.FindResource("Segment");
            var segButtons = Find<UniformGrid>("SegButtons");
            foreach (var src in DdcMonitor.Inputs)
            {
                int code = src.Code;
                var b = new Button { Content = src.Name, Style = segStyle };
                b.Click += (s, e) => RequestInput(code);
                inputButtons.Add(b);
                segButtons.Children.Add(b);
            }
            segGrid.SizeChanged += (s, e) => PositionIndicator(false);

            Find<Button>("ConfirmCancel").Click += (s, e) => HideConfirm();
            Find<Button>("ConfirmOk").Click += (s, e) => { int code = confirmTarget; HideConfirm(); DoSwitch(code); };
            Find<Button>("CloseButton").Click += (s, e) => HidePanel();
            Find<Button>("QuitButton").Click += (s, e) => { if (QuitRequested != null) QuitRequested(this, EventArgs.Empty); };
            monitorButton.Click += (s, e) => ShowMonitorMenu();

            loginSwitch.Checked += (s, e) => { if (!syncingLogin) LoginItem.Set(true); };
            loginSwitch.Unchecked += (s, e) => { if (!syncingLogin) LoginItem.Set(false); };
            SyncLogin();

            Deactivated += (s, e) => HidePanel();
            PreviewKeyDown += (s, e) => { if (e.Key == Key.Escape) HidePanel(); };
            SizeChanged += (s, e) => { if (IsVisible) Reposition(); };

            // Picks up input changes made elsewhere, e.g. with the monitor's own buttons.
            pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            pollTimer.Tick += (s, e) => PollInput();
            pollTimer.Start();

            ApplyTheme();
            SetStatus(null, false);
            LoadMonitors();
        }

        T Find<T>(string name) where T : class
        {
            return (T)root.FindName(name);
        }

        // ---- Showing and hiding ----

        public void ShowPanel()
        {
            if (IsVisible)
            {
                Activate();
                return;
            }
            BeginAnimation(OpacityProperty, null);
            Opacity = 0;
            Show();
            UpdateLayout();
            Reposition();
            Activate();

            BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140)));
            rootShift.BeginAnimation(TranslateTransform.YProperty,
                new DoubleAnimation(anchoredBottom ? 10 : -10, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = EaseOut });
            RefreshValues();
        }

        public void HidePanel()
        {
            if (!IsVisible) return;
            HideConfirm();
            Hide();
            hiddenAt = DateTime.Now;
        }

        public void TogglePanel()
        {
            // Clicking the tray icon deactivates (and so hides) the panel first; don't reopen it.
            if (IsVisible) HidePanel();
            else if ((DateTime.Now - hiddenAt).TotalMilliseconds > 300) ShowPanel();
        }

        // Next to the tray: bottom-right, or top-right when the taskbar is at the top.
        void Reposition()
        {
            var wa = SystemParameters.WorkArea;
            anchoredBottom = wa.Top <= 0;
            Left = wa.Right - ActualWidth + 6;
            Top = anchoredBottom ? wa.Bottom - ActualHeight + 6 : wa.Top - 6;
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Alt+F4 only hides the panel; the app keeps running in the tray.
            if (!shuttingDown)
            {
                e.Cancel = true;
                HidePanel();
            }
            base.OnClosing(e);
        }

        public void Shutdown()
        {
            shuttingDown = true;
            pollTimer.Stop();
            foreach (var m in monitors) m.Release();
            Close();
        }

        public void ApplyTheme()
        {
            bool dark = Theme.IsDark();
            Theme.Apply(Resources, dark);
            var shadow = shadowCard.Effect as DropShadowEffect;
            if (shadow != null) shadow.Opacity = dark ? 0.55 : 0.2;
            brightness.InvalidateVisual();
            contrast.InvalidateVisual();
        }

        // ---- Monitors ----

        void LoadMonitors()
        {
            monitors = DdcMonitor.Enumerate();
            current = monitors.Count > 0 ? monitors[0] : null;
            monitorChevron.Visibility = monitors.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            monitorButton.IsHitTestVisible = monitors.Count > 1;
            monitorName.Text = current != null ? current.Name : "No monitor found";
            brightness.IsEnabled = contrast.IsEnabled = current != null;
            if (current == null) SetStatus("No monitor with DDC/CI was found.", true);
        }

        void ShowMonitorMenu()
        {
            var menu = new ContextMenu { PlacementTarget = monitorButton, Placement = PlacementMode.Bottom };
            foreach (var m in monitors)
            {
                var monitor = m;
                var item = new MenuItem { Header = m.Name, IsCheckable = true, IsChecked = m == current };
                item.Click += (s, e) => SelectMonitor(monitor);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        void SelectMonitor(DdcMonitor m)
        {
            current = m;
            monitorName.Text = m.Name;
            currentInput = -1;
            PositionIndicator(false);
            RefreshValues();
        }

        // Reads everything off the UI thread; DDC/CI replies take tens of milliseconds each.
        void RefreshValues()
        {
            var m = current;
            if (m == null) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int b, bMax, c, cMax, input, iMax;
                bool okB = m.TryGet(DdcMonitor.Brightness, out b, out bMax);
                bool okC = m.TryGet(DdcMonitor.Contrast, out c, out cMax);
                bool okI = m.TryGet(DdcMonitor.Input, out input, out iMax);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (m != current) return;
                    bool idle = !writerBusy;
                    if (okB && idle && !brightness.IsDragging)
                    {
                        brightness.Maximum = bMax > 0 ? bMax : 100;
                        brightness.SetLevel(b, true);
                    }
                    if (okC && idle && !contrast.IsDragging)
                    {
                        contrast.Maximum = cMax > 0 ? cMax : 100;
                        contrast.SetLevel(c, true);
                    }
                    // Some monitors put extra data in the high byte; the input code is the low byte.
                    if (okI) SetCurrentInput(input & 0xFF, true);
                    UpdateLabels();
                    if (okB || okC) SetStatus(null, false);
                    else SetStatus("Can't reach the monitor. Turn on DDC/CI in its menu.", true);
                }));
            });
        }

        void PollInput()
        {
            var m = current;
            if (m == null || polling) return;
            polling = true;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                int input, max;
                bool ok = m.TryGet(DdcMonitor.Input, out input, out max);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    polling = false;
                    // Reads often fail while another input is showing; keep the last known value.
                    if (ok && m == current) SetCurrentInput(input & 0xFF, true);
                }));
            });
        }

        // ---- Brightness and contrast ----

        public void ApplyPreset(int percent)
        {
            int v = (int)Math.Round(percent * brightness.Maximum / 100.0);
            brightness.SetLevel(v, true);
            QueueWrite(DdcMonitor.Brightness, v);
            UpdateLabels();
        }

        void QueueWrite(byte code, int value)
        {
            var m = current;
            if (m == null) return;
            lock (writeLock)
            {
                if (code == DdcMonitor.Brightness) wantBrightness = value;
                else wantContrast = value;
                if (writerBusy) return;
                writerBusy = true;
            }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                while (true)
                {
                    int? b, c;
                    lock (writeLock)
                    {
                        b = wantBrightness;
                        c = wantContrast;
                        wantBrightness = wantContrast = null;
                        if (b == null && c == null)
                        {
                            writerBusy = false;
                            return;
                        }
                    }
                    bool ok = true;
                    if (b.HasValue) ok &= m.Set(DdcMonitor.Brightness, b.Value);
                    if (c.HasValue) ok &= m.Set(DdcMonitor.Contrast, c.Value);
                    if (!ok) Dispatcher.BeginInvoke(new Action(() => SetStatus("The monitor didn't accept the change.", true)));
                }
            });
        }

        void UpdateLabels()
        {
            brightnessText.Text = Percent(brightness) + "%";
            contrastText.Text = Percent(contrast) + "%";
            for (int i = 0; i < Presets.Length; i++)
                presetButtons[i].Tag = Percent(brightness) == Presets[i].Percent ? "selected" : null;
        }

        static int Percent(PillSlider s)
        {
            return (int)Math.Round(s.Value * 100.0 / s.Maximum);
        }

        // ---- Input source ----

        public void RequestInput(int code)
        {
            if (current == null || code == currentInput) return;
            if (currentInput < 0)
            {
                DoSwitch(code);
                return;
            }
            // Leaving the input this PC is on blanks the screen for this PC, so ask first.
            ShowPanel();
            confirmTarget = code;
            confirmText.Text = "Switch to " + DdcMonitor.InputName(code) + "? This PC is connected through " +
                DdcMonitor.InputName(currentInput) + ", so its picture will go dark until the monitor is switched back.";
            confirmPanel.Visibility = Visibility.Visible;
        }

        void HideConfirm()
        {
            confirmPanel.Visibility = Visibility.Collapsed;
            confirmTarget = -1;
        }

        void DoSwitch(int code)
        {
            var m = current;
            if (m == null || code < 0) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                bool ok = m.Set(DdcMonitor.Input, code);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (ok) SetCurrentInput(code, true);
                    else SetStatus("The monitor didn't accept the input change.", true);
                }));
            });
        }

        void SetCurrentInput(int code, bool animate)
        {
            if (code == currentInput) return;
            currentInput = code;
            inputText.Text = DdcMonitor.InputName(code);
            PositionIndicator(animate);
        }

        void PositionIndicator(bool animate)
        {
            int index = Array.FindIndex(DdcMonitor.Inputs, i => i.Code == currentInput);
            for (int i = 0; i < inputButtons.Count; i++)
                inputButtons[i].FontWeight = i == index ? FontWeights.SemiBold : FontWeights.Normal;
            if (index < 0 || segGrid.ActualWidth <= 0)
            {
                segIndicator.Visibility = Visibility.Hidden;
                return;
            }

            double width = segGrid.ActualWidth / inputButtons.Count;
            segIndicator.Width = width;
            var shift = (TranslateTransform)segIndicator.RenderTransform;
            // Don't slide in from the left edge the first time it appears.
            bool slide = animate && segIndicator.Visibility == Visibility.Visible;
            segIndicator.Visibility = Visibility.Visible;
            if (slide)
                shift.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(index * width, TimeSpan.FromMilliseconds(240)) { EasingFunction = EaseOut });
            else
            {
                shift.BeginAnimation(TranslateTransform.XProperty, null);
                shift.X = index * width;
            }
        }

        // ---- Settings and status ----

        public void SyncLogin()
        {
            syncingLogin = true;
            loginSwitch.IsChecked = LoginItem.IsEnabled();
            syncingLogin = false;
        }

        void SetStatus(string text, bool error)
        {
            statusText.Text = text ?? "Monitor Control " + Program.Version;
            statusText.SetResourceReference(TextBlock.ForegroundProperty, error ? "Danger" : "TextSecondary");
        }
    }
}
