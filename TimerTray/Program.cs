using System;
using System.Threading;
using System.Windows.Forms;

namespace TimerTray
{
    internal static class Program
    {
        private static Mutex? _mutex;

        [STAThread]
        static void Main()
        {
            bool createdNew;
            _mutex = new Mutex(true, "TimerTray_SingleInstance_Mutex", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("Aplikacja już działa w tle (zasobnik systemowy).", "TimerTray", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}
