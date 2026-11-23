using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace PinTray
{
    /// Attack Shark mice on the Xenta 2.4 GHz receiver (VID 1D57, PID FA60). Tested with the R1.
    ///
    /// The receiver's interface 2, HID usage page 000A, 5-byte input report, pushes a frame about
    /// every 2 s while the mouse is awake. Nothing is ever sent to the device.
    ///
    ///   03 MM 40 ST NN
    ///   03  report ID
    ///   MM  model marker (10 on the R1)
    ///   40  battery frame
    ///   ST  01 on battery, 02 on power, 03 charging
    ///   NN  level; the R1 reports 0..10 (08 = 80 %), other models 0..100
    ///
    /// On its cable the mouse shows up as 1D57:FA61 ("USB Gaming Mouse"), the receiver goes silent
    /// and no level is sent at all.
    ///
    /// Sometimes the receiver stops sending frames although the mouse works; unplugging and
    /// plugging it back in fixes that. The UI detects it through RawInputMatch.
    class AttackSharkDriver : IDriver
    {
        const string Key = "attackshark-fa60";
        const string VendorApp = @"C:\Program Files (x86)\Attack SharkR1Mouse\Mouse.exe";

        readonly object sync = new object();
        Reading last;
        bool haveReading;
        int marker = -1;
        DateTime lastFrame = DateTime.MinValue;
        bool present, wired, everSeen;

        public void Start()
        {
            var t = new Thread(ReceiverLoop);
            t.IsBackground = true;
            t.Start();
            var w = new Thread(WiredLoop);
            w.IsBackground = true;
            w.Start();
        }

        public List<DeviceStatus> Snapshot()
        {
            var list = new List<DeviceStatus>();
            lock (sync)
            {
                if (!everSeen) return list;
                var s = new DeviceStatus();
                s.Key = Key;
                s.Name = ModelName(marker);
                s.Kind = DeviceKind.Mouse;
                s.Present = present || wired;
                s.Wired = wired;
                s.HasReading = haveReading;
                s.Last = last;
                s.LastFrame = lastFrame;
                s.RawInputMatch = "VID_1D57&PID_FA60";
                s.VendorApp = File.Exists(VendorApp) ? VendorApp : null;
                list.Add(s);
            }
            return list;
        }

        public static string ModelName(int marker)
        {
            switch (marker)
            {
                case 0x10: return "Attack Shark R1";
                case 0x55: return "Attack Shark X11";
                case 0xE5: return "Attack Shark X1";
                default: return "Attack Shark";
            }
        }

        /// Decodes one input report (report ID included).
        public static bool Decode(byte[] f, int n, out Reading r, out int model)
        {
            r = new Reading();
            model = -1;
            if (n < 5 || f[0] != 0x03 || f[2] != 0x40) return false;
            int raw = f[4];
            if (raw > 100) return false;
            model = f[1];
            // The R1 counts in tens; other models send a plain percentage.
            r.Percent = (f[1] == 0x10 && raw <= 10) ? raw * 10 : raw;
            r.State = f[3];
            return true;
        }

        static string FindReceiver()
        {
            foreach (var c in Hid.Find(@"HID\\VID_1D57&PID_FA60&MI_02%"))
                if (c.UsagePage == 0x000A && c.InLen >= 5) return c.Path;
            return null;
        }

        static string FindWired()
        {
            foreach (var c in Hid.Find(@"HID\\VID_1D57&PID_FA61%")) return c.Path;
            return null;
        }

        void WiredLoop()
        {
            string path = null;
            while (true)
            {
                // Opening a known path is cheap; the WMI search runs only while the cable is out.
                if (path != null && !Hid.CanOpen(path)) path = null;
                if (path == null) path = FindWired();
                lock (sync)
                {
                    wired = path != null;
                    if (wired) everSeen = true;
                }
                Thread.Sleep(2000);
            }
        }

        void ReceiverLoop()
        {
            while (true)
            {
                string path = FindReceiver();
                lock (sync)
                {
                    present = path != null;
                    if (present) everSeen = true;
                }
                if (path == null) { Thread.Sleep(3000); continue; }

                using (var h = Hid.OpenRead(path, false))
                {
                    if (h.IsInvalid) { Thread.Sleep(3000); continue; }
                    try
                    {
                        using (var fs = new FileStream(h, FileAccess.Read, 64, false))
                        {
                            var buf = new byte[64];
                            bool havePrev = false;
                            Reading prev = new Reading();
                            while (true)
                            {
                                int n = fs.Read(buf, 0, 5); // blocks until the next frame
                                if (n <= 0) break;
                                Reading r;
                                int model;
                                if (!Decode(buf, n, out r, out model)) continue;
                                // Right after the link comes up the mouse may send one wrong 100 % frame:
                                // a reading counts once it was seen twice in a row.
                                bool confirmed = havePrev && prev.Percent == r.Percent && prev.State == r.State;
                                prev = r;
                                havePrev = true;
                                lock (sync)
                                {
                                    lastFrame = DateTime.Now;
                                    marker = model;
                                    if (confirmed) { last = r; haveReading = true; }
                                }
                            }
                        }
                    }
                    catch (Exception) { } // receiver unplugged: look for it again
                }
                Thread.Sleep(1000);
            }
        }
    }
}
