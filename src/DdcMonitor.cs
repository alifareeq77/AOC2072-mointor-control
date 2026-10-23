using System;
using System.Collections.Generic;
using System.Management;
using System.Threading;

namespace MonitorControl
{
    class InputSource
    {
        public readonly string Name;
        public readonly int Code;

        public InputSource(string name, int code)
        {
            Name = name;
            Code = code;
        }
    }

    // One physical monitor, controlled over DDC/CI (VESA MCCS codes).
    class DdcMonitor
    {
        public const byte Brightness = 0x10;
        public const byte Contrast = 0x12;
        public const byte Input = 0x60;

        // Standard MCCS input codes. Monitors that lack a port just ignore that code.
        public static readonly InputSource[] Inputs =
        {
            new InputSource("HDMI 1", 0x11),
            new InputSource("HDMI 2", 0x12),
            new InputSource("DisplayPort", 0x0F),
            new InputSource("VGA", 0x01),
        };

        // PNP manufacturer IDs from the EDID, for a readable brand name.
        static readonly Dictionary<string, string> Brands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "AOC", "AOC" }, { "DEL", "Dell" }, { "GSM", "LG" }, { "SAM", "Samsung" }, { "ACR", "Acer" },
            { "AUS", "ASUS" }, { "BNQ", "BenQ" }, { "HWP", "HP" }, { "HPN", "HP" }, { "LEN", "Lenovo" },
            { "MSI", "MSI" }, { "PHL", "Philips" }, { "VSC", "ViewSonic" }, { "GBT", "Gigabyte" }, { "SNY", "Sony" },
        };

        public IntPtr Handle;
        public string Name;

        // Background polling and slider writes must not talk to the monitor at the same time.
        readonly object sync = new object();

        public override string ToString() { return Name; }

        public static string InputName(int code)
        {
            foreach (var i in Inputs) if (i.Code == code) return i.Name;
            return "Input 0x" + code.ToString("X2");
        }

        public bool TryGet(byte code, out int current, out int max)
        {
            lock (sync)
            {
                uint cur, mx;
                // DDC/CI is flaky on some monitors; retry a couple of times.
                for (int i = 0; i < 3; i++)
                {
                    if (Native.GetVCPFeatureAndVCPFeatureReply(Handle, code, IntPtr.Zero, out cur, out mx))
                    {
                        current = (int)cur;
                        max = (int)mx;
                        return true;
                    }
                    Thread.Sleep(40);
                }
                current = 0;
                max = 100;
                return false;
            }
        }

        public bool Set(byte code, int value)
        {
            lock (sync) return Native.SetVCPFeature(Handle, code, (uint)value);
        }

        public void Release()
        {
            Native.DestroyPhysicalMonitor(Handle);
        }

        public static List<DdcMonitor> Enumerate()
        {
            var names = FriendlyNames();
            var list = new List<DdcMonitor>();
            Native.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMon, hdc, rect, data) =>
            {
                uint count;
                if (!Native.GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out count) || count == 0) return true;
                var physical = new Native.PHYSICAL_MONITOR[count];
                if (!Native.GetPhysicalMonitorsFromHMONITOR(hMon, count, physical)) return true;

                string friendly = null;
                string code = MonitorCode(hMon);
                if (code != null) names.TryGetValue(code, out friendly);

                foreach (var pm in physical)
                {
                    string name = friendly ?? (string.IsNullOrEmpty(pm.Description) ? "Display" : pm.Description);
                    list.Add(new DdcMonitor { Handle = pm.Handle, Name = name });
                }
                return true;
            }, IntPtr.Zero);
            return list;
        }

        // "MONITOR\AOC2702\{...}\0001" -> "AOC2702"
        static string MonitorCode(IntPtr hMon)
        {
            var info = new Native.MONITORINFOEX();
            info.Size = System.Runtime.InteropServices.Marshal.SizeOf(info);
            if (!Native.GetMonitorInfo(hMon, ref info)) return null;
            var dd = new Native.DISPLAY_DEVICE();
            dd.Size = System.Runtime.InteropServices.Marshal.SizeOf(dd);
            if (!Native.EnumDisplayDevices(info.Device, 0, ref dd, 0) || dd.DeviceID == null) return null;
            var parts = dd.DeviceID.Split('\\');
            return parts.Length > 1 ? parts[1] : null;
        }

        // The EDID model name ("AOC 27G2G3") from WMI, keyed by the monitor code.
        static Dictionary<string, string> FriendlyNames()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorID"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var parts = ((string)mo["InstanceName"]).Split('\\');
                        if (parts.Length < 2) continue;
                        string maker = Decode(mo["ManufacturerName"] as ushort[]);
                        string model = Decode(mo["UserFriendlyName"] as ushort[]);
                        if (model.Length == 0) continue;
                        string brand;
                        if (!Brands.TryGetValue(maker, out brand)) brand = maker;
                        names[parts[1]] = brand.Length == 0 || model.StartsWith(brand, StringComparison.OrdinalIgnoreCase)
                            ? model
                            : brand + " " + model;
                    }
                }
            }
            catch (ManagementException) { }
            return names;
        }

        static string Decode(ushort[] chars)
        {
            if (chars == null) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var c in chars)
            {
                if (c == 0) break;
                sb.Append((char)c);
            }
            return sb.ToString().Trim();
        }
    }
}
