using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using System.Text;
using System.Security.Cryptography;
using System.Reflection;
using SteamCmdCommon;

namespace SteamCmdService
{
    /// <summary>
    /// Đây là ServiceBase cho ứng dụng SteamCmdService
    /// </summary>
    public partial class SteamCmdService : ServiceBase
    {
        private Thread serviceThread;
        private Thread ipcThread;
        private bool isRunning = false;
        private string configFolder;
        private string logFile;
        private string bootstrapLogFile;
        private string encryptionKey = "yourEncryptionKey123!@#";

        // Từ điển lưu trữ các profile đang chạy
        private Dictionary<string, Process> runningProcesses = new Dictionary<string, Process>();

        // Từ điển để lưu trữ thông tin lịch chạy cho các profile
        private Dictionary<string, ScheduleInfo> schedules = new Dictionary<string, ScheduleInfo>();

        // Cấu trúc dữ liệu cho lịch trình
        private struct ScheduleInfo
        {
            public DateTime NextRunTime;
            public int IntervalHours;
            public bool IsActive;
        }

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

            // Đảm bảo thư mục logs tồn tại
            string logsDir = Path.Combine(appPath, "logs");
            if (!Directory.Exists(logsDir))
            {
                Directory.CreateDirectory(logsDir);
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

            // Dừng tất cả các quy trình đang chạy
            StopAllRunningProcesses();

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
                    DateTime now = DateTime.Now;

                    // Kiểm tra các profile cần chạy theo lịch
                    foreach (var kvp in schedules)
                    {
                        string profileName = kvp.Key;
                        ScheduleInfo schedule = kvp.Value;

                        if (schedule.IsActive && now >= schedule.NextRunTime)
                        {
                            LogToFile($"Running scheduled profile: {profileName}");

                            // Chạy profile
                            Task.Run(() => RunProfileInternal(profileName));

                            // Cập nhật thời gian chạy tiếp theo
                            var updatedSchedule = schedule;
                            updatedSchedule.NextRunTime = now.AddHours(schedule.IntervalHours);
                            schedules[profileName] = updatedSchedule;

                            LogToFile($"Next scheduled run for {profileName}: {updatedSchedule.NextRunTime}");
                        }
                    }

                    // Ngủ 1 phút trước khi kiểm tra lại
                    Thread.Sleep(60000);
                }
                catch (Exception ex)
                {
                    LogToFile($"Error in service worker thread: {ex.Message}");
                    Thread.Sleep(60000);
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
                        var commandObj = formatter.Deserialize(pipeServer);

                        // Trích xuất thông tin từ object nhận được
                        CommandType commandType = (CommandType)GetPropertyValue(commandObj, "CommandType");
                        string profileName = (string)GetPropertyValue(commandObj, "ProfileName") ?? string.Empty;
                        string data = (string)GetPropertyValue(commandObj, "Data") ?? string.Empty;

                        // Xử lý lệnh
                        ServiceResponse response = ProcessCommand(commandType, profileName, data);

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

        private ServiceResponse ProcessCommand(CommandType commandType, string profileName, string data)
        {
            try
            {
                LogToFile($"Received command: {commandType}, Profile: {profileName}");

                switch (commandType)
                {
                    case CommandType.GetStatus:
                        return new ServiceResponse { Success = true, Message = "Service is running" };

                    case CommandType.RunProfile:
                        // Logic để chạy một profile cụ thể
                        return RunSingleProfile(profileName);

                    case CommandType.RunAllProfiles:
                        // Logic để chạy tất cả profile
                        return RunAllProfiles();

                    case CommandType.StopProfile:
                        // Logic để dừng một profile đang chạy
                        return StopProfile(profileName);

                    case CommandType.UpdateConfig:
                        // Cập nhật cấu hình từ dữ liệu gửi đến
                        return UpdateConfiguration(profileName, data);

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
            try
            {
                // Kiểm tra xem profile có tồn tại không
                string profilePath = Path.Combine(configFolder, SafeFileName(profileName) + ".profile");
                if (!File.Exists(profilePath))
                {
                    return new ServiceResponse { Success = false, Message = $"Profile not found: {profileName}" };
                }

                // Chạy profile trong một task riêng biệt
                Task.Run(() => RunProfileInternal(profileName));

                return new ServiceResponse { Success = true, Message = $"Started profile {profileName}" };
            }
            catch (Exception ex)
            {
                LogToFile($"Error starting profile {profileName}: {ex.Message}");
                return new ServiceResponse { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private ServiceResponse RunAllProfiles()
        {
            try
            {
                // Tìm tất cả file .profile trong thư mục cấu hình
                if (!Directory.Exists(configFolder))
                {
                    return new ServiceResponse { Success = false, Message = "Profile directory does not exist" };
                }

                string[] profileFiles = Directory.GetFiles(configFolder, "*.profile");
                if (profileFiles.Length == 0)
                {
                    return new ServiceResponse { Success = false, Message = "No profiles found" };
                }

                // Chạy từng profile
                foreach (string profileFile in profileFiles)
                {
                    string profileName = Path.GetFileNameWithoutExtension(profileFile);
                    Task.Run(() => RunProfileInternal(profileName));
                    LogToFile($"Queued profile {profileName} for execution");
                }

                return new ServiceResponse { Success = true, Message = $"Started {profileFiles.Length} profiles" };
            }
            catch (Exception ex)
            {
                LogToFile($"Error running all profiles: {ex.Message}");
                return new ServiceResponse { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private ServiceResponse StopProfile(string profileName)
        {
            try
            {
                lock (runningProcesses)
                {
                    if (runningProcesses.TryGetValue(profileName, out Process process))
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            LogToFile($"Stopped process for profile: {profileName}");
                        }
                        runningProcesses.Remove(profileName);
                        return new ServiceResponse { Success = true, Message = $"Stopped profile {profileName}" };
                    }
                    else
                    {
                        return new ServiceResponse { Success = false, Message = $"Profile {profileName} is not running" };
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error stopping profile {profileName}: {ex.Message}");
                return new ServiceResponse { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private ServiceResponse UpdateConfiguration(string profileName, string configData)
        {
            try
            {
                LogToFile($"Updating configuration for profile: {profileName}");

                // Phân tích dữ liệu cấu hình
                if (string.IsNullOrEmpty(configData))
                {
                    return new ServiceResponse { Success = false, Message = "No configuration data provided" };
                }

                // Deserialize profile từ XML
                GameProfile profile;
                using (StringReader reader = new StringReader(configData))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                    profile = (GameProfile)serializer.Deserialize(reader);
                }

                if (profile == null)
                {
                    return new ServiceResponse { Success = false, Message = "Invalid profile data" };
                }

                // Lưu profile
                string profilePath = Path.Combine(configFolder, SafeFileName(profileName) + ".profile");
                profile.SaveToFile(profilePath);
                LogToFile($"Profile {profileName} configuration updated");

                // Kiểm tra xem dữ liệu có chứa thông tin lịch trình không (đơn giản hóa)
                // Trong thực tế, bạn sẽ cần phân tích dữ liệu này từ command.Data
                // Tạm thời mặc định là chạy mỗi 1 giờ
                ScheduleInfo schedule = new ScheduleInfo
                {
                    NextRunTime = DateTime.Now.AddHours(1),
                    IntervalHours = 1,
                    IsActive = true
                };

                // Cập nhật lịch trình
                lock (schedules)
                {
                    schedules[profileName] = schedule;
                }

                LogToFile($"Schedule for {profileName} updated: Next run at {schedule.NextRunTime}, interval: {schedule.IntervalHours}h");

                return new ServiceResponse { Success = true, Message = "Configuration updated" };
            }
            catch (Exception ex)
            {
                LogToFile($"Error updating configuration: {ex.Message}");
                return new ServiceResponse { Success = false, Message = $"Error: {ex.Message}" };
            }
        }

        private void RunProfileInternal(string profileName)
        {
            try
            {
                // Đọc profile từ file
                string profilePath = Path.Combine(configFolder, SafeFileName(profileName) + ".profile");
                if (!File.Exists(profilePath))
                {
                    LogToFile($"Profile file not found: {profilePath}");
                    return;
                }

                GameProfile profile = GameProfile.LoadFromFile(profilePath);
                if (profile == null)
                {
                    LogToFile($"Failed to load profile: {profileName}");
                    return;
                }

                LogToFile($"Running profile: {profileName}, AppID: {profile.AppID}");

                // Chuẩn bị môi trường
                KillSteamProcesses();
                string steamCmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamcmd.exe");

                // Tải steamcmd.exe nếu chưa có
                if (!File.Exists(steamCmdPath))
                {
                    LogToFile("SteamCMD not found, downloading...");
                    DownloadSteamCmd(steamCmdPath).Wait();
                }

                // Giải mã thông tin đăng nhập
                string username = DecryptString(profile.EncryptedUsername);
                string password = DecryptString(profile.EncryptedPassword);

                // Đảm bảo thư mục cài đặt tồn tại
                if (!Directory.Exists(profile.InstallDir))
                {
                    Directory.CreateDirectory(profile.InstallDir);
                }

                // Tạo symlink cho thư mục steamapps
                SetupSteamAppsDirectory(profile.InstallDir);

                // Tạo lệnh SteamCMD
                string arguments = $"+login {username} {password} +app_update {profile.AppID} {profile.Arguments} +quit";

                // Tạo và cấu hình process
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = steamCmdPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };

                // Chạy SteamCMD
                using (Process process = new Process())
                {
                    process.StartInfo = processInfo;
                    StringBuilder outputBuilder = new StringBuilder();

                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine(e.Data);
                            LogToFile($"[{profileName}] {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            outputBuilder.AppendLine($"ERROR: {e.Data}");
                            LogToFile($"[{profileName}] ERROR: {e.Data}");
                        }
                    };

                    // Lưu process vào từ điển để có thể điều khiển sau này
                    lock (runningProcesses)
                    {
                        if (runningProcesses.ContainsKey(profileName))
                        {
                            StopProfile(profileName);
                        }
                        runningProcesses[profileName] = process;
                    }

                    LogToFile($"Starting SteamCMD for profile: {profileName}");
                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // Chờ quá trình hoàn thành
                    process.WaitForExit();

                    // Ghi log kết quả
                    int exitCode = process.ExitCode;
                    LogToFile($"SteamCMD for profile {profileName} completed with exit code: {exitCode}");

                    // Ghi log output
                    string outputLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"steamcmd_{profileName}.log");
                    File.WriteAllText(outputLogPath, outputBuilder.ToString());

                    // Dọn dẹp
                    lock (runningProcesses)
                    {
                        runningProcesses.Remove(profileName);
                    }
                }

                // Dọn dẹp symlink
                CleanupSteamAppsDirectory();

                LogToFile($"Profile {profileName} execution completed");
            }
            catch (Exception ex)
            {
                LogToFile($"Error running profile {profileName}: {ex.Message}");
                // Dọn dẹp trong trường hợp lỗi
                lock (runningProcesses)
                {
                    runningProcesses.Remove(profileName);
                }
                CleanupSteamAppsDirectory();
            }
        }

        private async Task DownloadSteamCmd(string steamCmdPath)
        {
            try
            {
                string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamcmd.zip");

                using (var client = new System.Net.WebClient())
                {
                    LogToFile("Downloading SteamCMD...");
                    await client.DownloadFileTaskAsync(new Uri("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"), zipPath);

                    LogToFile("Extracting SteamCMD...");
                    System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, AppDomain.CurrentDomain.BaseDirectory);

                    File.Delete(zipPath);
                    LogToFile("SteamCMD download completed");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error downloading SteamCMD: {ex.Message}");
                throw;
            }
        }

        private void SetupSteamAppsDirectory(string installDir)
        {
            try
            {
                // Tạo thư mục steamapps trong thư mục cài đặt nếu chưa có
                string installSteamAppsDir = Path.Combine(installDir, "steamapps");
                if (!Directory.Exists(installSteamAppsDir))
                {
                    Directory.CreateDirectory(installSteamAppsDir);
                    LogToFile($"Created steamapps directory: {installSteamAppsDir}");
                }

                // Tạo symlink từ thư mục steamapps của SteamCMD đến thư mục cài đặt
                string localSteamAppsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamapps");
                if (Directory.Exists(localSteamAppsDir))
                {
                    Directory.Delete(localSteamAppsDir, true);
                }

                // Tạo symlink bằng cmd
                ProcessStartInfo linkInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C mklink /D \"{localSteamAppsDir}\" \"{installSteamAppsDir}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process linkProcess = Process.Start(linkInfo))
                {
                    linkProcess.WaitForExit();
                    LogToFile($"Created symbolic link from {installSteamAppsDir} to {localSteamAppsDir}");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error setting up steamapps directory: {ex.Message}");
            }
        }

        private void CleanupSteamAppsDirectory()
        {
            try
            {
                string localSteamAppsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamapps");
                if (Directory.Exists(localSteamAppsDir))
                {
                    Directory.Delete(localSteamAppsDir);
                    LogToFile("Removed steamapps symbolic link");
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error cleaning up steamapps directory: {ex.Message}");
            }
        }

        private void StopAllRunningProcesses()
        {
            lock (runningProcesses)
            {
                foreach (var kvp in runningProcesses)
                {
                    try
                    {
                        Process process = kvp.Value;
                        if (process != null && !process.HasExited)
                        {
                            process.Kill();
                            LogToFile($"Killed process for profile: {kvp.Key}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"Error killing process for profile {kvp.Key}: {ex.Message}");
                    }
                }
                runningProcesses.Clear();
            }
        }

        private void KillSteamProcesses()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("steam"))
                {
                    process.Kill();
                    LogToFile("Killed steam.exe process");
                }

                foreach (Process process in Process.GetProcessesByName("steamcmd"))
                {
                    process.Kill();
                    LogToFile("Killed steamcmd.exe process");
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

        private string SafeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
            {
                fileName = fileName.Replace(c, '_');
            }
            return fileName;
        }

        // Helper method để trích xuất giá trị thuộc tính từ object
        private object GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                return obj.GetType().GetProperty(propertyName)?.GetValue(obj);
            }
            catch
            {
                return null;
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