using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace PinTray
{
    /// Settings and saved state in HKCU\Software\PinTray (per device under Devices\<key>),
    /// history in %LOCALAPPDATA%\PinTray\history.csv.
    static class Store
    {
        public const string RootKey = @"Software\PinTray";
        public static readonly string Dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PinTray");
        public static readonly string History = Path.Combine(Dir, "history.csv");
        static readonly object logLock = new object();

        static string KeyOf(string device)
        {
            return device == null ? RootKey : RootKey + @"\Devices\" + device;
        }

        public static string Get(string device, string name, string fallback)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(KeyOf(device)))
                {
                    object v = k == null ? null : k.GetValue(name);
                    return v == null ? fallback : v.ToString();
                }
            }
            catch (Exception) { return fallback; }
        }

        public static int GetInt(string device, string name, int fallback)
        {
            int v;
            return int.TryParse(Get(device, name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public static long GetLong(string device, string name, long fallback)
        {
            long v;
            return long.TryParse(Get(device, name, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public static double GetDouble(string device, string name, double fallback)
        {
            double v;
            return double.TryParse(Get(device, name, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        public static void Set(string device, string name, object value)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(KeyOf(device)))
                    k.SetValue(name, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
            catch (Exception) { }
        }

        public static void DeleteDevice(string device)
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(KeyOf(device), false); } catch (Exception) { }
        }

        static string Csv(string s)
        {
            return s.IndexOfAny(new[] { ',', '"' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
        }

        /// Appends one line to the history (UTF-8 with BOM so Excel shows Vietnamese correctly).
        public static void Log(string device, string evt, int percent)
        {
            lock (logLock)
            {
                try
                {
                    Directory.CreateDirectory(Dir);
                    bool fresh = !File.Exists(History);
                    using (var w = new StreamWriter(History, true, new UTF8Encoding(true)))
                    {
                        if (fresh) w.WriteLine("thoi_gian,thiet_bi,su_kien,phan_tram");
                        w.WriteLine("{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3}", DateTime.Now, Csv(device), evt,
                            percent < 0 ? "" : percent.ToString(CultureInfo.InvariantCulture));
                    }
                }
                catch (Exception) { }
            }
        }
    }

    /// Estimates the time left from how long each level step took while the device was awake.
    /// Devices that report in steps of 10 % give a rate only after the level dropped twice.
    class Estimator
    {
        readonly string device;
        /// Seconds the device was awake, counted since PinTray first saw it.
        public double AwakeSeconds;
        readonly List<KeyValuePair<double, int>> drops = new List<KeyValuePair<double, int>>();

        public Estimator(string device) { this.device = device; }

        public void Load()
        {
            AwakeSeconds = Store.GetDouble(device, "AwakeSeconds", 0);
            drops.Clear();
            foreach (string part in Store.Get(device, "Drops", "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] f = part.Split(':');
                double a; int p;
                if (f.Length == 2 && double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a)
                    && int.TryParse(f[1], out p))
                    drops.Add(new KeyValuePair<double, int>(a, p));
            }
        }

        public void Save()
        {
            Store.Set(device, "AwakeSeconds", Math.Round(AwakeSeconds));
            var sb = new StringBuilder();
            foreach (var d in drops)
                sb.Append(Math.Round(d.Key).ToString(CultureInfo.InvariantCulture)).Append(':').Append(d.Value).Append(';');
            Store.Set(device, "Drops", sb.ToString());
        }

        /// Starts over (after charging, or after a gap with an unknown history).
        public void Reset() { if (drops.Count > 0) { drops.Clear(); Save(); } }

        public void Dropped(int percent)
        {
            drops.Add(new KeyValuePair<double, int>(AwakeSeconds, percent));
            if (drops.Count > 12) drops.RemoveAt(0);
            Save();
        }

        /// Awake hours left, or a negative value when there is not enough data yet.
        public double HoursLeft(int percent)
        {
            if (drops.Count < 2) return -1;
            var first = drops[0];
            var last = drops[drops.Count - 1];
            double used = first.Value - last.Value;
            double secs = last.Key - first.Key;
            if (used <= 0 || secs < 60) return -1;
            return percent / (used / secs) / 3600.0;
        }
    }
}
