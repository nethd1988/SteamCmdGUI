using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;
using System.Security.Principal;

namespace SteamCmdServiceInstaller
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SteamCmdService Installer ===");
            Console.WriteLine("1. Cài đặt dịch vụ");
            Console.WriteLine("2. Gỡ cài đặt dịch vụ");
            Console.WriteLine("3. Khởi động dịch vụ");
            Console.WriteLine("4. Dừng dịch vụ");
            Console.WriteLine("5. Thoát");

            // Kiểm tra quyền admin
            if (!IsRunningAsAdmin())
            {
                Console.WriteLine("\nCẢNH BÁO: Chương trình cần quyền Administrator để cài đặt/gỡ cài đặt dịch vụ.");
                Console.WriteLine("Vui lòng chạy lại ứng dụng với quyền Administrator.\n");
            }

            while (true)
            {
                Console.Write("\nChọn thao tác (1-5): ");
                string choice = Console.ReadLine();

                switch (choice)
                {
                    case "1":
                        InstallService();
                        break;
                    case "2":
                        UninstallService();
                        break;
                    case "3":
                        StartService();
                        break;
                    case "4":
                        StopService();
                        break;
                    case "5":
                        return;
                    default:
                        Console.WriteLine("Lựa chọn không hợp lệ!");
                        break;
                }
            }
        }

        static void InstallService()
        {
            try
            {
                Console.WriteLine("Đang cài đặt dịch vụ...");

                // Lấy đường dẫn đến file SteamCmdService.exe
                string servicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SteamCmdService.exe");

                if (!File.Exists(servicePath))
                {
                    // Kiểm tra trong thư mục cha (nếu đang chạy từ thư mục Debug hoặc Release)
                    string parentDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName;
                    string alternativePath = Path.Combine(parentDir, "SteamCmdService.exe");

                    if (File.Exists(alternativePath))
                    {
                        servicePath = alternativePath;
                    }
                    else
                    {
                        // Tìm kiếm trong thư mục bin
                        string binPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin");
                        if (Directory.Exists(binPath))
                        {
                            foreach (string dir in Directory.GetDirectories(binPath, "*", SearchOption.AllDirectories))
                            {
                                string testPath = Path.Combine(dir, "SteamCmdService.exe");
                                if (File.Exists(testPath))
                                {
                                    servicePath = testPath;
                                    break;
                                }
                            }
                        }
                    }
                }

                if (!File.Exists(servicePath))
                {
                    Console.WriteLine($"CẢNH BÁO: Không tìm thấy file SteamCmdService.exe!");
                    Console.WriteLine($"Đường dẫn đã tìm: {servicePath}");
                    Console.WriteLine("Vui lòng đảm bảo file SteamCmdService.exe nằm trong cùng thư mục với installer");
                    Console.WriteLine("Hoặc nhập đường dẫn đầy đủ đến file:");

                    string customPath = Console.ReadLine();
                    if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                    {
                        servicePath = customPath;
                    }
                    else
                    {
                        Console.WriteLine("Không tìm thấy file. Hủy cài đặt.");
                        return;
                    }
                }

                Console.WriteLine($"Sử dụng file service: {servicePath}");

                // Cài đặt dịch vụ sử dụng InstallUtil.exe
                ManagedInstallerClass.InstallHelper(new string[] { servicePath });
                Console.WriteLine("Cài đặt dịch vụ thành công!");

                // Hỏi người dùng có muốn khởi động dịch vụ ngay không
                Console.Write("Bạn có muốn khởi động dịch vụ ngay bây giờ? (y/n): ");
                if (Console.ReadLine().ToLower() == "y")
                {
                    StartService();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi cài đặt dịch vụ: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void UninstallService()
        {
            try
            {
                Console.WriteLine("Đang gỡ cài đặt dịch vụ...");

                // Trước tiên, dừng dịch vụ nếu nó đang chạy
                try
                {
                    ServiceController controller = new ServiceController("SteamCmdService");
                    if (controller.Status != ServiceControllerStatus.Stopped)
                    {
                        Console.WriteLine("Đang dừng dịch vụ trước khi gỡ cài đặt...");
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                }
                catch (Exception)
                {
                    // Có thể dịch vụ không tồn tại, bỏ qua lỗi
                }

                string servicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SteamCmdService.exe");

                if (!File.Exists(servicePath))
                {
                    // Kiểm tra trong thư mục cha (nếu đang chạy từ thư mục Debug hoặc Release)
                    string parentDir = Directory.GetParent(AppDomain.CurrentDomain.BaseDirectory).FullName;
                    string alternativePath = Path.Combine(parentDir, "SteamCmdService.exe");

                    if (File.Exists(alternativePath))
                    {
                        servicePath = alternativePath;
                    }
                    else
                    {
                        Console.WriteLine("Không tìm thấy file SteamCmdService.exe. Vui lòng nhập đường dẫn đến file:");
                        string customPath = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(customPath) && File.Exists(customPath))
                        {
                            servicePath = customPath;
                        }
                        else
                        {
                            Console.WriteLine("Không tìm thấy file. Hủy gỡ cài đặt.");
                            return;
                        }
                    }
                }

                Console.WriteLine($"Sử dụng file service: {servicePath}");
                ManagedInstallerClass.InstallHelper(new string[] { "/u", servicePath });
                Console.WriteLine("Gỡ cài đặt dịch vụ thành công!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gỡ cài đặt dịch vụ: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
            }
        }

        static void StartService()
        {
            try
            {
                Console.WriteLine("Đang khởi động dịch vụ...");

                ServiceController controller = new ServiceController("SteamCmdService");
                if (controller.Status == ServiceControllerStatus.Running)
                {
                    Console.WriteLine("Dịch vụ đã đang chạy!");
                    return;
                }

                controller.Start();
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                Console.WriteLine("Dịch vụ đã được khởi động thành công!");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Không thể khởi động dịch vụ. Dịch vụ không tồn tại hoặc chưa được cài đặt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khởi động dịch vụ: {ex.Message}");
            }
        }

        static void StopService()
        {
            try
            {
                Console.WriteLine("Đang dừng dịch vụ...");

                ServiceController controller = new ServiceController("SteamCmdService");
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    Console.WriteLine("Dịch vụ đã dừng!");
                    return;
                }

                controller.Stop();
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                Console.WriteLine("Dịch vụ đã được dừng!");
            }
            catch (InvalidOperationException)
            {
                Console.WriteLine("Không thể dừng dịch vụ. Dịch vụ không tồn tại hoặc chưa được cài đặt.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi dừng dịch vụ: {ex.Message}");
            }
        }

        static bool IsRunningAsAdmin()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}