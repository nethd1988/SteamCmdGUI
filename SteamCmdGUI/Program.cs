using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace SteamCmdGUI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (!IsAdministrator())
            {
                DialogResult result = MessageBox.Show(
                    "Ứng dụng cần quyền Administrator để ghi file cấu hình và chạy SteamCMD.\n\nBạn có muốn chạy lại với quyền Administrator không?",
                    "Yêu cầu quyền Administrator",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    RestartAsAdmin();
                    return;
                }
            }

            Application.Run(new MainForm());
        }

        private static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdmin()
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.Verb = "runas";
            processInfo.FileName = Application.ExecutablePath;

            try
            {
                Process.Start(processInfo);
            }
            catch (Exception)
            {
                MessageBox.Show(
                    "Không thể chạy với quyền Administrator. Một số tính năng có thể không hoạt động.",
                    "Cảnh báo",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}