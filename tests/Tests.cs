using System;

namespace PinTray.Tests
{
    /// Small test runner (no test framework needed): exits with 1 when a check fails.
    static class TestMain
    {
        static int fails;

        static void Check(bool ok, string what)
        {
            if (!ok) fails++;
            Console.WriteLine((ok ? "OK    " : "FAIL  ") + what);
        }

        static int Main()
        {
            AttackShark();
            Estimate();
            ChargeLearning();
            Colours();
            Console.WriteLine(fails == 0 ? "All tests passed." : fails + " test(s) failed.");
            return fails == 0 ? 0 : 1;
        }

        static void AttackShark()
        {
            Reading r;
            int model;
            Check(AttackSharkDriver.Decode(new byte[] { 3, 0x10, 0x40, 1, 8 }, 5, out r, out model)
                && r.Percent == 80 && r.State == Reading.OnBattery && model == 0x10, "Attack Shark R1: 08 = 80 %");
            Check(AttackSharkDriver.Decode(new byte[] { 3, 0x10, 0x40, 3, 10 }, 5, out r, out model)
                && r.Percent == 100 && r.State == Reading.Charging, "Attack Shark R1: 0A charging = 100 %");
            Check(AttackSharkDriver.Decode(new byte[] { 3, 0x55, 0x40, 1, 62 }, 5, out r, out model)
                && r.Percent == 62, "Attack Shark X11: plain percentage");
            Check(!AttackSharkDriver.Decode(new byte[] { 3, 0x10, 0x01, 0, 0 }, 5, out r, out model), "link-blink frame is not a battery frame");
            Check(!AttackSharkDriver.Decode(new byte[] { 3, 0x10, 0x40, 1, 200 }, 5, out r, out model), "level over 100 is rejected");
            Check(!AttackSharkDriver.Decode(new byte[] { 3, 0x10, 0x40 }, 3, out r, out model), "short frame is rejected");
            Check(AttackSharkDriver.ModelName(0x10) == "Attack Shark R1", "model marker 10 = R1");
        }

        static void Colours()
        {
            var white = System.Drawing.Color.White;
            var red = DeviceTray.LevelColor(10, false);
            var yellow = DeviceTray.LevelColor(50, false);
            Check(DeviceTray.LevelColor(60, false) == white && DeviceTray.LevelColor(90, false) == white, "colour: 60-90 % white");
            Check(DeviceTray.LevelColor(30, false) == yellow && yellow != white && yellow != red, "colour: 30-50 % yellow");
            Check(DeviceTray.LevelColor(20, false) == red && red.R == 0xFF && red.G < 0x80, "colour: 10-20 % red");
            Check(DeviceTray.LevelColor(60, true) == System.Drawing.Color.Black, "colour: black on a light taskbar");
        }

        static void ChargeLearning()
        {
            const string key = "pintray-test-charge";
            Store.DeleteDevice(key);
            try
            {
                var c = new ChargeModel(key);
                Check(Math.Abs(c.MinutesLeft(40, 0, 120) - 72) < 0.01, "charge: no data, fallback 120 min from 40 % = 72 min");
                Check(!c.Learn(-1, 80, 60), "charge: unknown level before, nothing learned");
                Check(!c.Learn(50, 60, 2), "charge: too short to learn");

                // Reached 100 % at an unknown time: only an upper bound (50 -> 100 within 60 min = full in 120 min at most).
                Check(c.Learn(50, 100, 60) && !c.Exact && Math.Abs(c.FullMinutes - 120) < 0.01, "charge: 100 % after unplug gives an upper bound");
                Check(c.Learn(50, 100, 45) && Math.Abs(c.FullMinutes - 90) < 0.01, "charge: a tighter bound replaces it");
                Check(!c.Learn(50, 100, 80), "charge: a looser bound is ignored");

                // Unplugged below 100 %: a measured rate (20 -> 60 in 40 min = 100 min for a full charge).
                Check(c.Learn(20, 60, 40) && c.Exact && Math.Abs(c.FullMinutes - 100) < 0.01, "charge: measured rate wins over the bound");
                Check(c.Learn(30, 80, 40) && Math.Abs(c.FullMinutes - 90) < 0.01, "charge: measured rates are averaged");
                Check(Math.Abs(c.MinutesLeft(40, 30, 120) - 24) < 0.01, "charge: 40 % after 30 min of a 90-min charge = 24 min left");

                var loaded = new ChargeModel(key);
                Check(loaded.Exact && Math.Abs(loaded.FullMinutes - 90) < 0.01, "charge: survives save and load");
            }
            finally { Store.DeleteDevice(key); }
        }

        static void Estimate()
        {
            const string key = "pintray-test-device";
            Store.DeleteDevice(key);
            try
            {
                var e = new Estimator(key);
                e.AwakeSeconds = 1000;
                Check(e.HoursLeft(90) < 0, "estimate: no drops, no estimate");
                e.Dropped(90);
                Check(e.HoursLeft(90) < 0, "estimate: one drop, no estimate");
                e.AwakeSeconds += 3 * 3600; // 10 % took 3 awake hours
                e.Dropped(80);
                Check(Math.Abs(e.HoursLeft(80) - 24) < 0.01, "estimate: 10 % per 3 h at 80 % = 24 h");

                var loaded = new Estimator(key);
                loaded.Load();
                Check(Math.Abs(loaded.HoursLeft(80) - 24) < 0.01, "estimate: survives save and load");

                e.Reset();
                Check(e.HoursLeft(80) < 0, "estimate: reset after charging");
            }
            finally { Store.DeleteDevice(key); }
        }
    }
}
