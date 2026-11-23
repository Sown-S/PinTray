using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace PinTray
{
    /// "Device report": lists every HID device and records, for a few seconds, the reports that
    /// vendor-specific collections send on their own. It is what is needed to add a new device.
    ///
    /// Read-only: nothing is ever sent to any device. Keyboard and mouse collections (where typing
    /// and movement go) are never read; only vendor-defined and battery-related ones.
    static class Report
    {
        public const int Seconds = 20;

        public const string Intro =
            "PinTray sẽ ghi lại danh sách thiết bị HID và các gói tin mà chúng tự gửi trong " + "20 giây, rồi mở file báo cáo.\n\n" +
            "Trong lúc ghi:\n" +
            "• Bật thiết bị cần thêm (chế độ 2.4G hoặc Bluetooth), di chuột / bấm vài phím bình thường.\n" +
            "• Nếu được, mở app hoặc web driver của hãng để nó hiện % pin.\n" +
            "• ĐỪNG gõ mật khẩu hay nội dung riêng tư.\n\n" +
            "PinTray chỉ đọc, không gửi gì xuống thiết bị. Báo cáo được lưu trên Desktop, bạn tự gửi cho người phát triển.";

        /// Collections worth listening to: vendor-defined pages and battery / power pages.
        static bool Interesting(HidCollection c)
        {
            if (c.InLen == 0) return false;
            if (c.UsagePage >= 0xFF00) return true;
            switch (c.UsagePage)
            {
                case 0x0006: // generic device controls (battery strength)
                case 0x000A: // ordinal (Attack Shark receivers)
                case 0x000B: // telephony
                case 0x0084: // power device
                case 0x0085: // battery system
                    return true;
            }
            return false;
        }

        static string WindowsVersion()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    return k.GetValue("ProductName") + " " + k.GetValue("DisplayVersion") + " build " + k.GetValue("CurrentBuild");
            }
            catch (Exception) { return Environment.OSVersion.ToString(); }
        }

        class Capture
        {
            public HidCollection Coll;
            public readonly List<string> Frames = new List<string>();
            public int Total;
            public string Error;
        }

        static void Listen(Capture cap, DateTime end)
        {
            try
            {
                using (var h = Hid.OpenRead(cap.Coll.Path, true))
                {
                    if (h.IsInvalid) { cap.Error = "không mở được để đọc"; return; }
                    using (var fs = new FileStream(h, FileAccess.Read, cap.Coll.InLen, true))
                    {
                        while (true)
                        {
                            int left = (int)(end - DateTime.Now).TotalMilliseconds;
                            if (left <= 0) return;
                            var buf = new byte[cap.Coll.InLen];
                            Task<int> t = fs.ReadAsync(buf, 0, buf.Length);
                            if (!t.Wait(left)) return; // closing the stream cancels the pending read
                            cap.Total++;
                            if (cap.Frames.Count < 40)
                                cap.Frames.Add(DateTime.Now.ToString("HH:mm:ss.fff") + "  " + BitConverter.ToString(buf, 0, t.Result));
                        }
                    }
                }
            }
            catch (Exception e) { cap.Error = e.GetBaseException().Message; }
        }

        /// Writes the report to the Desktop and returns its path.
        public static string Run(int seconds)
        {
            var all = Hid.All();
            var sb = new StringBuilder();
            sb.AppendLine("PinTray " + Program.Version + " – báo cáo thiết bị");
            sb.AppendLine("Thời gian: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Windows:   " + WindowsVersion());
            sb.AppendLine("Đã ghi " + seconds + " giây. Chỉ đọc, không gửi lệnh nào xuống thiết bị.");
            sb.AppendLine();
            sb.AppendLine("Thiết bị đang hỗ trợ:");
            foreach (var d in Drivers.All())
            {
                // Drivers are not started here; list what they claim instead.
                sb.AppendLine("  " + d.GetType().Name);
            }
            sb.AppendLine();

            // 1. Device list, grouped by VID:PID.
            sb.AppendLine("=== Thiết bị HID ===");
            string current = null;
            foreach (var c in all)
            {
                if (c.VidPid != current)
                {
                    current = c.VidPid;
                    string product = Hid.Product(c.Path), maker = Hid.Manufacturer(c.Path);
                    sb.AppendLine();
                    sb.AppendLine(c.VidPid + "  " + (maker.Length > 0 ? maker + " – " : "") + (product.Length > 0 ? product : "(không tên)"));
                }
                sb.AppendLine(string.Format("   {0,-6} page={1:X4} usage={2:X4} in={3} out={4} feat={5}   {6}",
                    c.Interface >= 0 ? "MI_" + c.Interface.ToString("X2") : "", c.UsagePage, c.Usage, c.InLen, c.OutLen, c.FeatLen, c.InstanceId));
            }

            // 2. What vendor collections send on their own.
            var caps = new List<Capture>();
            DateTime end = DateTime.Now.AddSeconds(seconds);
            var threads = new List<Thread>();
            foreach (var c in all)
            {
                if (!Interesting(c)) continue;
                var cap = new Capture { Coll = c };
                caps.Add(cap);
                var t = new Thread(delegate() { Listen(cap, end); });
                t.IsBackground = true;
                t.Start();
                threads.Add(t);
            }
            foreach (var t in threads) t.Join(TimeSpan.FromSeconds(seconds + 5));

            sb.AppendLine();
            sb.AppendLine("=== Gói tin tự gửi (kênh vendor / pin) ===");
            foreach (var cap in caps)
            {
                sb.AppendLine();
                sb.AppendLine(string.Format("{0} {1} page={2:X4} in={3}: {4}", cap.Coll.VidPid,
                    cap.Coll.Interface >= 0 ? "MI_" + cap.Coll.Interface.ToString("X2") : "", cap.Coll.UsagePage, cap.Coll.InLen,
                    cap.Error != null ? cap.Error : cap.Total + " gói" + (cap.Total > cap.Frames.Count ? " (hiện " + cap.Frames.Count + " gói đầu)" : "")));
                foreach (string f in cap.Frames) sb.AppendLine("   " + f);
            }

            string dir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!Directory.Exists(dir)) dir = Store.Dir;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "PinTray-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }
    }
}
