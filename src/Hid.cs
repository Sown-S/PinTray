using System;
using System.Collections.Generic;
using System.Globalization;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace PinTray
{
    /// One HID top-level collection (what Windows calls a "HID-compliant ... device").
    class HidCollection
    {
        public string InstanceId;
        public string Path;
        public ushort Vid, Pid;
        public int Interface = -1; // MI_xx, or -1
        public ushort UsagePage, Usage;
        public ushort InLen, OutLen, FeatLen;

        public string VidPid { get { return Vid.ToString("X4") + ":" + Pid.ToString("X4"); } }
    }

    static class Hid
    {
        const string HidGuid = "{4d1e55b2-f16f-11cf-88cb-001111000030}";
        // USB: VID_1D57&PID_FA60&MI_02   Bluetooth: VID&0002046D_PID&B023
        static readonly Regex Ids = new Regex(@"VID[_&](?:0002|0001)?([0-9A-F]{4})[_&]PID[_&]([0-9A-F]{4})(?:&MI_([0-9A-F]{2}))?", RegexOptions.IgnoreCase);

        public static string PathOf(string instanceId)
        {
            return @"\\?\" + instanceId.Replace('\\', '#') + "#" + HidGuid;
        }

        /// HID collections whose device instance id matches a WMI LIKE pattern, e.g.
        /// @"HID\\VID_1D57&PID_FA60%" (backslashes doubled for WQL).
        public static List<HidCollection> Find(string likePattern)
        {
            var list = new List<HidCollection>();
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT DeviceID FROM Win32_PnPEntity WHERE DeviceID LIKE '" + likePattern + "'"))
                {
                    foreach (ManagementObject o in s.Get())
                    {
                        var c = Describe((string)o["DeviceID"]);
                        if (c != null) list.Add(c);
                    }
                }
            }
            catch (Exception) { }
            list.Sort(delegate(HidCollection a, HidCollection b) { return string.Compare(a.InstanceId, b.InstanceId, StringComparison.Ordinal); });
            return list;
        }

        /// Every HID collection on the system.
        public static List<HidCollection> All()
        {
            return Find(@"HID\\%");
        }

        static HidCollection Describe(string instanceId)
        {
            var c = new HidCollection();
            c.InstanceId = instanceId;
            c.Path = PathOf(instanceId);
            Match m = Ids.Match(instanceId);
            if (m.Success)
            {
                c.Vid = ushort.Parse(m.Groups[1].Value, NumberStyles.HexNumber);
                c.Pid = ushort.Parse(m.Groups[2].Value, NumberStyles.HexNumber);
                if (m.Groups[3].Success) c.Interface = int.Parse(m.Groups[3].Value, NumberStyles.HexNumber);
            }
            using (var h = OpenInfo(c.Path))
            {
                if (h.IsInvalid) return c;
                IntPtr pp;
                if (!Native.HidD_GetPreparsedData(h, out pp)) return c;
                var caps = new byte[64];
                Native.HidP_GetCaps(pp, caps);
                Native.HidD_FreePreparsedData(pp);
                c.Usage = BitConverter.ToUInt16(caps, 0);
                c.UsagePage = BitConverter.ToUInt16(caps, 2);
                c.InLen = BitConverter.ToUInt16(caps, 4);
                c.OutLen = BitConverter.ToUInt16(caps, 6);
                c.FeatLen = BitConverter.ToUInt16(caps, 8);
            }
            return c;
        }

        /// Opens a collection without read access (works even for keyboards and mice, which Windows
        /// keeps for itself): enough for its descriptor and strings.
        public static SafeFileHandle OpenInfo(string path)
        {
            return Native.CreateFile(path, 0, Native.SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
        }

        /// Opens a collection for reading its input reports; shared, so the vendor's own app keeps working.
        public static SafeFileHandle OpenRead(string path, bool overlapped)
        {
            return Native.CreateFile(path, Native.GENERIC_READ, Native.SHARE_READ_WRITE, IntPtr.Zero, Native.OPEN_EXISTING,
                overlapped ? Native.FILE_FLAG_OVERLAPPED : 0, IntPtr.Zero);
        }

        public static bool CanOpen(string path)
        {
            using (var h = OpenInfo(path)) return !h.IsInvalid;
        }

        public static string Product(string path) { return Str(path, true); }
        public static string Manufacturer(string path) { return Str(path, false); }

        static string Str(string path, bool product)
        {
            using (var h = OpenInfo(path))
            {
                if (h.IsInvalid) return "";
                var buf = new byte[256];
                bool ok = product ? Native.HidD_GetProductString(h, buf, buf.Length) : Native.HidD_GetManufacturerString(h, buf, buf.Length);
                if (!ok) return "";
                return Encoding.Unicode.GetString(buf).TrimEnd('\0').Trim();
            }
        }
    }
}
