using System;
using System.Collections.Generic;

namespace PinTray
{
    enum DeviceKind { Mouse, Keyboard, Headset, Other }

    /// One battery reading.
    struct Reading
    {
        public const int OnBattery = 1, OnPower = 2, Charging = 3;
        public int Percent;
        public int State;
    }

    /// What a driver knows about one device right now. The UI keeps everything else
    /// (saved level, history, estimate, reminders).
    class DeviceStatus
    {
        /// Stable id, used for saved settings: "attackshark-fa60".
        public string Key;
        public string Name;
        public DeviceKind Kind;
        /// The receiver or the device is plugged in.
        public bool Present;
        /// On its charging cable; it may then report no level.
        public bool Wired;
        /// A level was read since PinTray started.
        public bool HasReading;
        public Reading Last;
        /// When the device last answered. Silence for a while means it is asleep.
        public DateTime LastFrame = DateTime.MinValue;
        /// Raw Input device-name fragment ("VID_1D57&PID_FA60") of the device's own mouse or keyboard
        /// interface, used to tell "asleep" from "receiver stuck"; null if not supported.
        public string RawInputMatch;
        /// The vendor's own app, if installed; shown in the menu.
        public string VendorApp;
    }

    /// One family of devices. A driver runs its own background thread(s); Snapshot is called once
    /// a second from the UI thread and must not block.
    interface IDriver
    {
        void Start();
        List<DeviceStatus> Snapshot();
    }

    static class Drivers
    {
        /// Every supported family. Add new drivers here.
        public static IDriver[] All()
        {
            return new IDriver[]
            {
                new AttackSharkDriver(),
            };
        }
    }
}
