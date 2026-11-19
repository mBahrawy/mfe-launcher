using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MfeLauncher
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var root = AppDiscovery.RealCase(AppDomain.CurrentDomain.BaseDirectory);
            Application.Run(new MainForm(root, AppDiscovery.Discover(root)));
        }
    }
}
