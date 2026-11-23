using System;
using System.Drawing;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace PinTray
{
    /// The notification-area icon of one device, and everything PinTray keeps about it:
    /// saved level, history, time-left estimate, charge reminder, stuck-receiver check.
    class DeviceTray : IDisposable
    {
        /// No answer for this long means the device is asleep or off.
        static readonly TimeSpan Silent = TimeSpan.FromSeconds(10);
        /// The device was used but its receiver stayed silent this long: the receiver is stuck.
        static readonly TimeSpan StuckAfter = TimeSpan.FromSeconds(30);
        static readonly Color Green = Color.FromArgb(0x2E, 0xCC, 0x40);
        static readonly Color Red = Color.FromArgb(0xFF, 0x41, 0x36);
        static readonly Color Orange = Color.FromArgb(0xFF, 0x98, 0x00);
        /// Yellow that stays readable: bright on a dark taskbar, deeper on a light one.
        static readonly Color YellowOnDark = Color.FromArgb(0xFF, 0xD2, 0x3F);
        static readonly Color YellowOnLight = Color.FromArgb(0xB8, 0x86, 0x00);
        /// Level colours: white (or black on a light taskbar) above 50 %, yellow 30-50 %, red at 20 % and below.
        public const int YellowAt = 50, RedAt = 20;
        /// Icon text that stands for the full-battery drawing (shown instead of "100").
        const string FullBattery = "FULL";

        public static Color LevelColor(int percent, bool lightTaskbar)
        {
            if (percent <= RedAt) return Red;
            if (percent <= YellowAt) return lightTaskbar ? YellowOnLight : YellowOnDark;
            return lightTaskbar ? Color.Black : Color.White;
        }
        /// Icon text that stands for the lightning-bolt drawing.
        const string Bolt = "⚡";

        readonly App app;
        readonly string key;
        string name;
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Estimator est;
        readonly ToolStripMenuItem statusItem = new ToolStripMenuItem();
        readonly ToolStripMenuItem estimateItem = new ToolStripMenuItem();
        readonly ToolStripMenuItem seenItem = new ToolStripMenuItem();
        readonly ToolStripMenuItem vendorItem = new ToolStripMenuItem();
        IntPtr iconHandle = IntPtr.Zero;
        string lastIconKey;
        int warnedAt = 101; // lowest warning level already shown since the last charge

        // Saved reading: what was last logged and when it was last confirmed live.
        int savedPercent, savedState;
        DateTime savedAt = DateTime.MinValue;
        DateTime lastTick = DateTime.Now;
        DateTime lastPersist = DateTime.Now;

        bool wasWired;
        DateTime wiredSince;
        bool reminded;

        // Charging: level before the cable went in, the charge waiting for its level after, the model.
        readonly ChargeModel charge;
        int plugPercent = -1;
        bool pendingCharge;
        int pendingBefore;
        double pendingMinutes;
        DateTime unpluggedAt;
        bool fullNotified;
        /// While charging the bolt fills up again and again.
        static readonly float[] AnimFill = { 0.2f, 0.4f, 0.6f, 0.8f, 1f, 1f };
        readonly Timer anim = new Timer();
        Icon[] animIcons;
        IntPtr[] animHandles;
        int animFrame;

        DateTime stuckUseSince = DateTime.MinValue; // first use seen during the current silence
        bool stuck, stuckWarned, watching;
        string watchMatch;

        public DeviceTray(App app, DeviceStatus first)
        {
            this.app = app;
            key = first.Key;
            name = Store.Get(key, "Name", first.Name);
            savedPercent = Store.GetInt(key, "LastPercent", -1);
            savedState = Store.GetInt(key, "LastState", Reading.OnBattery);
            long ticks = Store.GetLong(key, "LastSeen", 0);
            if (ticks > 0) savedAt = new DateTime(ticks);
            est = new Estimator(key);
            est.Load();
            charge = new ChargeModel(key);
            anim.Interval = 350;
            anim.Tick += delegate { NextFrame(); };

            var menu = new ContextMenuStrip();
            foreach (var item in new[] { statusItem, estimateItem, seenItem })
            {
                item.Enabled = false;
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            vendorItem.Click += delegate { App.Open(vendorItem.Tag as string); };
            menu.Items.Add(vendorItem);
            app.AddCommonItems(menu);
            tray.ContextMenuStrip = menu;
            Update(first, DateTime.Now);
            tray.Visible = true;
        }

        public void Save()
        {
            if (savedAt != DateTime.MinValue) Store.Set(key, "LastSeen", savedAt.Ticks);
            est.Save();
        }

        public void Dispose()
        {
            Save();
            if (watching) app.Watcher.Watch(watchMatch, false);
            anim.Stop();
            tray.Visible = false;
            tray.Dispose();
            if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
            if (animHandles != null) foreach (IntPtr h in animHandles) Native.DestroyIcon(h);
        }

        /// Charging animation: a red bolt that fills from the bottom, over and over.
        void StartAnim()
        {
            if (anim.Enabled) return;
            if (animIcons == null)
            {
                animIcons = new Icon[AnimFill.Length];
                animHandles = new IntPtr[AnimFill.Length];
                for (int i = 0; i < AnimFill.Length; i++)
                    using (Bitmap bmp = RenderBolt(Green, AnimFill[i]))
                    {
                        animHandles[i] = bmp.GetHicon();
                        animIcons[i] = Icon.FromHandle(animHandles[i]);
                    }
            }
            lastIconKey = "anim";
            animFrame = 0;
            NextFrame();
            anim.Start();
        }

        void StopAnim()
        {
            if (!anim.Enabled) return;
            anim.Stop();
            lastIconKey = null; // the next Update draws the static icon
        }

        void NextFrame()
        {
            tray.Icon = animIcons[animFrame];
            animFrame = (animFrame + 1) % animIcons.Length;
        }

        static string Hours(double h)
        {
            if (h < 1) return Math.Max(1, (int)Math.Round(h * 60)) + " phút";
            if (h < 10) return h.ToString("0.#", CultureInfo.GetCultureInfo("vi-VN")) + " giờ";
            return Math.Round(h).ToString(CultureInfo.InvariantCulture) + " giờ";
        }

        static string Ago(DateTime t)
        {
            if (t == DateTime.MinValue) return "chưa rõ";
            TimeSpan d = DateTime.Now - t;
            if (d.TotalMinutes < 1) return "vừa xong";
            if (d.TotalHours < 1) return (int)d.TotalMinutes + " phút trước";
            if (d.TotalDays < 1) return (int)d.TotalHours + " giờ trước";
            return t.ToString("dd/MM HH:mm");
        }

        void Watch(bool on)
        {
            if (on == watching || watchMatch == null) return;
            watching = on;
            app.Watcher.Watch(watchMatch, on);
        }

        /// Keeps the saved state, the history, the estimate, the charge reminder and the stuck check up to date.
        void Track(DeviceStatus s, DateTime now, bool live)
        {
            double dt = (now - lastTick).TotalSeconds;
            lastTick = now;
            if (live && dt > 0 && dt < 5) est.AwakeSeconds += dt;

            // Cable in / out.
            if (s.Wired && !wasWired)
            {
                wiredSince = now; reminded = false; fullNotified = false;
                // The level before charging, if it is recent enough to trust.
                plugPercent = savedAt != DateTime.MinValue && (now - savedAt).TotalMinutes < 30 ? savedPercent : -1;
                pendingCharge = false;
                est.Reset();
                warnedAt = 101;
                Store.Log(name, "cam_cap_sac", savedPercent);
            }
            else if (!s.Wired && wasWired)
            {
                // The first live level after this tells how far the charge got.
                pendingCharge = true;
                pendingBefore = plugPercent;
                pendingMinutes = (now - wiredSince).TotalMinutes;
                unpluggedAt = now;
                Store.Log(name, "rut_cap", -1);
            }
            wasWired = s.Wired;

            // Charge reminder.
            int remind = app.RemindMinutes;
            if (s.Wired && !reminded && remind > 0 && (now - wiredSince).TotalMinutes >= remind)
            {
                reminded = true;
                tray.ShowBalloonTip(10000, "Có thể rút sạc", name + " đã cắm sạc " + Hours((now - wiredSince).TotalHours) + ". Bạn có thể rút cáp.", ToolTipIcon.Info);
            }

            // A new live reading: save it, log it, count drops for the estimate.
            if (pendingCharge && (now - unpluggedAt).TotalMinutes > 15) pendingCharge = false;
            if (live)
            {
                Reading r = s.Last;
                if (pendingCharge)
                {
                    pendingCharge = false;
                    if (charge.Learn(pendingBefore, r.Percent, pendingMinutes))
                        Store.Log(name, "hoc_thoi_gian_sac_day_" + Math.Round(charge.FullMinutes) + "_phut", r.Percent);
                }
                // A change seen right after a gap (PinTray closed, PC off) happened at an unknown time.
                bool continuous = savedAt != DateTime.MinValue && (now - savedAt).TotalMinutes < 2;
                if (r.Percent != savedPercent || r.State != savedState)
                {
                    bool fell = r.State == Reading.OnBattery && savedPercent >= 0 && r.Percent < savedPercent;
                    if (fell && continuous) est.Dropped(r.Percent);
                    else if (fell || r.State == Reading.Charging || (savedPercent >= 0 && r.Percent > savedPercent)) est.Reset();
                    Store.Log(name, r.State == Reading.Charging ? "dang_sac" : r.State == Reading.OnPower ? "cam_nguon" : "dung_pin", r.Percent);
                    savedPercent = r.Percent; savedState = r.State;
                    Store.Set(key, "LastPercent", r.Percent);
                    Store.Set(key, "LastState", r.State);
                }
                savedAt = now;
            }
            if ((now - lastPersist).TotalSeconds >= 60)
            {
                lastPersist = now;
                Save();
            }

            // Stuck receiver: silent while the device itself is being used.
            watchMatch = s.RawInputMatch;
            bool silent = s.Present && !s.Wired && !live && watchMatch != null;
            if (!silent)
            {
                if (stuck) Store.Log(name, "receiver_hoat_dong_lai", -1);
                Watch(false);
                stuckUseSince = DateTime.MinValue;
                stuck = false; stuckWarned = false;
                return;
            }
            if (stuck) return; // already known; watching stays off to save CPU
            Watch(true);
            DateTime used = app.Watcher.LastUse(watchMatch);
            if (used > s.LastFrame && (now - used).TotalSeconds < 3)
            {
                if (stuckUseSince == DateTime.MinValue) stuckUseSince = now;
                if (now - stuckUseSince >= StuckAfter)
                {
                    stuck = true;
                    Watch(false);
                    Store.Log(name, "receiver_bi_ket", -1);
                    if (!stuckWarned)
                    {
                        stuckWarned = true;
                        tray.ShowBalloonTip(10000, "Receiver không gửi pin",
                            name + " vẫn chạy nhưng receiver không báo pin. Hãy rút ra rồi cắm lại receiver.", ToolTipIcon.Warning);
                    }
                }
            }
            else if (used <= s.LastFrame || (now - used).TotalSeconds > 10)
            {
                stuckUseSince = DateTime.MinValue; // not in use: it is just asleep
            }
        }

        public void Update(DeviceStatus s, DateTime now)
        {
            if (s.Name != name && s.HasReading)
            {
                name = s.Name; // the model is known once the device has answered
                Store.Set(key, "Name", name);
            }
            bool live = s.HasReading && now - s.LastFrame <= Silent;
            Track(s, now, live);

            // Shown level: the live one, else the saved one.
            bool has = s.HasReading || savedPercent >= 0;
            Reading r = s.HasReading ? s.Last : new Reading { Percent = savedPercent, State = savedState };

            string text, tip, status, estimate = "";
            Color color;
            double hoursLeft = has ? est.HoursLeft(r.Percent) : -1;
            if (has && !s.Wired)
                estimate = hoursLeft > 0 ? "Còn khoảng " + Hours(hoursLeft) + " dùng" : "Thời gian còn lại: chưa đủ dữ liệu";

            bool animate = false;
            if (s.Wired)
            {
                // On the cable the device sends no level and no "full": estimate it from past charges.
                double elapsed = (now - wiredSince).TotalMinutes;
                double fallback = app.RemindMinutes > 0 ? app.RemindMinutes : 120;
                double left = charge.MinutesLeft(plugPercent, elapsed, fallback);
                string basis = charge.FullMinutes > 0 ? "theo các lần sạc trước" : "chưa có dữ liệu, tạm tính " + Hours(fallback / 60.0) + " cho cả lần sạc";
                string before = plugPercent >= 0 ? " (cắm lúc " + plugPercent + "%)" : "";
                if (left <= 0)
                {
                    text = FullBattery; color = Green;
                    tip = name + ": ước tính đã sạc đầy";
                    status = "Ước tính đã sạc đầy" + before;
                    if (!fullNotified)
                    {
                        fullNotified = true;
                        tray.ShowBalloonTip(8000, "Có lẽ đã sạc đầy", name + " đã sạc " + Hours(elapsed / 60.0)
                            + " (ước tính " + basis + "). Đèn trên chuột chuyển xanh là đầy thật.", ToolTipIcon.Info);
                    }
                }
                else
                {
                    text = Bolt; color = Green; animate = true;
                    tip = name + ": đang sạc, đầy sau ~" + Hours(left / 60.0);
                    status = "Đang sạc" + before;
                }
                estimate = (left > 0 ? "Ước tính đầy sau ~" + Hours(left / 60.0) : "Đã cắm " + Hours(elapsed / 60.0)) + " (" + basis + ")";
            }
            else if (!s.Present)
            {
                text = "–"; color = Color.Gray;
                tip = name + ": không thấy receiver";
                status = "Không thấy receiver";
            }
            else if (stuck)
            {
                text = "!"; color = Orange;
                tip = name + ": receiver kẹt – rút ra cắm lại";
                status = "Receiver không gửi pin – rút ra cắm lại";
            }
            else if (!has)
            {
                text = "?"; color = Color.Gray;
                tip = name + ": đang chờ dữ liệu pin";
                status = "Đang chờ dữ liệu pin (hãy dùng thiết bị)";
            }
            else
            {
                text = r.Percent >= 100 ? FullBattery : r.Percent.ToString(CultureInfo.InvariantCulture);
                string state = r.State == Reading.Charging ? "đang sạc" : r.State == Reading.OnPower ? "cắm nguồn" : "dùng pin";
                if (!live) state = "đang ngủ, mức cuối";
                tip = name + ": " + r.Percent + "% (" + state + ")";
                if (live && hoursLeft > 0) tip += " · còn ~" + Hours(hoursLeft);
                status = "Pin " + r.Percent + "% – " + state;
                if (!live) color = Color.Gray;
                else if (r.State == Reading.Charging) color = Green;
                else color = LevelColor(r.Percent, App.LightTaskbar());

                // Low-battery notice at 20 % and 10 %, once each until charged.
                if (r.State == Reading.Charging || r.Percent > 20) warnedAt = 101;
                else if (live && r.Percent <= 10 && warnedAt > 10) { warnedAt = 10; Warn(r.Percent); }
                else if (live && r.Percent <= 20 && warnedAt > 20) { warnedAt = 20; Warn(r.Percent); }
            }

            if (tip.Length > 63) tip = tip.Substring(0, 63);
            tray.Text = tip;
            statusItem.Text = name + " – " + status;
            estimateItem.Text = estimate;
            estimateItem.Visible = estimate.Length > 0;
            seenItem.Text = "Cập nhật: " + (live ? "vừa xong" : Ago(savedAt));
            seenItem.Visible = !s.Wired && has;
            vendorItem.Visible = s.VendorApp != null;
            vendorItem.Text = "Mở app " + name;
            vendorItem.Tag = s.VendorApp;

            if (animate) { StartAnim(); return; }
            StopAnim();
            string iconKey = text + "|" + color.ToArgb();
            if (iconKey == lastIconKey) return;
            lastIconKey = iconKey;
            SetIcon(text, color);
        }

        void Warn(int pct)
        {
            tray.ShowBalloonTip(5000, "Sắp hết pin", name + " còn " + pct + "%. Hãy sạc.", ToolTipIcon.Warning);
        }

        /// Draws the bolt; with fill below 1 only the bottom part is bright, the rest dimmed.
        static void DrawBolt(Graphics g, Color color, Size sz, float fill)
        {
            // Bolt in a 0..1 box, then scaled to the icon (a little narrower than tall).
            float[,] p = { { 0.62f, 0.00f }, { 0.16f, 0.58f }, { 0.46f, 0.58f }, { 0.36f, 1.00f },
                           { 0.84f, 0.38f }, { 0.54f, 0.38f }, { 0.66f, 0.00f } };
            float w = sz.Width * 0.85f, h = sz.Height;
            float ox = (sz.Width - w) / 2;
            var pts = new PointF[p.GetLength(0)];
            for (int i = 0; i < pts.Length; i++) pts[i] = new PointF(ox + p[i, 0] * w, p[i, 1] * h);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var bright = new SolidBrush(color))
            {
                if (fill >= 1) { g.FillPolygon(bright, pts); return; }
                using (var dim = new SolidBrush(Color.FromArgb(90, color)))
                    g.FillPolygon(dim, pts);
                g.SetClip(new RectangleF(0, h * (1 - fill), sz.Width, h * fill));
                g.FillPolygon(bright, pts);
                g.ResetClip();
            }
        }

        /// Slate outline of the full-battery icon; the core takes the state colour.
        static readonly Color BatteryOutline = Color.FromArgb(0x7C, 0x93, 0xA1);

        static System.Drawing.Drawing2D.GraphicsPath Rounded(RectangleF r, float radius)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// A full battery standing upright (unlike Windows' own sideways one): rounded slate
        /// outline, terminal on top, and a core in the state colour with a small gap around it.
        static void DrawFullBattery(Graphics g, Color color, Size sz)
        {
            float w = sz.Width, h = sz.Height;
            float stroke = Math.Max(1.5f, w / 11f);
            float nubH = h * 0.10f;
            float bodyW = w * 0.68f, bodyH = h - nubH - 1;
            var body = new RectangleF((w - bodyW) / 2, nubH + 0.5f, bodyW, bodyH);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using (var outline = new Pen(BatteryOutline, stroke))
            using (var outlineFill = new SolidBrush(BatteryOutline))
            using (var core = new SolidBrush(color))
            {
                // Terminal: a small rounded cap centred on top.
                float nubW = bodyW * 0.45f;
                using (var nub = Rounded(new RectangleF((w - nubW) / 2, 0.5f, nubW, nubH + stroke), stroke * 0.6f))
                    g.FillPath(outlineFill, nub);
                var inset = RectangleF.Inflate(body, -stroke / 2, -stroke / 2);
                using (var path = Rounded(inset, stroke * 1.4f))
                    g.DrawPath(outline, path);
                float gap = stroke * 0.7f;
                var inner = RectangleF.Inflate(body, -(stroke + gap), -(stroke + gap));
                using (var path = Rounded(inner, stroke * 0.7f))
                    g.FillPath(core, path);
            }
        }

        public static Bitmap RenderBolt(Color color, float fill)
        {
            Size sz = SystemInformation.SmallIconSize;
            var bmp = new Bitmap(sz.Width, sz.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                DrawBolt(g, color, sz, fill);
            }
            return bmp;
        }

        /// Draws the icon: a number (or symbol) as large as fits, or the bolt.
        public static Bitmap Render(string text, Color color)
        {
            Size sz = SystemInformation.SmallIconSize;
            var bmp = new Bitmap(sz.Width, sz.Height);
            using (var g = Graphics.FromImage(bmp))
            using (var brush = new SolidBrush(color))
            {
                g.Clear(Color.Transparent);
                if (text == FullBattery)
                {
                    DrawFullBattery(g, color, sz);
                    return bmp;
                }
                if (text == Bolt)
                {
                    DrawBolt(g, color, sz, 1f);
                    return bmp;
                }
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                var fmt = new StringFormat(StringFormat.GenericTypographic);
                fmt.Alignment = StringAlignment.Center;
                fmt.LineAlignment = StringAlignment.Center;
                // One line, never wrapped: "100" once lost its last digit to a second, hidden line.
                fmt.FormatFlags |= StringFormatFlags.NoWrap | StringFormatFlags.NoClip;
                float size = sz.Height;
                Font font = null;
                while (size > 4)
                {
                    if (font != null) font.Dispose();
                    font = new Font("Segoe UI", size, FontStyle.Bold, GraphicsUnit.Pixel);
                    SizeF m = g.MeasureString(text, font, 1000, fmt);
                    if (m.Width <= sz.Width && m.Height <= sz.Height + 2) break;
                    size -= 0.5f;
                }
                g.DrawString(text, font, brush, new RectangleF(0, 0, sz.Width, sz.Height + 1), fmt);
                font.Dispose();
            }
            return bmp;
        }

        void SetIcon(string text, Color color)
        {
            using (Bitmap bmp = Render(text, color))
            {
                IntPtr h = bmp.GetHicon();
                Icon old = tray.Icon;
                tray.Icon = Icon.FromHandle(h);
                if (old != null) old.Dispose();
                if (iconHandle != IntPtr.Zero) Native.DestroyIcon(iconHandle);
                iconHandle = h;
            }
        }
    }
}
