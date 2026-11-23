using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PinTray
{
    /// Runs the drivers and keeps one notification-area icon per device. With no device found,
    /// one "PinTray" icon stays so the menu (report, exit) is reachable.
    class App : ApplicationContext
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "PinTray";
        static readonly int[] RemindChoices = { 60, 90, 120, 180, 0 };

        public readonly InputWatcher Watcher = new InputWatcher();
        public int RemindMinutes;

        readonly IDriver[] drivers = Drivers.All();
        readonly Dictionary<string, DeviceTray> trays = new Dictionary<string, DeviceTray>();
        readonly NotifyIcon fallback = new NotifyIcon();
        readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        volatile string reportDone; // set by the report thread, picked up on the UI thread
        bool reportRunning;

        public App()
        {
            RemindMinutes = Store.GetInt(null, "RemindMinutes", 120);
            foreach (var d in drivers) d.Start();

            var menu = new ContextMenuStrip();
            var none = new ToolStripMenuItem("Chưa thấy thiết bị nào được hỗ trợ");
            none.Enabled = false;
            menu.Items.Add(none);
            menu.Items.Add(new ToolStripSeparator());
            AddCommonItems(menu);
            fallback.ContextMenuStrip = menu;
            fallback.Text = "PinTray: chưa thấy thiết bị nào";
            using (var bmp = DeviceTray.Render("–", Color.Gray))
                fallback.Icon = Icon.FromHandle(bmp.GetHicon());

            Store.Log("PinTray", "mo_app", -1);
            timer.Interval = 1000;
            timer.Tick += delegate { Tick(); };
            timer.Start();
            SystemEvents.SessionEnding += delegate { SaveAll(); };
            // Give the drivers a moment to find their devices before showing the fallback icon.
            ThreadPool.QueueUserWorkItem(delegate { Thread.Sleep(4000); fallbackAllowed = true; });
        }
        volatile bool fallbackAllowed;

        void Tick()
        {
            DateTime now = DateTime.Now;
            foreach (var d in drivers)
            {
                foreach (var s in d.Snapshot())
                {
                    DeviceTray t;
                    if (trays.TryGetValue(s.Key, out t)) t.Update(s, now);
                    else trays[s.Key] = new DeviceTray(this, s);
                }
            }
            fallback.Visible = trays.Count == 0 && fallbackAllowed;

            string done = reportDone;
            if (done != null)
            {
                reportDone = null;
                reportRunning = false;
                if (done.StartsWith("!")) MessageBox.Show("Không tạo được báo cáo: " + done.Substring(1), "PinTray");
                else Open(done);
            }
        }

        public static void Open(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { System.Diagnostics.Process.Start(path); } catch (Exception) { }
        }

        public static bool LightTaskbar()
        {
            try
            {
                object v = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "SystemUsesLightTheme", 0);
                return v is int && (int)v == 1;
            }
            catch (Exception) { return false; }
        }

        /// The items every icon's menu ends with.
        public void AddCommonItems(ContextMenuStrip menu)
        {
            var remind = new ToolStripMenuItem("Nhắc rút sạc sau");
            foreach (int m in RemindChoices)
            {
                int minutes = m;
                string label = m == 0 ? "Tắt" : (m / 60.0).ToString("0.#", CultureInfo.GetCultureInfo("vi-VN")) + " giờ";
                var it = new ToolStripMenuItem(label);
                it.Click += delegate { RemindMinutes = minutes; Store.Set(null, "RemindMinutes", minutes); };
                it.Tag = minutes;
                remind.DropDownItems.Add(it);
            }
            menu.Items.Add(remind);
            menu.Items.Add("Mở lịch sử pin", null, delegate
            {
                if (File.Exists(Store.History)) Open(Store.History);
                else MessageBox.Show("Chưa có lịch sử pin.", "PinTray");
            });
            menu.Items.Add("Tạo báo cáo thiết bị (để thêm hỗ trợ)...", null, delegate { StartReport(); });
            var autostart = new ToolStripMenuItem("Khởi động cùng Windows");
            autostart.Click += delegate { SetAutostart(!AutostartOn()); };
            menu.Items.Add(autostart);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Thoát", null, delegate { Quit(); });

            menu.Opening += delegate
            {
                foreach (ToolStripMenuItem it in remind.DropDownItems) it.Checked = (int)it.Tag == RemindMinutes;
                autostart.Checked = AutostartOn();
            };
        }

        void StartReport()
        {
            if (reportRunning) return;
            if (MessageBox.Show(Report.Intro, "PinTray – báo cáo thiết bị", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK)
                return;
            reportRunning = true;
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { reportDone = Report.Run(Report.Seconds); }
                catch (Exception e) { reportDone = "!" + e.Message; }
            });
        }

        void SaveAll()
        {
            foreach (var t in trays.Values) t.Save();
        }

        void Quit()
        {
            timer.Stop();
            foreach (var t in trays.Values) t.Dispose();
            trays.Clear();
            Watcher.StopAll();
            fallback.Visible = false;
            Application.Exit();
        }

        static bool AutostartOn()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(RunKey))
                return k != null && k.GetValue(RunName) != null;
        }

        static void SetAutostart(bool on)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                if (on) k.SetValue(RunName, "\"" + Application.ExecutablePath + "\"");
                else k.DeleteValue(RunName, false);
            }
        }
    }
}
