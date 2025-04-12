using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Xml.Serialization;

namespace SteamCmdService
{
    [Serializable]
    public class GameProfile
    {
        public string ProfileName { get; set; }
        public string InstallDir { get; set; }
        public string EncryptedUsername { get; set; }
        public string EncryptedPassword { get; set; }
        public string AppID { get; set; }
        public string Arguments { get; set; }

        public GameProfile()
        {
            ProfileName = "";
            InstallDir = "";
            EncryptedUsername = "";
            EncryptedPassword = "";
            AppID = "";
            Arguments = "-norepairfiles -noverifyfiles";
        }

        public void SaveToFile(string filePath)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                serializer.Serialize(writer, this);
            }
        }

        public static GameProfile LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return null;
            XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
            using (StreamReader reader = new StreamReader(filePath))
            {
                return (GameProfile)serializer.Deserialize(reader);
            }
        }
    }

    public partial class SteamCmdService : ServiceBase
    {
        private readonly string configFolder;
        private readonly string logFile;
        private readonly string bootstrapLogFile;
        private readonly string encryptionKey = "yourEncryptionKey123!@#";
        private List<GameProfile> profiles = new List<GameProfile>();
        private GameProfile currentProfile = null;
        private System.Timers.Timer autoRunTimer;
        private int currentProfileIndex = 0;
        private bool isRunning = false;
        private Process currentProcess = null;
        private bool runAllProfiles = false;
        private bool cancelAutoRun = false;
        private TcpListener serviceListener;
        private Thread listenerThread;
        private bool stopListening = false;
        private int port = 61188; // Port for communication with GUI
        private StringBuilder logBuffer = new StringBuilder();

        public SteamCmdService()
        {
            InitializeComponent();

            // Set service properties
            ServiceName = "SteamCmdService";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            configFolder = Path.Combine(appDirectory, "Profiles");
            logFile = Path.Combine(appDirectory, "steamcmd_service_log.txt");
            bootstrapLogFile = Path.Combine(appDirectory, "logs", "bootstrap_log");

            LogMessage("Service constructor executed");
        }

        protected override void OnStart(string[] args)
        {
            LogMessage("Service starting...");

            // Create required directories
            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
            }

            // Kill any existing Steam processes
            KillSteamAndSteamCmdProcesses();

            // Load profiles from the storage
            LoadProfiles();

            // Setup the auto-run timer
            SetupAutoRunTimer();

            // Start the TCP listener for GUI communications
            StartTcpListener();

            LogMessage("Service started successfully");
        }

        protected override void OnStop()
        {
            LogMessage("Service stopping...");

            // Stop the listener thread
            stopListening = true;
            serviceListener?.Stop();
            listenerThread?.Join(5000);

            // Cancel any running operations
            cancelAutoRun = true;

            // Stop the timer
            if (autoRunTimer != null)
            {
                autoRunTimer.Stop();
                autoRunTimer.Dispose();
            }

            // Cleanup any processes
            KillSteamAndSteamCmdProcesses();
            RemoveSteamAppsSymLink();

            LogMessage("Service stopped");
        }

        private void StartTcpListener()
        {
            try
            {
                stopListening = false;
                serviceListener = new TcpListener(IPAddress.Any, port);
                serviceListener.Start();

                LogMessage($"TCP listener started on port {port}");

                // Start a thread to handle client connections
                listenerThread = new Thread(ListenForClients)
                {
                    IsBackground = true
                };
                listenerThread.Start();
            }
            catch (Exception ex)
            {
                LogMessage($"Error starting TCP listener: {ex.Message}");
            }
        }

        private void ListenForClients()
        {
            LogMessage("Listening for client connections...");

            while (!stopListening)
            {
                try
                {
                    if (serviceListener.Pending())
                    {
                        TcpClient client = serviceListener.AcceptTcpClient();
                        ThreadPool.QueueUserWorkItem(HandleClientRequest, client);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    if (!stopListening)
                    {
                        LogMessage($"Error in client listener: {ex.Message}");
                    }
                }
            }
        }

        private void HandleClientRequest(object clientObj)
        {
            TcpClient client = (TcpClient)clientObj;

            try
            {
                using (client)
                {
                    NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[4096];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    string request = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                    LogMessage($"Received request: {request}");

                    // Process the request
                    if (request.StartsWith("GET_PROFILES"))
                    {
                        SendProfilesToClient(stream);
                    }
                    else if (request.StartsWith("GET_PROFILE_DETAILS"))
                    {
                        string profileName = request.Substring("GET_PROFILE_DETAILS ".Length);
                        SendProfileDetails(stream, profileName);
                    }
                    else if (request.StartsWith("SAVE_PROFILE"))
                    {
                        // Get the XML after the command
                        string profileXml = request.Substring("SAVE_PROFILE ".Length);
                        SaveProfileFromClient(stream, profileXml);
                    }
                    else if (request.StartsWith("DELETE_PROFILE"))
                    {
                        string profileName = request.Substring("DELETE_PROFILE ".Length);
                        DeleteProfile(stream, profileName);
                    }
                    else if (request.StartsWith("RUN_PROFILE"))
                    {
                        string profileName = request.Substring("RUN_PROFILE ".Length);
                        RunProfile(stream, profileName);
                    }
                    else if (request.StartsWith("RUN_ALL"))
                    {
                        RunAllProfiles(stream);
                    }
                    else if (request.StartsWith("STOP_OPERATION"))
                    {
                        StopOperation(stream);
                    }
                    else if (request.StartsWith("GET_STATUS"))
                    {
                        SendStatus(stream);
                    }
                    else if (request.StartsWith("GET_LOGS"))
                    {
                        SendLogs(stream);
                    }
                    else if (request.StartsWith("SET_TIMER"))
                    {
                        string value = request.Substring("SET_TIMER ".Length);
                        SetAutoRunTimer(stream, value);
                    }
                    else if (request.StartsWith("TOGGLE_AUTORUN"))
                    {
                        string value = request.Substring("TOGGLE_AUTORUN ".Length);
                        ToggleAutoRun(stream, value);
                    }
                    else
                    {
                        byte[] response = Encoding.UTF8.GetBytes("INVALID_REQUEST");
                        stream.Write(response, 0, response.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error handling client request: {ex.Message}");
            }
        }

        private void SendProfilesToClient(NetworkStream stream)
        {
            try
            {
                if (profiles.Count == 0)
                {
                    byte[] response = Encoding.UTF8.GetBytes("NO_PROFILES");
                    stream.Write(response, 0, response.Length);
                    return;
                }

                string profileNames = string.Join(",", profiles.Select(p => p.ProfileName));
                byte[] response2 = Encoding.UTF8.GetBytes(profileNames);
                stream.Write(response2, 0, response2.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending profiles to client: {ex.Message}");
            }
        }

        private void SendProfileDetails(NetworkStream stream, string profileName)
        {
            try
            {
                GameProfile profile = profiles.FirstOrDefault(p => p.ProfileName == profileName);

                if (profile == null)
                {
                    byte[] response = Encoding.UTF8.GetBytes("PROFILE_NOT_FOUND");
                    stream.Write(response, 0, response.Length);
                    return;
                }

                XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                using (StringWriter writer = new StringWriter())
                {
                    serializer.Serialize(writer, profile);
                    string xmlProfile = writer.ToString();
                    byte[] response = Encoding.UTF8.GetBytes(xmlProfile);
                    stream.Write(response, 0, response.Length);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending profile details: {ex.Message}");
            }
        }

        private void SaveProfileFromClient(NetworkStream stream, string profileXml)
        {
            try
            {
                XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                using (StringReader reader = new StringReader(profileXml))
                {
                    GameProfile profile = (GameProfile)serializer.Deserialize(reader);

                    // Check if profile already exists
                    int existingIndex = profiles.FindIndex(p => p.ProfileName == profile.ProfileName);
                    if (existingIndex >= 0)
                    {
                        profiles[existingIndex] = profile;
                    }
                    else
                    {
                        profiles.Add(profile);
                    }

                    // Save to file
                    profile.SaveToFile(Path.Combine(configFolder, SafeFileName(profile.ProfileName) + ".profile"));

                    byte[] response = Encoding.UTF8.GetBytes("PROFILE_SAVED");
                    stream.Write(response, 0, response.Length);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error saving profile from client: {ex.Message}");
                byte[] response = Encoding.UTF8.GetBytes("ERROR: " + ex.Message);
                stream.Write(response, 0, response.Length);
            }
        }

        private void DeleteProfile(NetworkStream stream, string profileName)
        {
            try
            {
                GameProfile profile = profiles.FirstOrDefault(p => p.ProfileName == profileName);

                if (profile == null)
                {
                    byte[] response = Encoding.UTF8.GetBytes("PROFILE_NOT_FOUND");
                    stream.Write(response, 0, response.Length);
                    return;
                }

                string filePath = Path.Combine(configFolder, SafeFileName(profile.ProfileName) + ".profile");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                profiles.Remove(profile);

                byte[] successResponse = Encoding.UTF8.GetBytes("PROFILE_DELETED");
                stream.Write(successResponse, 0, successResponse.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"Error deleting profile: {ex.Message}");
                byte[] response = Encoding.UTF8.GetBytes("ERROR: " + ex.Message);
                stream.Write(response, 0, response.Length);
            }
        }

        private async void RunProfile(NetworkStream stream, string profileName)
        {
            try
            {
                if (isRunning)
                {
                    byte[] response = Encoding.UTF8.GetBytes("ALREADY_RUNNING");
                    stream.Write(response, 0, response.Length);
                    return;
                }

                GameProfile profile = profiles.FirstOrDefault(p => p.ProfileName == profileName);

                if (profile == null)
                {
                    byte[] response = Encoding.UTF8.GetBytes("PROFILE_NOT_FOUND");
                    stream.Write(response, 0, response.Length);
                    return;
                }

                currentProfile = profile;
                isRunning = true;
                cancelAutoRun = false;

                byte[] startResponse = Encoding.UTF8.GetBytes("OPERATION_STARTED");
                stream.Write(startResponse, 0, startResponse.Length);

                // Run the profile in a separate task
                Task.Run(async () =>
                {
                    try
                    {
                        await RunSteamCmdAsync();
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error running profile {profileName}: {ex.Message}");
                    }
                    finally
                    {
                        isRunning = false;
                    }
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Error initiating profile run: {ex.Message}");
                byte[] response = Encoding.UTF8.GetBytes("ERROR: " + ex.Message);
                stream.Write(response, 0, response.Length);
                isRunning = false;
            }
        }

        private void RunAllProfiles(NetworkStream stream)
        {
            try
            {
                if (isRunning)
                {
                    byte[] response = Encoding.UTF8.GetBytes("ALREADY_RUNNING");
                    stream.Write(response, 0, response.Length);
                    return;
                }

                if (profiles.Count == 0)
                {
                    byte[] response = Encoding.UTF8.GetBytes("NO_PROFILES");
                    stream.Write(response, 0, response.Length);
                    return;
                }

                runAllProfiles = true;
                cancelAutoRun = false;
                currentProfileIndex = 0;

                byte[] startResponse = Encoding.UTF8.GetBytes("OPERATION_STARTED");
                stream.Write(startResponse, 0, startResponse.Length);

                // Run all profiles in a separate task
                Task.Run(async () => await RunAllProfilesAsync());
            }
            catch (Exception ex)
            {
                LogMessage($"Error initiating run all profiles: {ex.Message}");
                byte[] response = Encoding.UTF8.GetBytes("ERROR: " + ex.Message);
                stream.Write(response, 0, response.Length);
            }
        }

        private void StopOperation(NetworkStream stream)
        {
            try
            {
                cancelAutoRun = true;

                if (currentProcess != null && !currentProcess.HasExited)
                {
                    try
                    {
                        currentProcess.Kill();
                        currentProcess.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error stopping process: {ex.Message}");
                    }
                }

                byte[] response = Encoding.UTF8.GetBytes("OPERATION_STOPPED");
                stream.Write(response, 0, response.Length);

                RemoveSteamAppsSymLink();
                KillSteamAndSteamCmdProcesses();
                isRunning = false;
                currentProcess = null;
            }
            catch (Exception ex)
            {
                LogMessage($"Error stopping operation: {ex.Message}");
                byte[] response = Encoding.UTF8.GetBytes("ERROR: " + ex.Message);
                stream.Write(response, 0, response.Length);
            }
        }

        private void SendStatus(NetworkStream stream)
        {
            try
            {
                string status = $"RUNNING={isRunning};CURRENT_PROFILE={currentProfile?.ProfileName ?? "None"};";

                if (runAllProfiles)
                {
                    status += $"RUN_ALL=true;CURRENT_INDEX={currentProfileIndex};TOTAL_PROFILES={profiles.Count};";
                }
                else
                {
                    status += "RUN_ALL=false;";
                }

                byte[] response = Encoding.UTF8.GetBytes(status);
                stream.Write(response, 0, response.Length);
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending status: {ex.Message}");
            }
        }

        private void SendLogs(NetworkStream stream)
        {
            try
            {
                string logs = logBuffer.ToString();
                byte[] response = Encoding.UTF8.GetBytes(logs);
                stream.Write(response, 0, response.Length);

                // Clear the log buffer after sending
                logBuffer.Clear();
            }
            catch (Exception ex)
            {
                LogMessage($"Error sending logs: {ex.Message}");
            }
        }

        private void SetAutoRunTimer(NetworkStream stream, string value)
        {
            try
            {
                if (int.TryParse(value, out int hours))
                {
                    if (autoRunTimer != null)
                    {
                        autoRunTimer.Interval = hours == 0 ? 1000 : hours * 60 * 60 * 1000;

                        byte[] response = Encoding.UTF8.GetBytes("TIMER_SET");
                        stream.Write(response, 0, response.Length);
                    }
                    else
                    {
                        byte[] response = Encoding.UTF8.GetBytes("TIMER_NOT_INITIALIZED");
                        stream.Write(response, 0, response.Length);
                    }
                }
                else
                {
                    byte[] response = Encoding.UTF8.GetBytes("INVALID_VALUE");
                    stream.Write(response, 0, response.Length);
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error setting timer: {ex.Message}");
                byte[] response = Encoding.UTF8.GetBytes("ERROR: " + ex.Message);
                stream.Write(response, 0, response.Length);
            }
        }

        private void ToggleAutoRun(NetworkStream stream, string value)
        {
            try
            {
                bool enable = value.ToLower() == "true";

                if (enable)
                {
                    if (profiles.Count == 0)
                    {
                        byte[] response = Encoding.UTF8.GetBytes("NO_PROFILES");
                        stream.Write(response, 0, response.Length);
                        return;
                    }

                    if (autoRunTimer != null)
                    {
                        autoRunTimer.Start();
                        byte[] response = Encoding.UTF8.GetBytes("AUTORUN_ENABLED");
                        stream.Write(response, 0, response.Length);
                    }
                    else
                    {
                        byte[] response = Encoding.UTF8.GetBytes("TIMER_NOT_INITIALIZED");
                        stream.Write(response, 0, response.Length);
                    }
                }
                else
                {
                    if (autoRunTimer != null)
                    {
                        autoRunTimer.Stop();
                        cancelAutoRun = true;
                        byte[] response = Encoding.UTF8.GetBytes("AUTORUN_DISABLED");
                        stream.Write(response, 0, response.Length);
                    }
                    else
                    {
                        byte[] response = Encoding.UTF8.GetBytes("TIMER_NOT_INITIALIZED");
                        stream.Write(response, 0, response.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error toggling autorun: {ex.Message}");
                byte[] response = Encoding.UTF8.GetBytes("ERROR: " + ex.Message);
                stream.Write(response, 0, response.Length);
            }
        }

        private void SetupAutoRunTimer()
        {
            autoRunTimer = new System.Timers.Timer();
            autoRunTimer.Interval = 60 * 60 * 1000; // Default 1 hour
            autoRunTimer.Elapsed += async (s, e) =>
            {
                if (profiles.Count == 0 || isRunning) return;

                LogMessage("Auto-run timer triggered");

                if (autoRunTimer.Interval == 1000) // 0 hours setting
                {
                    autoRunTimer.Stop();
                    await RunAllProfilesAsync();
                }
                else
                {
                    await RunAllProfilesAsync();
                }
            };
            autoRunTimer.AutoReset = true;
        }

        private async Task RunAllProfilesAsync()
        {
            if (profiles.Count == 0 || isRunning) return;

            runAllProfiles = true;
            cancelAutoRun = false;
            currentProfileIndex = 0;
            LogMessage("Starting to run all profiles");

            while (currentProfileIndex < profiles.Count && !cancelAutoRun)
            {
                GameProfile profileToRun = profiles[currentProfileIndex];
                currentProfile = profileToRun;
                LogMessage($"Running profile ({currentProfileIndex + 1}/{profiles.Count}): {profileToRun.ProfileName}");
                isRunning = true;

                try
                {
                    await RunSteamCmdAsync();
                }
                catch (Exception ex)
                {
                    LogMessage($"Error running profile {profileToRun.ProfileName}: {ex.Message}");
                    LogMessage("Skipping and continuing...");
                }

                isRunning = false;
                currentProfileIndex++;
                await Task.Delay(2000);
            }

            runAllProfiles = false;
            LogMessage("Completed running all profiles");

            if (autoRunTimer.Interval == 1000) // If this was a "run once" operation
            {
                // Don't restart timer
            }
        }

        private void LoadProfiles()
        {
            profiles.Clear();

            if (Directory.Exists(configFolder))
            {
                string[] profileFiles = Directory.GetFiles(configFolder, "*.profile");
                foreach (string file in profileFiles)
                {
                    try
                    {
                        GameProfile profile = GameProfile.LoadFromFile(file);
                        if (profile != null)
                        {
                            profiles.Add(profile);
                            LogMessage($"Loaded profile: {profile.ProfileName}");
                        }
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error loading profile from file {file}: {ex.Message}");
                    }
                }
            }

            LogMessage($"Loaded {profiles.Count} profiles");
        }

        private async Task RunSteamCmdAsync()
        {
            try
            {
                LogMessage($"Starting SteamCMD for profile: {currentProfile.ProfileName}");

                KillSteamAndSteamCmdProcesses();
                DeleteLocalSteamAppsFolder();

                string steamCmdPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamcmd.exe");
                if (!File.Exists(steamCmdPath))
                {
                    LogMessage("SteamCMD not found. Downloading...");

                    using (var client = new WebClient())
                    {
                        string zipPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamcmd.zip");
                        await client.DownloadFileTaskAsync(new Uri("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"), zipPath);

                        LogMessage("SteamCMD downloaded. Extracting...");

                        using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Open))
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string destinationPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, entry.FullName);
                                string destinationDir = Path.GetDirectoryName(destinationPath);
                                if (!Directory.Exists(destinationDir)) Directory.CreateDirectory(destinationDir);

                                using (Stream entryStream = entry.Open())
                                using (FileStream fileStream = File.Create(destinationPath))
                                    await entryStream.CopyToAsync(fileStream);
                            }
                        }

                        File.Delete(zipPath);

                        if (!File.Exists(steamCmdPath))
                        {
                            throw new Exception("Unable to extract steamcmd.exe");
                        }

                        LogMessage("SteamCMD extracted successfully");
                    }
                }

                if (!Directory.Exists(currentProfile.InstallDir))
                {
                    LogMessage($"Creating installation directory: {currentProfile.InstallDir}");
                    Directory.CreateDirectory(currentProfile.InstallDir);
                }

                CreateSteamAppsSymLink(currentProfile.InstallDir);

                string usernameToUse = DecryptString(currentProfile.EncryptedUsername);
                string passwordToUse = DecryptString(currentProfile.EncryptedPassword);

                if (usernameToUse.Length > 0)
                {
                    LogMessage($"Using login: {usernameToUse.Substring(0, Math.Min(2, usernameToUse.Length))}*** (length: {usernameToUse.Length})");
                }
                else
                {
                    LogMessage($"WARNING: Empty username!");
                }

                string arguments = $"+login {usernameToUse} {passwordToUse} +app_update {currentProfile.AppID} {currentProfile.Arguments} +quit";

                if (File.Exists(logFile)) File.Delete(logFile);
                if (File.Exists(bootstrapLogFile)) File.Delete(bootstrapLogFile);

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = steamCmdPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                StringBuilder outputLog = new StringBuilder();
                bool loginFailed = false;
                bool operationFailed = false;
                string errorMessage = "";
                int retryCount = 0;
                const int maxRetries = 2;

                while (retryCount <= maxRetries && !cancelAutoRun)
                {
                    LogMessage($"Running SteamCMD (attempt {retryCount + 1}/{maxRetries + 1})");

                    await Task.Run(() =>
                    {
                        using (currentProcess = new Process())
                        {
                            currentProcess.StartInfo = startInfo;
                            currentProcess.OutputDataReceived += (s, args) =>
                            {
                                if (args.Data != null)
                                {
                                    outputLog.AppendLine(args.Data);
                                    LogMessage(args.Data);

                                    if (args.Data.Contains("Success!"))
                                    {
                                        LogMessage($"Successfully updated: {currentProfile.ProfileName}");
                                    }
                                    else if (args.Data.Contains("FAILED login") && args.Data.ToLower().Contains("password"))
                                    {
                                        loginFailed = true;
                                        errorMessage = "Incorrect password!";
                                        LogMessage("Authentication error: Incorrect password");
                                    }
                                    else if (args.Data.Contains("Steam Guard"))
                                    {
                                        loginFailed = true;
                                        errorMessage = "Steam Guard code required!";
                                        LogMessage("Authentication error: Steam Guard code required");
                                    }
                                    else if (args.Data.Contains("ERROR") || args.Data.Contains("FAILED"))
                                    {
                                        operationFailed = true;
                                        if (string.IsNullOrEmpty(errorMessage)) errorMessage = args.Data;
                                    }
                                }
                            };

                            currentProcess.ErrorDataReceived += (s, args) =>
                            {
                                if (args.Data != null)
                                {
                                    outputLog.AppendLine("ERROR: " + args.Data);
                                    LogMessage("ERROR: " + args.Data);
                                    operationFailed = true;
                                    if (string.IsNullOrEmpty(errorMessage)) errorMessage = args.Data;
                                }
                            };

                            currentProcess.Start();
                            currentProcess.BeginOutputReadLine();
                            currentProcess.BeginErrorReadLine();
                            currentProcess.WaitForExit();
                            File.WriteAllText(logFile, outputLog.ToString());
                        }
                        currentProcess = null;
                    });

                    if (File.Exists(bootstrapLogFile)) File.Delete(bootstrapLogFile);

                    if (cancelAutoRun) break;

                    if (loginFailed)
                    {
                        LogMessage($"Login failed for {currentProfile.ProfileName}: {errorMessage}");
                        break;
                    }
                    else if (operationFailed)
                    {
                        LogMessage($"Operation failed for {currentProfile.ProfileName}: {errorMessage}");
                        break;
                    }
                    else break;

                    retryCount++;
                }

                FixNestedSteamAppsFolder(currentProfile.InstallDir);

                if (loginFailed && retryCount > maxRetries)
                {
                    LogMessage($"Authentication failed after {maxRetries} attempts! {errorMessage}");
                }
                else if (operationFailed)
                {
                    LogMessage($"Installation error! {errorMessage}");
                }
                else if (!cancelAutoRun)
                {
                    LogMessage("SteamCMD completed successfully!");
                }
                else
                {
                    LogMessage("Operation manually stopped.");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error running SteamCMD: {ex.Message}");
                throw;
            }
            finally
            {
                RemoveSteamAppsSymLink();
                KillSteamAndSteamCmdProcesses();
                isRunning = false;
                currentProcess = null;
            }
        }

        private void LogMessage(string message)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logMessage = $"[{timestamp}] {message}";

                // Add to in-memory buffer for GUI requests
                lock (logBuffer)
                {
                    // Keep a reasonable buffer size (last 500 lines)
                    if (logBuffer.Length > 50000)
                    {
                        int newLinePos = logBuffer.ToString().IndexOf(Environment.NewLine, 5000);
                        if (newLinePos > 0)
                        {
                            logBuffer.Remove(0, newLinePos + Environment.NewLine.Length);
                        }
                    }

                    logBuffer.AppendLine(logMessage);
                }

                // Also write to file log
                File.AppendAllText(logFile, logMessage + Environment.NewLine);

                // Write to Windows Event Log
                EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Information);
            }
            catch
            {
                // Ignore logging errors to prevent cascading failures
            }
        }

        private void KillSteamAndSteamCmdProcesses()
        {
            try
            {
                foreach (Process process in Process.GetProcessesByName("steam"))
                {
                    if (!process.HasExited)
                    {
                        LogMessage($"Closing steam.exe process (PID: {process.Id})...");
                        process.Kill();
                        process.WaitForExit(5000);
                        LogMessage(process.HasExited ? "Successfully closed steam.exe." : "Unable to close steam.exe.");
                    }
                }

                foreach (Process process in Process.GetProcessesByName("steamcmd"))
                {
                    if (!process.HasExited)
                    {
                        LogMessage($"Closing steamcmd.exe process (PID: {process.Id})...");
                        process.Kill();
                        process.WaitForExit(5000);
                        LogMessage(process.HasExited ? "Successfully closed steamcmd.exe." : "Unable to close steamcmd.exe.");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error closing processes: {ex.Message}");
            }
        }

        private void DeleteLocalSteamAppsFolder()
        {
            string localSteamAppsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamapps");
            try
            {
                if (Directory.Exists(localSteamAppsPath))
                {
                    Directory.Delete(localSteamAppsPath, true);
                    LogMessage($"Deleted steamapps folder at {localSteamAppsPath}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error deleting steamapps folder at {localSteamAppsPath}: {ex.Message}");
                throw;
            }
        }

        private void CreateSteamAppsSymLink(string gameInstallDir)
        {
            string gameSteamAppsPath = Path.Combine(gameInstallDir, "steamapps");
            string localSteamAppsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamapps");
            try
            {
                if (!Directory.Exists(gameSteamAppsPath))
                {
                    Directory.CreateDirectory(gameSteamAppsPath);
                    LogMessage($"Created steamapps folder at {gameSteamAppsPath}");
                }

                if (Directory.Exists(localSteamAppsPath))
                {
                    Directory.Delete(localSteamAppsPath, true);
                }

                ProcessStartInfo mklinkInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C mklink /D \"{localSteamAppsPath}\" \"{gameSteamAppsPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using (Process mklinkProcess = Process.Start(mklinkInfo))
                {
                    string output = mklinkProcess.StandardOutput.ReadToEnd();
                    string error = mklinkProcess.StandardError.ReadToEnd();
                    mklinkProcess.WaitForExit();

                    if (mklinkProcess.ExitCode == 0)
                        LogMessage($"Created symbolic link from {gameSteamAppsPath} to {localSteamAppsPath}");
                    else
                    {
                        LogMessage($"Error creating symbolic link: {error}");
                        throw new Exception($"Cannot create symbolic link: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error creating symbolic link: {ex.Message}");
                throw;
            }
        }

        private void RemoveSteamAppsSymLink()
        {
            string localSteamAppsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steamapps");
            try
            {
                if (Directory.Exists(localSteamAppsPath))
                {
                    Directory.Delete(localSteamAppsPath, true);
                    LogMessage($"Removed symbolic link at {localSteamAppsPath}");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error removing symbolic link at {localSteamAppsPath}: {ex.Message}");
            }
        }

        private void FixNestedSteamAppsFolder(string gameInstallDir)
        {
            string gameSteamAppsPath = Path.Combine(gameInstallDir, "steamapps");
            string nestedSteamAppsPath = Path.Combine(gameSteamAppsPath, "steamapps");

            try
            {
                if (Directory.Exists(nestedSteamAppsPath))
                {
                    LogMessage($"Detected nested steamapps folder at {nestedSteamAppsPath}. Processing...");

                    foreach (string dir in Directory.GetDirectories(nestedSteamAppsPath))
                    {
                        string dirName = Path.GetFileName(dir);
                        string targetDir = Path.Combine(gameSteamAppsPath, dirName);

                        if (!Directory.Exists(targetDir))
                        {
                            Directory.Move(dir, targetDir);
                            LogMessage($"Moved directory {dirName} from {nestedSteamAppsPath} to {gameSteamAppsPath}");
                        }
                    }

                    foreach (string file in Directory.GetFiles(nestedSteamAppsPath))
                    {
                        string fileName = Path.GetFileName(file);
                        string targetFile = Path.Combine(gameSteamAppsPath, fileName);

                        if (!File.Exists(targetFile))
                        {
                            File.Move(file, targetFile);
                            LogMessage($"Moved file {fileName} from {nestedSteamAppsPath} to {gameSteamAppsPath}");
                        }
                    }

                    if (Directory.GetFiles(nestedSteamAppsPath).Length == 0 && Directory.GetDirectories(nestedSteamAppsPath).Length == 0)
                    {
                        Directory.Delete(nestedSteamAppsPath);
                        LogMessage($"Deleted nested steamapps folder at {nestedSteamAppsPath}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error processing nested steamapps folder: {ex.Message}");
            }
        }

        private string SafeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }

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

        // Xóa định nghĩa InitializeComponent() ở đây vì nó đã được định nghĩa trong SteamCmdService.Designer.cs
    }
}