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

    /// Learns how long a full charge takes, from charges whose level is known before plugging the
    /// cable in and right after pulling it out (the device sends no level while on the cable).
    class ChargeModel
    {
        readonly string device;
        /// Minutes for 0 -> 100 %; 0 = nothing learned yet.
        public double FullMinutes;
        /// True when measured from a charge that ended below 100 %; false when only an upper
        /// bound is known (the charge reached 100 % at some unknown moment before unplugging).
        public bool Exact;

        public ChargeModel(string device)
        {
            this.device = device;
            FullMinutes = Store.GetDouble(device, "ChargeFullMinutes", 0);
            Exact = Store.GetInt(device, "ChargeFullExact", 0) == 1;
        }

        /// One charge: level before, level after, minutes on the cable. Returns true if it taught something.
        public bool Learn(int before, int after, double minutes)
        {
            if (before < 0 || after <= before || minutes < 3) return false;
            if (after < 100)
            {
                double full = minutes * 100.0 / (after - before);
                FullMinutes = Exact && FullMinutes > 0 ? (FullMinutes + full) / 2 : full;
                Exact = true;
            }
            else
            {
                double bound = minutes * 100.0 / (100 - before);
                if (FullMinutes <= 0 || bound < FullMinutes) FullMinutes = bound;
                else return false;
            }
            Store.Set(device, "ChargeFullMinutes", Math.Round(FullMinutes, 1));
            Store.Set(device, "ChargeFullExact", Exact ? 1 : 0);
            return true;
        }

        /// Minutes still needed when the charge started at `before` % (unknown: -1) and has run `elapsed` minutes.
        public double MinutesLeft(int before, double elapsed, double fallbackFull)
        {
            double full = FullMinutes > 0 ? FullMinutes : fallbackFull;
            return (100 - Math.Max(0, before)) / 100.0 * full - elapsed;
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
