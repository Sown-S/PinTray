using System;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("PinTray")]
[assembly: AssemblyProduct("PinTray")]
[assembly: AssemblyDescription("Battery levels of wireless mice and keyboards in the notification area")]
[assembly: AssemblyVersion("1.1.0.0")]
[assembly: AssemblyFileVersion("1.1.0.0")]

namespace PinTray
{
    static class Program
    {
        public const string Version = "1.1.0";

        [STAThread]
        static void Main(string[] args)
        {
            Native.SetProcessDPIAware();
            Application.EnableVisualStyles();

            if (args.Length > 0 && (args[0] == "--report" || args[0] == "/report"))
            {
                if (MessageBox.Show(Report.Intro, "PinTray – báo cáo thiết bị", MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                    App.Open(Report.Run(Report.Seconds));
                return;
            }

            bool fresh;
            using (new Mutex(true, "PinTray-single-instance", out fresh))
            {
                if (!fresh) return;
                Application.Run(new App());
            }
        }
    }
}
