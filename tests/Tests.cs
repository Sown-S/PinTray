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
