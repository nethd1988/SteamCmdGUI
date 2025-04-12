using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Threading;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using System.Text;
using System.Security.Cryptography;
using SteamCmdCommon;

namespace SteamCmdService
{
    public partial class SteamCmdService : ServiceBase
    {
        private Thread serviceThread;
        private Thread ipcThread;
        private bool isRunning = false;
        private string configFolder;
        private string logFile;
        private string bootstrapLogFile;
        private string encryptionKey = "yourEncryptionKey123!@#";

        public SteamCmdService()
        {
            InitializeComponent();

            string appPath = AppDomain.CurrentDomain.BaseDirectory;
            configFolder = Path.Combine(appPath, "Profiles");
            logFile = Path.Combine(appPath, "service_log.txt");
            bootstrapLogFile = Path.Combine(appPath, "logs", "bootstrap_log");

            // Đảm bảo thư mục cấu hình tồn tại
            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
            }
        }

        protected override void OnStart(string[] args)
        {
            LogToFile("Service starting...");

            isRunning = true;

            // Khởi động thread công việc chính
            serviceThread = new Thread(ServiceWorkerThread);
            serviceThread.IsBackground = true;
            serviceThread.Start();

            // Khởi động thread IPC
            ipcThread = new Thread(IpcListenerThread);
            ipcThread.IsBackground = true;
            ipcThread.Start();

            LogToFile("Service started successfully");
        }

        protected override void OnStop()
        {
            LogToFile("Service stopping...");
            isRunning = false;

            if (serviceThread != null)
            {
                serviceThread.Join(3000);
            }

            if (ipcThread != null)
            {
                ipcThread.Join(3000);
            }

            KillSteamProcesses();
            LogToFile("Service stopped");
        }

        private void ServiceWorkerThread()
        {
            LogToFile("Service worker thread started");

            while (isRunning)
            {
                try
                {
                    // Kiểm tra các hồ sơ và thực hiện các tác vụ tự động
                    Thread.Sleep(60000); // 1 phút
                }
                catch (Exception ex)
                {
                    LogToFile($"Error in service worker thread: {ex.Message}");
                }
            }
        }

        private void IpcListenerThread()
        {
            LogToFile("IPC listener thread started");

            while (isRunning)
            {
                try
                {
                    using (NamedPipeServerStream pipeServer = new NamedPipeServerStream("SteamCmdServicePipe", PipeDirection.InOut))
                    {
                        // Chờ kết nối
                        pipeServer.WaitForConnection();

                        // Nhận lệnh
                        BinaryFormatter formatter = new BinaryFormatter();
                        ServiceCommand command = (ServiceCommand)formatter.Deserialize(pipeServer);

                        // Xử lý lệnh
                        ServiceResponse response = ProcessCommand(command);

                        // Gửi phản hồi
                        formatter.Serialize(pipeServer, response);
                        pipeServer.Flush();
                    }
                }
                catch (Exception ex)
                {
                    LogToFile($"Error in IPC thread: {ex.Message}");
                    // Ngủ một chút trước khi thử lại
                    Thread.Sleep(1000);
                }
            }
        }

        private ServiceResponse ProcessCommand(ServiceCommand command)
        {
            try
            {
                LogToFile($"Received command: {command.CommandType}");

                switch (command.CommandType)
                {
                    case CommandType.GetStatus:
                        return new ServiceResponse { Success = true, Message = "Service is running" };

                    case CommandType.RunProfile:
                        // Logic để chạy một profile cụ thể
                        return RunSingleProfile(command.ProfileName);

                    case CommandType.RunAllProfiles:
                        // Logic để chạy tất cả profile
                        return RunAllProfiles();

                    case CommandType.StopProfile:
                        // Logic để dừng một profile đang chạy
                        return StopProfile(command.ProfileName);

                    case CommandType.UpdateConfig:
                        // Cập nhật cấu hình từ dữ liệu gửi đến
                        return UpdateConfiguration(command.Data);

                    default:
                        return new ServiceResponse { Success = false, Message = "Unknown command" };
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error processing command: {ex.Message}");
                return new ServiceResponse { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private ServiceResponse RunSingleProfile(string profileName)
        {
            // Triển khai sau
            return new ServiceResponse { Success = true, Message = $"Started profile {profileName}" };
        }

        private ServiceResponse RunAllProfiles()
        {
            // Triển khai sau
            return new ServiceResponse { Success = true, Message = "Started all profiles" };
        }

        private ServiceResponse StopProfile(string profileName)
        {
            // Triển khai sau
            return new ServiceResponse { Success = true, Message = $"Stopped profile {profileName}" };
        }

        private ServiceResponse UpdateConfiguration(string configData)
        {
            // Triển khai sau
            return new ServiceResponse { Success = true, Message = "Configuration updated" };
        }

        private void KillSteamProcesses()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("steam"))
                {
                    process.Kill();
                }

                foreach (Process process in Process.GetProcessesByName("steamcmd"))
                {
                    process.Kill();
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error killing processes: {ex.Message}");
            }
        }

        private void LogToFile(string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now}] {message}";
                File.AppendAllText(logFile, logMessage + Environment.NewLine);
            }
            catch
            {
                // Bỏ qua lỗi ghi log
            }
        }

        // Các phương thức mã hóa/giải mã
        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return "";
            byte[] clearBytes = Encoding.Unicode.GetBytes(plainText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(encryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private string DecryptString(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return "";
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(encryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    return Encoding.Unicode.GetString(ms.ToArray());
                }
            }
        }
    }
}