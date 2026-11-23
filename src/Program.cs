using System;
using System.Threading;
using System.Windows.Forms;

namespace HotspotYue
{
    internal static class Program
    {
        private const string MutexName = "HotspotYue.SingleInstance.v1";
        private const string ShowEventName = "HotspotYue.ShowWindow.v1";

        [STAThread]
        private static void Main(string[] args)
        {
            bool startHidden = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--tray", StringComparison.OrdinalIgnoreCase))
                {
                    startHidden = true;
                }
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    SignalExistingInstance();
                    return;
                }

                EventWaitHandle showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                MainForm form = new MainForm(showEvent, startHidden);
                Application.Run(form);

                showEvent.Close();
                GC.KeepAlive(mutex);
            }
        }

        private static void SignalExistingInstance()
        {
            try
            {
                using (EventWaitHandle handle = EventWaitHandle.OpenExisting(ShowEventName))
                {
                    handle.Set();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("HotspotYue 已经在运行，请查看系统托盘。", "HotspotYue",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }
}