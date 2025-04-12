using System;
using System.Configuration.Install;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.ServiceProcess;

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

                string servicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SteamCmdService.exe");

                if (!File.Exists(servicePath))
                {
                    Console.WriteLine($"Không tìm thấy file {servicePath}");
                    return;
                }

                ManagedInstallerClass.InstallHelper(new string[] { servicePath });
                Console.WriteLine("Cài đặt dịch vụ thành công!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi cài đặt dịch vụ: {ex.Message}");
            }
        }

        static void UninstallService()
        {
            try
            {
                Console.WriteLine("Đang gỡ cài đặt dịch vụ...");

                string servicePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SteamCmdService.exe");

                if (!File.Exists(servicePath))
                {
                    Console.WriteLine($"Không tìm thấy file {servicePath}");
                    return;
                }

                ManagedInstallerClass.InstallHelper(new string[] { "/u", servicePath });
                Console.WriteLine("Gỡ cài đặt dịch vụ thành công!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi gỡ cài đặt dịch vụ: {ex.Message}");
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
                controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(10));
                Console.WriteLine("Dịch vụ đã được khởi động!");
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
                controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(10));
                Console.WriteLine("Dịch vụ đã được dừng!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi dừng dịch vụ: {ex.Message}");
            }
        }
    }
}