using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace PinTray
{
    /// Notices that a given device is being used (mouse moved, key pressed), through Raw Input.
    /// Only the device name is looked at, never what was typed. It is switched on only while a
    /// receiver is silent, since it sees every mouse and keyboard report.
    class InputWatcher : NativeWindow
    {
        readonly Dictionary<IntPtr, string> names = new Dictionary<IntPtr, string>();
        readonly Dictionary<string, DateTime> lastUse = new Dictionary<string, DateTime>();
        readonly HashSet<string> active = new HashSet<string>();
        readonly int headerSize = 8 + 2 * IntPtr.Size;
        bool registered;

        public InputWatcher()
        {
            var cp = new CreateParams();
            cp.Parent = new IntPtr(-3); // HWND_MESSAGE: an invisible message-only window
            CreateHandle(cp);
        }

        /// Starts or stops watching for a device-name fragment such as "VID_1D57&PID_FA60".
        public void Watch(string match, bool on)
        {
            if (match == null) return;
            match = match.ToUpperInvariant();
            if (on) active.Add(match); else active.Remove(match);
            Register(active.Count > 0);
        }

        public DateTime LastUse(string match)
        {
            DateTime t;
            return match != null && lastUse.TryGetValue(match.ToUpperInvariant(), out t) ? t : DateTime.MinValue;
        }

        public void StopAll()
        {
            active.Clear();
            Register(false);
        }

        void Register(bool on)
        {
            if (on == registered) return;
            var d = new Native.RAWINPUTDEVICE[2];
            d[0].UsagePage = 0x01; d[0].Usage = 0x02; // mouse
            d[1].UsagePage = 0x01; d[1].Usage = 0x06; // keyboard
            for (int i = 0; i < d.Length; i++)
            {
                d[i].Flags = on ? Native.RIDEV_INPUTSINK : Native.RIDEV_REMOVE;
                d[i].Target = on ? Handle : IntPtr.Zero;
            }
            if (Native.RegisterRawInputDevices(d, (uint)d.Length, (uint)Marshal.SizeOf(typeof(Native.RAWINPUTDEVICE))))
                registered = on;
        }

        string NameOf(IntPtr device)
        {
            string name;
            if (names.TryGetValue(device, out name)) return name;
            uint size = 0;
            Native.GetRawInputDeviceInfo(device, Native.RIDI_DEVICENAME, null, ref size);
            var sb = new StringBuilder((int)size + 1);
            Native.GetRawInputDeviceInfo(device, Native.RIDI_DEVICENAME, sb, ref size);
            name = sb.ToString().ToUpperInvariant();
            names[device] = name;
            return name;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_INPUT && active.Count > 0)
            {
                uint size = (uint)headerSize;
                IntPtr buf = Marshal.AllocHGlobal(headerSize);
                try
                {
                    if (Native.GetRawInputData(m.LParam, Native.RID_HEADER, buf, ref size, (uint)headerSize) != uint.MaxValue)
                    {
                        string name = NameOf(Marshal.ReadIntPtr(buf, 8));
                        foreach (string match in active)
                            if (name.Contains(match)) lastUse[match] = DateTime.Now;
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
            base.WndProc(ref m);
        }
    }
}
