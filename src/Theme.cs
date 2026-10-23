using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace MonitorControl
{
    // Apple-style system colors, swapped to follow the Windows app theme.
    static class Theme
    {
        public static bool IsDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object v = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return v is int && (int)v == 0;
                }
            }
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        public static void Apply(ResourceDictionary r, bool dark)
        {
            //       key               light        dark
            Set(r, "WindowBg",      "#F2F2F7", "#1C1C1E", dark);
            Set(r, "CardBg",        "#FFFFFF", "#2C2C2E", dark);
            Set(r, "Text",          "#1D1D1F", "#F5F5F7", dark);
            Set(r, "TextSecondary", "#86868B", "#98989D", dark);
            Set(r, "Hairline",      "#14000000", "#1FFFFFFF", dark);
            Set(r, "TrackBg",       "#E5E5EA", "#3A3A3C", dark);
            Set(r, "SliderFill",    "#FFFFFF", "#EBEBF0", dark);
            Set(r, "SliderIcon",    "#8E8E93", "#8E8E93", dark);
            Set(r, "KnobShadow",    "#26000000", "#66000000", dark);
            Set(r, "Accent",        "#007AFF", "#0A84FF", dark);
            Set(r, "ChipBg",        "#F2F2F7", "#3A3A3C", dark);
            Set(r, "HoverOverlay",  "#0F000000", "#14FFFFFF", dark);
            Set(r, "SegBg",         "#EEEEF0", "#3A3A3C", dark);
            Set(r, "SegSelected",   "#FFFFFF", "#636366", dark);
            Set(r, "SwitchOff",     "#E9E9EB", "#48484A", dark);
            Set(r, "SwitchOn",      "#34C759", "#30D158", dark);
            Set(r, "Warning",       "#FF9500", "#FF9F0A", dark);
            Set(r, "WarningBg",     "#FFF5E6", "#3A2E1F", dark);
            Set(r, "Danger",        "#FF3B30", "#FF453A", dark);
        }

        static void Set(ResourceDictionary r, string key, string light, string dark, bool isDark)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(isDark ? dark : light));
            brush.Freeze();
            r[key] = brush;
        }
    }
}
