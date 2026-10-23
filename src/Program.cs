using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using Microsoft.Win32;
using SD = System.Drawing;
using WF = System.Windows.Forms;

namespace MonitorControl
{
    public static class Program
    {
        public const string Version = "1.0.0";

        // Set by the PowerShell launcher ("Monitor Control.ps1"), which compiles and runs this
        // code in memory on PCs where Smart App Control blocks the unsigned .exe.
        public static string BaseDirectory;
        public static string LaunchCommand;

        [STAThread]
        public static void Main(string[] args)
        {
            // The .exe gets this from its manifest; under PowerShell it has to be set here.
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch (EntryPointNotFoundException) { }

            bool created;
            // Names differ from the old PowerShell prototype's, so a leftover copy of it can't block this app.
            using (var mutex = new Mutex(true, @"Local\MonitorControl.App.SingleInstance", out created))
            using (var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\MonitorControl.App.Show"))
            {
                // A second launch just opens the panel of the one already running.
                if (!created)
                {
                    showSignal.Set();
                    return;
                }

                WF.Application.EnableVisualStyles();
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.DispatcherUnhandledException += (s, e) => { Log(e.Exception); e.Handled = true; };
                AppDomain.CurrentDomain.UnhandledException += (s, e) => Log(e.ExceptionObject as Exception);

                LoginItem.Repair();
                var tray = new TrayApp(args == null || Array.IndexOf(args, "--tray") < 0);

                var listener = new Thread(() =>
                {
                    while (showSignal.WaitOne())
                        app.Dispatcher.BeginInvoke(new Action(tray.ShowPanel));
                }) { IsBackground = true };
                listener.Start();

                app.Run();
            }
        }

        // Embedded in the .exe; next to the sources when run by the launcher.
        public static Stream OpenResource(string name)
        {
            var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MonitorControl." + name);
            if (stream != null) return stream;
            return File.OpenRead(Path.Combine(BaseDirectory ?? AppDomain.CurrentDomain.BaseDirectory, name));
        }

        public static void Log(Exception ex)
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonitorControl");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "error.log"), DateTime.Now.ToString("s") + "  " + ex + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    // "Open at Login" via the per-user Run key, so no admin rights are needed.
    static class LoginItem
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "MonitorControl";

        static string Command
        {
            get { return Program.LaunchCommand ?? "\"" + Assembly.GetExecutingAssembly().Location + "\" --tray"; }
        }

        public static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && key.GetValue(Name) != null;
        }

        public static void Set(bool enable)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (enable) key.SetValue(Name, Command);
                else key.DeleteValue(Name, false);
            }
        }

        // The app is portable: if its folder moved, point the login entry at the new location.
        public static void Repair()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
            {
                var value = key == null ? null : key.GetValue(Name) as string;
                if (value == null || value == Command) return;
            }
            Set(true);
        }
    }

    class TrayApp
    {
        readonly PanelWindow panel;
        readonly WF.NotifyIcon icon;
        readonly WF.ToolStripMenuItem loginItem;
        readonly List<WF.ToolStripMenuItem> inputItems = new List<WF.ToolStripMenuItem>();

        public TrayApp(bool showNow)
        {
            panel = new PanelWindow();
            panel.QuitRequested += (s, e) => Quit();

            var menu = new WF.ContextMenuStrip();
            menu.Items.Add("Open Monitor Control", null, (s, e) => panel.ShowPanel());
            menu.Items.Add(new WF.ToolStripSeparator());

            var brightness = new WF.ToolStripMenuItem("Brightness");
            foreach (var p in PanelWindow.Presets)
            {
                int percent = p.Percent;
                brightness.DropDownItems.Add(p.Name + " (" + p.Percent + "%)", null, (s, e) => panel.ApplyPreset(percent));
            }
            menu.Items.Add(brightness);

            var input = new WF.ToolStripMenuItem("Input");
            foreach (var src in DdcMonitor.Inputs)
            {
                int code = src.Code;
                var item = new WF.ToolStripMenuItem(src.Name, null, (s, e) => panel.RequestInput(code)) { Tag = code };
                inputItems.Add(item);
                input.DropDownItems.Add(item);
            }
            menu.Items.Add(input);
            menu.Items.Add(new WF.ToolStripSeparator());

            loginItem = new WF.ToolStripMenuItem("Open at Login", null, (s, e) =>
            {
                LoginItem.Set(!LoginItem.IsEnabled());
                panel.SyncLogin();
            });
            menu.Items.Add(loginItem);
            menu.Items.Add(new WF.ToolStripSeparator());
            menu.Items.Add("Quit", null, (s, e) => Quit());

            menu.Opening += (s, e) =>
            {
                foreach (var item in inputItems) item.Checked = (int)item.Tag == panel.CurrentInput;
                loginItem.Checked = LoginItem.IsEnabled();
            };

            icon = new WF.NotifyIcon { Icon = LoadIcon(), Text = "Monitor Control", ContextMenuStrip = menu, Visible = true };
            icon.MouseClick += (s, e) => { if (e.Button == WF.MouseButtons.Left) panel.TogglePanel(); };

            SystemEvents.UserPreferenceChanged += OnPreferenceChanged;

            if (showNow) panel.ShowPanel();
        }

        public void ShowPanel()
        {
            panel.ShowPanel();
        }

        static SD.Icon LoadIcon()
        {
            using (var stream = Program.OpenResource("app.ico"))
                return new SD.Icon(stream, WF.SystemInformation.SmallIconSize);
        }

        // Follow Windows when it switches between light and dark mode.
        void OnPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            panel.Dispatcher.BeginInvoke(new Action(panel.ApplyTheme));
        }

        void Quit()
        {
            SystemEvents.UserPreferenceChanged -= OnPreferenceChanged;
            icon.Visible = false;
            icon.Dispose();
            panel.Shutdown();
            Application.Current.Shutdown();
        }
    }
}
