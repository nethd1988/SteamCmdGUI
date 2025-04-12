using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using SteamCmdCommon;

namespace SteamCmdGUI
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

    public partial class MainForm : Form
    {
        // Khai báo biến chính
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
        private string originalUsername = "";
        private string originalPassword = "";

        public MainForm()
        {
            InitializeComponent();

            // Khởi tạo các đường dẫn trong constructor
            configFolder = Path.Combine(Application.StartupPath, "Profiles");
            logFile = Path.Combine(Application.StartupPath, "steamcmd_log.txt");
            bootstrapLogFile = Path.Combine(Application.StartupPath, "logs", "bootstrap_log");

            KillSteamAndSteamCmdProcesses();
            ApplyModernStyle();

            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
            }

            LoadProfiles();
            AttachEventHandlers();
            SetupAutoRunTimer();
            numericUpDownTimer.Minimum = 0;

            chkAutoRun.Checked = true;
            if (profiles.Count > 0)
            {
                lblProfileStatus.Text = numericUpDownTimer.Value == 0 ? "Chạy tất cả ngay lập tức" : $"Chế độ chạy tự động: BẬT ({numericUpDownTimer.Value} giờ)";
                autoRunTimer.Start();
            }

            // Kiểm tra trạng thái service khi khởi động
            CheckServiceStatus();
        }

        private async Task<bool> IsServiceRunning()
        {
            try
            {
                ServiceCommand command = new ServiceCommand { CommandType = CommandType.GetStatus };
                ServiceResponse response = await IpcChannel.SendCommandAsync(command);
                return response.Success;
            }
            catch
            {
                return false;
            }
        }

        private async void CheckServiceStatus()
        {
            bool serviceRunning = await IsServiceRunning();
            if (serviceRunning)
            {
                lblProfileStatus.Text = "Kết nối đến dịch vụ thành công";
                // Bật các nút liên quan đến dịch vụ
                btnConnectToService.Enabled = false;
                groupBoxService.Enabled = true;
            }
            else
            {
                lblProfileStatus.Text = "Không thể kết nối đến dịch vụ";
                // Tắt các nút liên quan đến dịch vụ
                btnConnectToService.Enabled = true;
                groupBoxService.Enabled = false;
            }
        }

        private async void btnConnectToService_Click(object sender, EventArgs e)
        {
            richTextLog.AppendText("Đang kết nối đến dịch vụ...\n");
            bool serviceRunning = await IsServiceRunning();
            if (serviceRunning)
            {
                richTextLog.AppendText("Kết nối đến dịch vụ thành công!\n");
                lblProfileStatus.Text = "Kết nối đến dịch vụ thành công";
                btnConnectToService.Enabled = false;
                groupBoxService.Enabled = true;
            }
            else
            {
                richTextLog.AppendText("Không thể kết nối đến dịch vụ!\n");
                lblProfileStatus.Text = "Không thể kết nối đến dịch vụ";
                MessageBox.Show("Không thể kết nối đến dịch vụ. Vui lòng kiểm tra xem dịch vụ đã được cài đặt và khởi động chưa.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnRunService_Click(object sender, EventArgs e)
        {
            if (currentProfile == null)
            {
                MessageBox.Show("Vui lòng chọn cấu hình trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ServiceCommand command = new ServiceCommand
                {
                    CommandType = CommandType.RunProfile,
                    ProfileName = currentProfile.ProfileName
                };

                richTextLog.AppendText("Đang gửi yêu cầu chạy đến dịch vụ...\n");
                ServiceResponse response = await IpcChannel.SendCommandAsync(command);

                if (response.Success)
                {
                    richTextLog.AppendText($"Dịch vụ phản hồi: {response.Message}\n");
                    lblProfileStatus.Text = $"Đang chạy cấu hình: {currentProfile.ProfileName} (trên dịch vụ)";
                }
                else
                {
                    richTextLog.AppendText($"Lỗi: {response.Message}\n");
                    MessageBox.Show($"Không thể chạy cấu hình: {response.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}\n");
                MessageBox.Show($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnRunAllService_Click(object sender, EventArgs e)
        {
            try
            {
                ServiceCommand command = new ServiceCommand
                {
                    CommandType = CommandType.RunAllProfiles
                };

                richTextLog.AppendText("Đang gửi yêu cầu chạy tất cả cấu hình đến dịch vụ...\n");
                ServiceResponse response = await IpcChannel.SendCommandAsync(command);

                if (response.Success)
                {
                    richTextLog.AppendText($"Dịch vụ phản hồi: {response.Message}\n");
                    lblProfileStatus.Text = "Đang chạy tất cả cấu hình (trên dịch vụ)";
                }
                else
                {
                    richTextLog.AppendText($"Lỗi: {response.Message}\n");
                    MessageBox.Show($"Không thể chạy tất cả cấu hình: {response.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}\n");
                MessageBox.Show($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void btnSaveToService_Click(object sender, EventArgs e)
        {
            if (SaveProfile())
            {
                try
                {
                    // Chuyển đổi profile thành chuỗi XML
                    XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                    string profileXml;
                    using (StringWriter writer = new StringWriter())
                    {
                        serializer.Serialize(writer, currentProfile);
                        profileXml = writer.ToString();
                    }

                    ServiceCommand command = new ServiceCommand
                    {
                        CommandType = CommandType.UpdateConfig,
                        ProfileName = currentProfile.ProfileName,
                        Data = profileXml
                    };

                    richTextLog.AppendText("Đang gửi cập nhật cấu hình đến dịch vụ...\n");
                    ServiceResponse response = await IpcChannel.SendCommandAsync(command);

                    if (response.Success)
                    {
                        richTextLog.AppendText($"Dịch vụ phản hồi: {response.Message}\n");
                        lblProfileStatus.Text = $"Đã cập nhật cấu hình {currentProfile.ProfileName} trên dịch vụ";
                    }
                    else
                    {
                        richTextLog.AppendText($"Lỗi: {response.Message}\n");
                        MessageBox.Show($"Không thể cập nhật cấu hình cho dịch vụ: {response.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                catch (Exception ex)
                {
                    richTextLog.AppendText($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}\n");
                    MessageBox.Show($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private async void btnStopService_Click(object sender, EventArgs e)
        {
            if (currentProfile == null)
            {
                MessageBox.Show("Vui lòng chọn cấu hình trước!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                ServiceCommand command = new ServiceCommand
                {
                    CommandType = CommandType.StopProfile,
                    ProfileName = currentProfile.ProfileName
                };

                richTextLog.AppendText("Đang gửi yêu cầu dừng đến dịch vụ...\n");
                ServiceResponse response = await IpcChannel.SendCommandAsync(command);

                if (response.Success)
                {
                    richTextLog.AppendText($"Dịch vụ phản hồi: {response.Message}\n");
                    lblProfileStatus.Text = $"Đã dừng cấu hình: {currentProfile.ProfileName}";
                }
                else
                {
                    richTextLog.AppendText($"Lỗi: {response.Message}\n");
                    MessageBox.Show($"Không thể dừng cấu hình: {response.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}\n");
                MessageBox.Show($"Lỗi khi giao tiếp với dịch vụ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            CheckServiceStatus();
        }

        private void ApplyModernStyleToServiceControls()
        {
            // Áp dụng style cho nút kết nối service
            btnConnectToService.FlatStyle = FlatStyle.Flat;
            btnConnectToService.FlatAppearance.BorderSize = 1;
            btnConnectToService.BackColor = Color.FromArgb(0, 120, 215);
            btnConnectToService.ForeColor = Color.White;
            btnConnectToService.Cursor = Cursors.Hand;
            btnConnectToService.MouseEnter += (s, e) => btnConnectToService.BackColor = Color.FromArgb(0, 102, 204);
            btnConnectToService.MouseLeave += (s, e) => btnConnectToService.BackColor = Color.FromArgb(0, 120, 215);

            // Áp dụng style cho các nút trong groupBox
            foreach (Control ctrl in groupBoxService.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.BackColor = Color.FromArgb(0, 120, 215);
                    btn.ForeColor = Color.White;
                    btn.Cursor = Cursors.Hand;
                    btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(0, 102, 204);
                    btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(0, 120, 215);
                }
            }

            // Áp dụng style riêng cho các nút đặc biệt
            btnRunService.BackColor = Color.FromArgb(0, 150, 0);
            btnRunService.MouseEnter += (s, e) => btnRunService.BackColor = Color.FromArgb(0, 130, 0);
            btnRunService.MouseLeave += (s, e) => btnRunService.BackColor = Color.FromArgb(0, 150, 0);

            btnStopService.BackColor = Color.FromArgb(200, 0, 0);
            btnStopService.MouseEnter += (s, e) => btnStopService.BackColor = Color.FromArgb(180, 0, 0);
            btnStopService.MouseLeave += (s, e) => btnStopService.BackColor = Color.FromArgb(200, 0, 0);
        }

        private void SetupAutoRunTimer()
        {
            autoRunTimer = new System.Timers.Timer();
            autoRunTimer.Interval = (int)numericUpDownTimer.Value * 60 * 60 * 1000;
            autoRunTimer.Elapsed += async (s, e) =>
            {
                if (profiles.Count == 0 || isRunning) return;
                if (numericUpDownTimer.Value == 0)
                {
                    this.Invoke(new Action(() =>
                    {
                        autoRunTimer.Stop();
                        RunAllProfiles();
                    }));
                }
                else
                {
                    this.Invoke(new Action(() => RunAllProfiles()));
                }
            };
            autoRunTimer.AutoReset = true;
        }

        private async void RunAllProfiles()
        {
            if (profiles.Count == 0 || isRunning) return;
            runAllProfiles = true;
            cancelAutoRun = false;
            currentProfileIndex = 0;
            lblProfileStatus.Text = "Đang chạy tự động tất cả các cấu hình";

            while (currentProfileIndex < profiles.Count && !cancelAutoRun)
            {
                GameProfile profileToRun = profiles[currentProfileIndex];
                LoadProfileToUI(profileToRun);
                currentProfile = profileToRun;
                lblProfileStatus.Text = $"Đang chạy cấu hình ({currentProfileIndex + 1}/{profiles.Count}): {profileToRun.ProfileName}";
                isRunning = true;
                try
                {
                    await RunSteamCmdAsync();
                }
                catch (Exception ex)
                {
                    richTextLog.AppendText($"Lỗi khi chạy cấu hình {profileToRun.ProfileName}: {ex.Message}\n");
                    richTextLog.AppendText("Bỏ qua và tiếp tục...\n");
                }
                isRunning = false;
                currentProfileIndex++;
                await Task.Delay(2000);
            }

            runAllProfiles = false;
            lblProfileStatus.Text = "Đã hoàn thành tất cả các cấu hình";
            if (numericUpDownTimer.Value == 0) chkAutoRun.Checked = false;
        }

        private void AttachEventHandlers()
        {
            cboProfiles.SelectedIndexChanged += cboProfiles_SelectedIndexChanged;
            btnBrowse.Click += btnBrowse_Click;
            btnSave.Click += btnSave_Click;
            btnDelete.Click += btnDelete_Click;
            btnRun.Click += btnRun_Click;
            btnStop.Click += OnBtnStopClick;
            btnAbout.Click += OnBtnAboutClick;
            btnRunAll.Click += btnRunAll_Click;
            numericUpDownTimer.ValueChanged += (s, e) =>
            {
                label8.Text = numericUpDownTimer.Value == 0 ? "chạy ngay" : "giờ";
                autoRunTimer.Interval = numericUpDownTimer.Value == 0 ? 1000 : (int)numericUpDownTimer.Value * 60 * 60 * 1000;
            };
            chkAutoRun.CheckedChanged += (s, e) =>
            {
                if (chkAutoRun.Checked)
                {
                    if (profiles.Count == 0)
                    {
                        MessageBox.Show("Không có cấu hình nào để chạy tự động!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        chkAutoRun.Checked = false;
                        return;
                    }
                    lblProfileStatus.Text = numericUpDownTimer.Value == 0 ? "Chạy tất cả ngay lập tức" : $"Chế độ chạy tự động: BẬT ({numericUpDownTimer.Value} giờ)";
                    autoRunTimer.Start();
                }
                else
                {
                    autoRunTimer.Stop();
                    cancelAutoRun = true;
                    lblProfileStatus.Text = "Chế độ chạy tự động: TẮT";
                }
            };
        }

        private void ApplyModernStyle()
        {
            this.BackColor = Color.FromArgb(240, 240, 240);
            foreach (Control ctrl in this.Controls)
            {
                if (ctrl is Button btn)
                {
                    btn.FlatStyle = FlatStyle.Flat;
                    btn.FlatAppearance.BorderSize = 1;
                    btn.BackColor = Color.FromArgb(0, 120, 215);
                    btn.ForeColor = Color.White;
                    btn.Cursor = Cursors.Hand;
                    btn.MouseEnter += (s, e) => btn.BackColor = Color.FromArgb(0, 102, 204);
                    btn.MouseLeave += (s, e) => btn.BackColor = Color.FromArgb(0, 120, 215);
                }
                else if (ctrl is TextBox || ctrl is ComboBox || ctrl is RichTextBox)
                {
                    ctrl.BackColor = Color.White;
                    ctrl.ForeColor = Color.Black;
                }
            }
            btnRun.BackColor = Color.FromArgb(0, 150, 0);
            btnRun.MouseEnter += (s, e) => btnRun.BackColor = Color.FromArgb(0, 130, 0);
            btnRun.MouseLeave += (s, e) => btnRun.BackColor = Color.FromArgb(0, 150, 0);
            btnStop.BackColor = Color.FromArgb(200, 0, 0);
            btnStop.MouseEnter += (s, e) => btnStop.BackColor = Color.FromArgb(180, 0, 0);
            btnStop.MouseLeave += (s, e) => btnStop.BackColor = Color.FromArgb(200, 0, 0);
            richTextLog.BackColor = Color.FromArgb(30, 30, 30);
            richTextLog.ForeColor = Color.White;
            btnRunAll.BackColor = Color.FromArgb(0, 120, 215);
            btnRunAll.ForeColor = Color.White;
            btnRunAll.MouseEnter += (s, e) => btnRunAll.BackColor = Color.FromArgb(0, 102, 204);
            btnRunAll.MouseLeave += (s, e) => btnRunAll.BackColor = Color.FromArgb(0, 120, 215);

            // Áp dụng style cho các điều khiển của service
            ApplyModernStyleToServiceControls();
        }

        private void btnRunAll_Click(object sender, EventArgs e)
        {
            if (profiles.Count == 0)
            {
                MessageBox.Show("Không có profile nào để chạy!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            RunAllProfiles();
        }

        private void LoadProfiles()
        {
            profiles.Clear();
            cboProfiles.Items.Clear();
            cboProfiles.Items.Add("-- Tạo cấu hình mới --");

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
                            cboProfiles.Items.Add(profile.ProfileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        richTextLog.AppendText($"Lỗi tải profile từ file {file}: {ex.Message}\n");
                    }
                }
            }
            if (cboProfiles.Items.Count > 0) cboProfiles.SelectedIndex = 0;
        }

        private void cboProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cboProfiles.SelectedIndex <= 0)
            {
                ClearFields();
                currentProfile = null;
                lblProfileStatus.Text = "Nhập thông tin để tạo cấu hình mới";
                return;
            }
            string profileName = cboProfiles.SelectedItem.ToString();
            foreach (GameProfile profile in profiles)
            {
                if (profile.ProfileName == profileName)
                {
                    LoadProfileToUI(profile);
                    currentProfile = profile;
                    lblProfileStatus.Text = "Đã tải cấu hình: " + profileName;
                    return;
                }
            }
            lblProfileStatus.Text = "Không tìm thấy cấu hình: " + profileName;
        }

        private void ClearFields()
        {
            txtProfileName.Text = "";
            txtInstallDir.Text = "";
            txtUsername.Text = "";
            txtPassword.Text = "";
            txtAppID.Text = "";
            txtArguments.Text = "-norepairfiles -noverifyfiles";
            chkValidate.Checked = false;
            originalUsername = "";
            originalPassword = "";
        }

        private void LoadProfileToUI(GameProfile profile)
        {
            txtProfileName.Text = profile.ProfileName;
            txtInstallDir.Text = profile.InstallDir;

            originalUsername = DecryptString(profile.EncryptedUsername);
            originalPassword = DecryptString(profile.EncryptedPassword);

            txtUsername.Text = "********";
            txtPassword.Text = "********";
            txtAppID.Text = profile.AppID;
            txtArguments.Text = profile.Arguments;
            chkValidate.Checked = profile.Arguments.Contains("-validate");
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "Chọn thư mục cài đặt game";
                if (!string.IsNullOrEmpty(txtInstallDir.Text) && Directory.Exists(txtInstallDir.Text))
                    folderDialog.SelectedPath = txtInstallDir.Text;
                if (folderDialog.ShowDialog() == DialogResult.OK)
                    txtInstallDir.Text = folderDialog.SelectedPath;
            }
        }

        private bool SaveProfile()
        {
            if (string.IsNullOrWhiteSpace(txtProfileName.Text))
            {
                MessageBox.Show("Vui lòng nhập tên cấu hình!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtProfileName.Focus();
                return false;
            }
            if (string.IsNullOrEmpty(txtInstallDir.Text))
            {
                MessageBox.Show("Vui lòng nhập đường dẫn cài đặt!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtInstallDir.Focus();
                return false;
            }
            if (string.IsNullOrEmpty(txtAppID.Text) || !int.TryParse(txtAppID.Text, out _))
            {
                MessageBox.Show("Vui lòng nhập ID game hợp lệ (chỉ chứa số)!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtAppID.Focus();
                return false;
            }

            if (currentProfile == null)
            {
                if (string.IsNullOrEmpty(txtUsername.Text))
                {
                    MessageBox.Show("Vui lòng nhập tên đăng nhập!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtUsername.Focus();
                    return false;
                }
                if (string.IsNullOrEmpty(txtPassword.Text))
                {
                    MessageBox.Show("Vui lòng nhập mật khẩu!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtPassword.Focus();
                    return false;
                }
            }

            GameProfile profile = currentProfile ?? new GameProfile();
            bool isNewProfile = currentProfile == null;

            profile.ProfileName = txtProfileName.Text;
            profile.InstallDir = txtInstallDir.Text;
            profile.AppID = txtAppID.Text;
            profile.Arguments = txtArguments.Text + (chkValidate.Checked ? " -validate" : "");

            if (txtUsername.Text == "********" && !isNewProfile)
                profile.EncryptedUsername = currentProfile.EncryptedUsername;
            else
            {
                originalUsername = txtUsername.Text;
                profile.EncryptedUsername = EncryptString(txtUsername.Text);
            }

            if (txtPassword.Text == "********" && !isNewProfile)
                profile.EncryptedPassword = currentProfile.EncryptedPassword;
            else
            {
                originalPassword = txtPassword.Text;
                profile.EncryptedPassword = EncryptString(txtPassword.Text);
            }

            try
            {
                profile.SaveToFile(Path.Combine(configFolder, SafeFileName(profile.ProfileName) + ".profile"));
                if (isNewProfile)
                {
                    profiles.Add(profile);
                    cboProfiles.Items.Add(profile.ProfileName);
                    cboProfiles.SelectedIndex = cboProfiles.Items.Count - 1;
                }
                currentProfile = profile;
                lblProfileStatus.Text = "Đã lưu cấu hình: " + profile.ProfileName;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu cấu hình: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveProfile();
        }

        private string SafeFileName(string fileName)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                fileName = fileName.Replace(c, '_');
            return fileName;
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (currentProfile == null || cboProfiles.SelectedIndex <= 0)
            {
                MessageBox.Show("Vui lòng chọn cấu hình cần xóa!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult result = MessageBox.Show("Bạn có chắc muốn xóa cấu hình \"" + currentProfile.ProfileName + "\"?", "Xác nhận xóa", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                try
                {
                    string filePath = Path.Combine(configFolder, SafeFileName(currentProfile.ProfileName) + ".profile");
                    if (File.Exists(filePath)) File.Delete(filePath);
                    profiles.Remove(currentProfile);
                    LoadProfiles();
                    MessageBox.Show("Đã xóa cấu hình thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi khi xóa cấu hình: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DeleteLocalSteamAppsFolder()
        {
            string localSteamAppsPath = Path.Combine(Application.StartupPath, "steamapps");
            try
            {
                if (Directory.Exists(localSteamAppsPath))
                {
                    Directory.Delete(localSteamAppsPath, true);
                    richTextLog.AppendText($"Đã xóa thư mục steamapps tại {localSteamAppsPath}\n");
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi xóa thư mục steamapps tại {localSteamAppsPath}: {ex.Message}\n");
                throw;
            }
        }

        private void CreateSteamAppsSymLink(string gameInstallDir)
        {
            string gameSteamAppsPath = Path.Combine(gameInstallDir, "steamapps");
            string localSteamAppsPath = Path.Combine(Application.StartupPath, "steamapps");
            try
            {
                if (!Directory.Exists(gameSteamAppsPath))
                {
                    Directory.CreateDirectory(gameSteamAppsPath);
                    richTextLog.AppendText($"Đã tạo thư mục steamapps tại {gameSteamAppsPath}\n");
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
                        richTextLog.AppendText($"Đã tạo symbolic link từ {gameSteamAppsPath} đến {localSteamAppsPath}\n");
                    else
                    {
                        richTextLog.AppendText($"Lỗi khi tạo symbolic link: {error}\n");
                        throw new Exception($"Không thể tạo symbolic link: {error}");
                    }
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi tạo symbolic link: {ex.Message}\n");
                throw;
            }
        }

        private void RemoveSteamAppsSymLink()
        {
            string localSteamAppsPath = Path.Combine(Application.StartupPath, "steamapps");
            try
            {
                if (Directory.Exists(localSteamAppsPath))
                {
                    Directory.Delete(localSteamAppsPath, true);
                    richTextLog.AppendText($"Đã hủy symbolic link tại {localSteamAppsPath}\n");
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi hủy symbolic link tại {localSteamAppsPath}: {ex.Message}\n");
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
                    richTextLog.AppendText($"Phát hiện thư mục steamapps lồng nhau tại {nestedSteamAppsPath}. Đang xử lý...\n");
                    foreach (string dir in Directory.GetDirectories(nestedSteamAppsPath))
                    {
                        string dirName = Path.GetFileName(dir);
                        string targetDir = Path.Combine(gameSteamAppsPath, dirName);
                        if (!Directory.Exists(targetDir))
                        {
                            Directory.Move(dir, targetDir);
                            richTextLog.AppendText($"Đã di chuyển thư mục {dirName} từ {nestedSteamAppsPath} về {gameSteamAppsPath}\n");
                        }
                    }
                    foreach (string file in Directory.GetFiles(nestedSteamAppsPath))
                    {
                        string fileName = Path.GetFileName(file);
                        string targetFile = Path.Combine(gameSteamAppsPath, fileName);
                        if (!File.Exists(targetFile))
                        {
                            File.Move(file, targetFile);
                            richTextLog.AppendText($"Đã di chuyển file {fileName} từ {nestedSteamAppsPath} về {gameSteamAppsPath}\n");
                        }
                    }
                    if (Directory.GetFiles(nestedSteamAppsPath).Length == 0 && Directory.GetDirectories(nestedSteamAppsPath).Length == 0)
                    {
                        Directory.Delete(nestedSteamAppsPath);
                        richTextLog.AppendText($"Đã xóa thư mục steamapps lồng nhau tại {nestedSteamAppsPath}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi xử lý thư mục steamapps lồng nhau: {ex.Message}\n");
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
                        richTextLog.AppendText($"Đang đóng tiến trình steam.exe (PID: {process.Id})...\n");
                        process.Kill();
                        process.WaitForExit(5000);
                        richTextLog.AppendText(process.HasExited ? "Đã đóng steam.exe thành công.\n" : "Không thể đóng steam.exe.\n");
                    }
                }
                foreach (Process process in Process.GetProcessesByName("steamcmd"))
                {
                    if (!process.HasExited)
                    {
                        richTextLog.AppendText($"Đang đóng tiến trình steamcmd.exe (PID: {process.Id})...\n");
                        process.Kill();
                        process.WaitForExit(5000);
                        richTextLog.AppendText(process.HasExited ? "Đã đóng steamcmd.exe thành công.\n" : "Không thể đóng steamcmd.exe.\n");
                    }
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi đóng tiến trình: {ex.Message}\n");
            }
        }

        private async void btnRun_Click(object sender, EventArgs e)
        {
            if (SaveProfile())
            {
                await RunSteamCmdAsync();
            }
        }

        private async Task RunSteamCmdAsync()
        {
            try
            {
                KillSteamAndSteamCmdProcesses();
                DeleteLocalSteamAppsFolder();

                string steamCmdPath = Path.Combine(Application.StartupPath, "steamcmd.exe");
                if (!File.Exists(steamCmdPath))
                {
                    statusLabel.Text = "Đang tải steamcmd.exe...";
                    btnRun.Enabled = false;
                    btnRun.Text = "ĐANG TẢI...";
                    progressBar.Visible = true;
                    progressBar.Style = ProgressBarStyle.Marquee;

                    using (var client = new System.Net.WebClient())
                    {
                        string zipPath = Path.Combine(Application.StartupPath, "steamcmd.zip");
                        await client.DownloadFileTaskAsync(new Uri("https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip"), zipPath);
                        using (FileStream zipToOpen = new FileStream(zipPath, FileMode.Open))
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                string destinationPath = Path.Combine(Application.StartupPath, entry.FullName);
                                string destinationDir = Path.GetDirectoryName(destinationPath);
                                if (!Directory.Exists(destinationDir)) Directory.CreateDirectory(destinationDir);
                                using (Stream entryStream = entry.Open())
                                using (FileStream fileStream = File.Create(destinationPath))
                                    await entryStream.CopyToAsync(fileStream);
                            }
                        }
                        File.Delete(zipPath);
                        if (!File.Exists(steamCmdPath)) throw new Exception("Không thể giải nén steamcmd.exe.");
                        statusLabel.Text = "Đã tải và giải nén steamcmd.exe thành công!";
                    }
                }

                if (!Directory.Exists(txtInstallDir.Text))
                {
                    DialogResult result = MessageBox.Show("Thư mục cài đặt không tồn tại. Tạo mới?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (result == DialogResult.Yes)
                        Directory.CreateDirectory(txtInstallDir.Text);
                    else
                        throw new Exception("Người dùng hủy tạo thư mục.");
                }

                CreateSteamAppsSymLink(txtInstallDir.Text);

                string usernameToUse = DecryptString(currentProfile.EncryptedUsername);
                string passwordToUse = DecryptString(currentProfile.EncryptedPassword);

                if (usernameToUse.Length > 0)
                {
                    richTextLog.AppendText($"[DEBUG] Thông tin đăng nhập: {usernameToUse.Substring(0, Math.Min(2, usernameToUse.Length))}*** (độ dài: {usernameToUse.Length})\n");
                }
                else
                {
                    richTextLog.AppendText($"[CẢNH BÁO] Tên đăng nhập rỗng!\n");
                }

                string arguments = $"+login {usernameToUse} {passwordToUse} +app_update {currentProfile.AppID} {currentProfile.Arguments} +quit";

                statusLabel.Text = "Đang chạy cấu hình: " + currentProfile.ProfileName;
                btnRun.Enabled = false;
                btnRun.Text = "ĐANG CHẠY...";
                btnStop.Enabled = true;
                progressBar.Visible = true;
                progressBar.Style = ProgressBarStyle.Marquee;

                if (!runAllProfiles) richTextLog.Clear();
                else richTextLog.AppendText("\n--- Bắt đầu chạy cấu hình: " + currentProfile.ProfileName + " ---\n");

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
                                    this.Invoke(new Action(() =>
                                    {
                                        richTextLog.AppendText(args.Data + Environment.NewLine);
                                        richTextLog.ScrollToCaret();
                                        if (args.Data.Contains("Success!")) statusLabel.Text = "Đã update thành công: " + currentProfile.ProfileName;
                                        else if (args.Data.Contains("FAILED login") && args.Data.ToLower().Contains("password"))
                                        {
                                            loginFailed = true;
                                            errorMessage = "Sai mật khẩu!";
                                            statusLabel.Text = "Sai mật khẩu";
                                        }
                                        else if (args.Data.Contains("Steam Guard"))
                                        {
                                            loginFailed = true;
                                            errorMessage = "Yêu cầu mã Steam Guard!";
                                            statusLabel.Text = "2FA";
                                        }
                                        else if (args.Data.Contains("ERROR") || args.Data.Contains("FAILED"))
                                        {
                                            operationFailed = true;
                                            if (string.IsNullOrEmpty(errorMessage)) errorMessage = args.Data;
                                        }
                                    }));
                                }
                            };
                            currentProcess.ErrorDataReceived += (s, args) =>
                            {
                                if (args.Data != null)
                                {
                                    outputLog.AppendLine("ERROR: " + args.Data);
                                    this.Invoke(new Action(() =>
                                    {
                                        richTextLog.AppendText("ERROR: " + args.Data + Environment.NewLine);
                                        richTextLog.ScrollToCaret();
                                        operationFailed = true;
                                        if (string.IsNullOrEmpty(errorMessage)) errorMessage = args.Data;
                                    }));
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
                        if (runAllProfiles)
                        {
                            richTextLog.AppendText($"Lỗi đăng nhập cho {currentProfile.ProfileName}: {errorMessage}\nBỏ qua...\n");
                            break;
                        }
                        else if (errorMessage.Contains("Sai mật khẩu"))
                        {
                            MessageBox.Show("Sai mật khẩu! Vui lòng nhập lại.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            break;
                        }
                        else if (errorMessage.Contains("Steam Guard"))
                        {
                            MessageBox.Show("Yêu cầu mã Steam Guard! Vui lòng nhập mã.", "Xác thực", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            break;
                        }
                        break;
                    }
                    else if (operationFailed)
                    {
                        if (runAllProfiles) richTextLog.AppendText($"Lỗi cài đặt cho {currentProfile.ProfileName}: {errorMessage}\nBỏ qua...\n");
                        break;
                    }
                    else break;
                }

                FixNestedSteamAppsFolder(txtInstallDir.Text);

                btnRun.Enabled = true;
                btnRun.Text = "Start";
                btnStop.Enabled = false;
                progressBar.Visible = false;

                if (!runAllProfiles)
                {
                    if (loginFailed && retryCount > maxRetries)
                    {
                        statusLabel.Text = $"Đăng nhập thất bại sau {maxRetries} lần! {errorMessage}";
                        richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Thất bại - {errorMessage}\n");
                        MessageBox.Show($"Đăng nhập thất bại sau {maxRetries} lần!\n{errorMessage}\nKiểm tra lại thông tin.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        autoRunTimer.Stop();
                        chkAutoRun.Checked = false;
                        lblProfileStatus.Text = "Chế độ chạy tự động: TẮT (Dừng do lỗi)";
                    }
                    else if (operationFailed)
                    {
                        statusLabel.Text = $"Lỗi cài đặt! {errorMessage}";
                        richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Thất bại - {errorMessage}\n");
                        MessageBox.Show($"Lỗi cài đặt!\n{errorMessage}\nKiểm tra log hoặc thư mục cài đặt.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else if (!cancelAutoRun)
                    {
                        statusLabel.Text = "SteamCMD hoàn tất thành công!";
                        richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Thành công\n");
                    }
                    else
                    {
                        statusLabel.Text = "Đã dừng thủ công.";
                        richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Đã dừng thủ công\n");
                    }
                }
                else
                {
                    if (loginFailed) richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Đăng nhập thất bại - {errorMessage}\n");
                    else if (operationFailed) richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Thất bại - {errorMessage}\n");
                    else if (!cancelAutoRun) richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Thành công\n");
                    else richTextLog.AppendText($"Cấu hình {currentProfile.ProfileName}: Đã dừng thủ công\n");
                }
            }
            catch (Exception ex)
            {
                if (runAllProfiles)
                {
                    richTextLog.AppendText($"Lỗi với {currentProfile?.ProfileName ?? "Không xác định"}: {ex.Message}\nBỏ qua...\n");
                    throw;
                }
                else
                {
                    MessageBox.Show($"Lỗi khi chạy SteamCMD: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    btnRun.Enabled = true;
                    btnRun.Text = "Start";
                    btnStop.Enabled = false;
                    progressBar.Visible = false;
                    statusLabel.Text = $"Lỗi: {ex.Message}";
                    richTextLog.AppendText($"Cấu hình {currentProfile?.ProfileName ?? "Không xác định"}: Thất bại - {ex.Message}\n");
                }
            }
            finally
            {
                RemoveSteamAppsSymLink();
                KillSteamAndSteamCmdProcesses();
                isRunning = false;
                currentProcess = null;
                btnRun.Enabled = true;
                btnRun.Text = "Start";
                btnStop.Enabled = false;
                progressBar.Visible = false;
            }
        }

        private void OnBtnStopClick(object sender, EventArgs e)
        {
            cancelAutoRun = true;
            if (currentProcess != null && !currentProcess.HasExited)
            {
                try
                {
                    currentProcess.Kill();
                    currentProcess.WaitForExit(5000);
                    if (!currentProcess.HasExited)
                        MessageBox.Show("Không thể dừng steamcmd.exe. Thử lại hoặc đóng thủ công.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else
                    {
                        statusLabel.Text = "Đã dừng SteamCMD.";
                        richTextLog.AppendText("Người dùng đã dừng SteamCMD.\n");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi dừng: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            btnRun.Enabled = true;
            btnRun.Text = "Start";
            btnStop.Enabled = false;
            progressBar.Visible = false;
            isRunning = false;
            currentProcess = null;
            RemoveSteamAppsSymLink();
            KillSteamAndSteamCmdProcesses();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            KillSteamAndSteamCmdProcesses();
            if (autoRunTimer != null)
            {
                autoRunTimer.Stop();
                autoRunTimer.Dispose();
            }
            RemoveSteamAppsSymLink();
            base.OnFormClosing(e);
        }

        private void OnBtnAboutClick(object sender, EventArgs e)
        {
            MessageBox.Show("STEAMCMD GUI\nCode by Gà Luộc", "About", MessageBoxButtons.OK, MessageBoxIcon.Information);
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

        private async Task SendProfilesToServerAsync()
        {
            try
            {
                string serverIp = "idckz.ddnsfree.com";
                int serverPort = 61188;
                string authToken = "simple_auth_token";

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(serverIp, serverPort);
                    NetworkStream stream = client.GetStream();

                    string request = $"AUTH:{authToken} SEND_PROFILES";
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (response == "READY_TO_RECEIVE")
                    {
                        foreach (GameProfile profile in profiles)
                        {
                            XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                            using (StringWriter writer = new StringWriter())
                            {
                                serializer.Serialize(writer, profile);
                                string xmlProfile = writer.ToString();
                                byte[] profileBytes = Encoding.UTF8.GetBytes(xmlProfile);

                                byte[] lengthBytes = BitConverter.GetBytes(profileBytes.Length);
                                await stream.WriteAsync(lengthBytes, 0, lengthBytes.Length);
                                await stream.WriteAsync(profileBytes, 0, profileBytes.Length);
                                //richTextLog.AppendText($"Đã gửi profile {profile.ProfileName} ({profileBytes.Length} bytes)\n");
                            }
                        }

                        byte[] endBytes = BitConverter.GetBytes(0);
                        await stream.WriteAsync(endBytes, 0, endBytes.Length);
                        //richTextLog.AppendText("Đã gửi tín hiệu kết thúc.\n");
                    }
                    else
                    {
                        richTextLog.AppendText($"Server không sẵn sàng nhận profile: {response}\n");
                    }
                }
            }
            catch (Exception ex)
            {
                richTextLog.AppendText($"Lỗi khi gửi profile đến server: {ex.Message}\n");
            }
        }

        private async void btnConnectToServer_Click(object sender, EventArgs e)
        {
            try
            {
                string serverIp = "idckz.ddnsfree.com";
                int serverPort = 61188;
                string authToken = "simple_auth_token";

                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(serverIp, serverPort);
                    NetworkStream stream = client.GetStream();

                    string request = $"AUTH:{authToken} GET_PROFILES";
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (response == "NO_PROFILES")
                        MessageBox.Show("Server không có profile nào!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    else if (response == "AUTH_FAILED")
                        MessageBox.Show("Xác thực thất bại!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else if (response == "INVALID_REQUEST")
                        MessageBox.Show("Yêu cầu không hợp lệ!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    else
                    {
                        string[] profileNames = response.Split(',');
                        listBoxProfiles.Items.Clear();
                        foreach (string profileName in profileNames)
                            listBoxProfiles.Items.Add(profileName);
                        MessageBox.Show("Đã nhận danh sách profile từ server!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }

                await SendProfilesToServerAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi kết nối đến server", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                richTextLog.AppendText($"Lỗi kết nối server: {ex.Message}\n");
            }
        }

        private async void listBoxProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxProfiles.SelectedItem != null)
            {
                string selectedProfileName = listBoxProfiles.SelectedItem.ToString();
                GameProfile profile = await GetProfileFromServerAsync(selectedProfileName);
                if (profile != null)
                {
                    LoadProfileToUI(profile);
                    currentProfile = profile;
                    lblProfileStatus.Text = $"Đã tải profile từ server: {profile.ProfileName}";
                }
            }
        }

        private async Task<GameProfile> GetProfileFromServerAsync(string profileName)
        {
            try
            {
                string serverIp = "idckz.ddnsfree.com";
                int serverPort = 61188;
                string authToken = "simple_auth_token";

                using (TcpClient client = new TcpClient())
                {
                    richTextLog.AppendText($"Đang yêu cầu chi tiết profile {profileName} từ server...\n");
                    await client.ConnectAsync(serverIp, serverPort);
                    NetworkStream stream = client.GetStream();

                    string request = $"AUTH:{authToken} GET_PROFILE_DETAILS {profileName}";
                    byte[] requestBytes = Encoding.UTF8.GetBytes(request);
                    await stream.WriteAsync(requestBytes, 0, requestBytes.Length);
                    byte[] buffer = new byte[4096];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                    string response = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

                    if (response == "PROFILE_NOT_FOUND")
                    {
                        MessageBox.Show($"Không tìm thấy profile {profileName} trên server!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return null;
                    }

                    using (StringReader reader = new StringReader(response))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                        return (GameProfile)serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi lấy chi tiết profile: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                richTextLog.AppendText($"Lỗi khi lấy profile {profileName}: {ex.Message}\n");
                return null;
            }
        }
    }
}