private void ParseAndDisplayStatus(string status)
{
    // Parse key-value pairs from status string
    Dictionary<string, string> statusValues = new Dictionary<string, string>();
    string[] pairs = status.Split(';');

    foreach (string pair in pairs)
    {
        if (string.IsNullOrEmpty(pair)) continue;

        string[] keyValue = pair.Split('=');
        if (keyValue.Length == 2)
        {
            statusValues[keyValue[0]] = keyValue[1];
        }
    }

    // Update UI based on status
    if (statusValues.TryGetValue("RUNNING", out string running) && running == "True")
    {
        btnRun.Enabled = false;
        btnRunAll.Enabled = false;
        btnStop.Enabled = true;
        progressBar.Visible = true;
        progressBar.Style = ProgressBarStyle.Marquee;

        if (statusValues.TryGetValue("CURRENT_PROFILE", out string profileName) && profileName != "None")
        {
            if (statusValues.TryGetValue("RUN_ALL", out string runAll) && runAll == "true")
            {
                if (statusValues.TryGetValue("CURRENT_INDEX", out string currentIndex) &&
                    statusValues.TryGetValue("TOTAL_PROFILES", out string totalProfiles))
                {
                    statusLabel.Text = $"Đang chạy cấu hình ({currentIndex}/{totalProfiles}): {profileName}";
                }
                else
                {
                    statusLabel.Text = $"Đang chạy tất cả các cấu hình: {profileName}";
                }
            }
            else
            {
                statusLabel.Text = $"Đang chạy cấu hình: {profileName}";
            }
        }
        else
        {
            statusLabel.Text = "Đang chạy...";
        }
    }
    else
    {
        btnRun.Enabled = true;
        btnRunAll.Enabled = true;
        btnStop.Enabled = false;
        progressBar.Visible = false;

        if (statusValues.TryGetValue("LAST_RESULT", out string lastResult))
        {
            statusLabel.Text = lastResult;
        }
        else
        {
            statusLabel.Text = "Sẵn sàng";
        }
    }
}

private async void btnServiceControl_Click(object sender, EventArgs e)
{
    try
    {
        ServiceController sc = new ServiceController(ServiceName);

        if (sc.Status == ServiceControllerStatus.Running)
        {
            // Stop the service
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
            isConnectedToService = false;
            btnServiceControl.Text = "Khởi động Service";
            statusLabel.Text = "Service đã dừng";
            lblProfileStatus.Text = $"Trạng thái dịch vụ: {GetServiceStatusText(sc.Status)}";
        }
        else if (sc.Status == ServiceControllerStatus.Stopped)
        {
            // Start the service
            sc.Start();
            sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

            // Wait a moment for the service to initialize
            await Task.Delay(1000);

            // Connect to the service
            await ConnectToService();

            if (isConnectedToService)
            {
                await GetProfilesFromService();
                btnServiceControl.Text = "Dừng Service";
                lblProfileStatus.Text = $"Trạng thái dịch vụ: {GetServiceStatusText(sc.Status)}";
            }
        }
    }
    catch (InvalidOperationException) // Service may not be installed
    {
        DialogResult result = MessageBox.Show(
            "Dịch vụ SteamCmdService chưa được cài đặt. Bạn có muốn cài đặt ngay không?",
            "Dịch vụ chưa được cài đặt",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result == DialogResult.Yes)
        {
            await InstallService();
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Lỗi khi điều khiển dịch vụ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

private async Task InstallService()
{
    try
    {
        // Determine the service file paths
        string serviceExePath = Path.Combine(Application.StartupPath, "SteamCmdService.exe");

        if (!File.Exists(serviceExePath))
        {
            MessageBox.Show(
                "Không tìm thấy file thực thi của dịch vụ (SteamCmdService.exe).\n" +
                "Vui lòng đảm bảo file này nằm cùng thư mục với ứng dụng này.",
                "File không tồn tại",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        // Run the installer with admin privileges
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"create {ServiceName} binPath= \"{serviceExePath}\" DisplayName= \"Steam CMD Service\" start= auto",
            Verb = "runas", // Request admin privileges
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(psi))
        {
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                MessageBox.Show(
                    "Dịch vụ đã được cài đặt thành công.\nBây giờ bạn có thể khởi động dịch vụ.",
                    "Cài đặt thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Try to start the service
                ServiceController sc = new ServiceController(ServiceName);
                sc.Start();
                sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));

                // Wait a moment for the service to initialize
                await Task.Delay(1000);

                // Connect to the service
                await ConnectToService();

                if (isConnectedToService)
                {
                    await GetProfilesFromService();
                    btnServiceControl.Text = "Dừng Service";
                    lblProfileStatus.Text = $"Trạng thái dịch vụ: Đang chạy";
                }
            }
            else
            {
                MessageBox.Show(
                    "Không thể cài đặt dịch vụ. Mã lỗi: " + process.ExitCode,
                    "Lỗi cài đặt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Lỗi khi cài đặt dịch vụ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

private async void btnRun_Click(object sender, EventArgs e)
{
    if (!isConnectedToService)
    {
        MessageBox.Show("Không có kết nối đến dịch vụ. Vui lòng khởi động dịch vụ trước.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    if (await SaveProfileToService())
    {
        await RunProfileOnService(currentProfile.ProfileName);
    }
}

private async Task<bool> RunProfileOnService(string profileName)
{
    try
    {
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(ServiceAddress, ServicePort);
            using (NetworkStream stream = client.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes($"RUN_PROFILE {profileName}");
                await stream.WriteAsync(request, 0, request.Length);

                byte[] response = new byte[1024];
                int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                if (responseStr == "OPERATION_STARTED")
                {
                    btnRun.Enabled = false;
                    btnRunAll.Enabled = false;
                    btnStop.Enabled = true;
                    progressBar.Visible = true;
                    progressBar.Style = ProgressBarStyle.Marquee;
                    statusLabel.Text = $"Đang chạy cấu hình: {profileName}";
                    return true;
                }
                else if (responseStr == "ALREADY_RUNNING")
                {
                    MessageBox.Show("Một tiến trình SteamCMD đang chạy. Vui lòng đợi hoặc dừng tiến trình hiện tại.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }
                else
                {
                    MessageBox.Show($"Không thể chạy cấu hình: {responseStr}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Lỗi khi chạy cấu hình: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        return false;
    }
}

private async void btnRunAll_Click(object sender, EventArgs e)
{
    if (!isConnectedToService)
    {
        MessageBox.Show("Không có kết nối đến dịch vụ. Vui lòng khởi động dịch vụ trước.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    if (profiles.Count == 0)
    {
        MessageBox.Show("Không có cấu hình nào để chạy!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    try
    {
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(ServiceAddress, ServicePort);
            using (NetworkStream stream = client.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes("RUN_ALL");
                await stream.WriteAsync(request, 0, request.Length);

                byte[] response = new byte[1024];
                int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                if (responseStr == "OPERATION_STARTED")
                {
                    btnRun.Enabled = false;
                    btnRunAll.Enabled = false;
                    btnStop.Enabled = true;
                    progressBar.Visible = true;
                    progressBar.Style = ProgressBarStyle.Marquee;
                    statusLabel.Text = "Đang chạy tất cả các cấu hình";
                }
                else if (responseStr == "ALREADY_RUNNING")
                {
                    MessageBox.Show("Một tiến trình SteamCMD đang chạy. Vui lòng đợi hoặc dừng tiến trình hiện tại.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else if (responseStr == "NO_PROFILES")
                {
                    MessageBox.Show("Không có cấu hình nào trên dịch vụ!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"Không thể chạy tất cả cấu hình: {responseStr}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Lỗi khi chạy tất cả cấu hình: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

private async void btnStop_Click(object sender, EventArgs e)
{
    if (!isConnectedToService) return;

    try
    {
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(ServiceAddress, ServicePort);
            using (NetworkStream stream = client.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes("STOP_OPERATION");
                await stream.WriteAsync(request, 0, request.Length);

                byte[] response = new byte[1024];
                int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                if (responseStr == "OPERATION_STOPPED")
                {
                    btnRun.Enabled = true;
                    btnRunAll.Enabled = true;
                    btnStop.Enabled = false;
                    progressBar.Visible = false;
                    statusLabel.Text = "Thao tác đã bị dừng";
                }
                else
                {
                    MessageBox.Show($"Không thể dừng thao tác: {responseStr}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Lỗi khi dừng thao tác: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

private async void btnDelete_Click(object sender, EventArgs e)
{
    if (currentProfile == null || cboProfiles.SelectedIndex <= 0)
    {
        MessageBox.Show("Vui lòng chọn cấu hình cần xóa!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        return;
    }

    DialogResult result = MessageBox.Show(
        $"Bạn có chắc muốn xóa cấu hình \"{currentProfile.ProfileName}\"?",
        "Xác nhận xóa",
        MessageBoxButtons.YesNo,
        MessageBoxIcon.Question);

    if (result == DialogResult.Yes)
    {
        if (!isConnectedToService)
        {
            MessageBox.Show("Không có kết nối đến dịch vụ. Vui lòng khởi động dịch vụ trước.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            using (TcpClient client = new TcpClient())
            {
                await client.ConnectAsync(ServiceAddress, ServicePort);
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] request = Encoding.UTF8.GetBytes($"DELETE_PROFILE {currentProfile.ProfileName}");
                    await stream.WriteAsync(request, 0, request.Length);

                    byte[] response = new byte[1024];
                    int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                    string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                    if (responseStr == "PROFILE_DELETED")
                    {
                        profiles.Remove(currentProfile);
                        cboProfiles.Items.Remove(currentProfile.ProfileName);
                        cboProfiles.SelectedIndex = 0;
                        currentProfile = null;

                        MessageBox.Show("Đã xóa cấu hình thành công!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        ClearFields();
                    }
                    else if (responseStr == "PROFILE_NOT_FOUND")
                    {
                        MessageBox.Show("Không tìm thấy cấu hình trên dịch vụ!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                    else
                    {
                        MessageBox.Show($"Không thể xóa cấu hình: {responseStr}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi xóa cấu hình: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

private async void chkAutoRun_CheckedChanged(object sender, EventArgs e)
{
    if (!isConnectedToService) return;

    try
    {
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(ServiceAddress, ServicePort);
            using (NetworkStream stream = client.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes($"TOGGLE_AUTORUN {chkAutoRun.Checked}");
                await stream.WriteAsync(request, 0, request.Length);

                byte[] response = new byte[1024];
                int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                if (responseStr == "AUTORUN_ENABLED")
                {
                    lblProfileStatus.Text = numericUpDownTimer.Value == 0 ?
                        "Chạy tất cả ngay lập tức" :
                        $"Chế độ chạy tự động: BẬT ({numericUpDownTimer.Value} giờ)";
                }
                else if (responseStr == "AUTORUN_DISABLED")
                {
                    lblProfileStatus.Text = "Chế độ chạy tự động: TẮT";
                }
                else if (responseStr == "NO_PROFILES")
                {
                    MessageBox.Show("Không có cấu hình nào để chạy tự động!", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    chkAutoRun.Checked = false;
                }
                else
                {
                    MessageBox.Show($"Không thể thay đổi chế độ tự động: {responseStr}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Lỗi khi thay đổi chế độ tự động: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        chkAutoRun.Checked = !chkAutoRun.Checked; // Revert the checkbox
    }
}

private async void numericUpDownTimer_ValueChanged(object sender, EventArgs e)
{
    if (!isConnectedToService) return;

    label8.Text = numericUpDownTimer.Value == 0 ? "chạy ngay" : "giờ";

    try
    {
        using (TcpClient client = new TcpClient())
        {
            await client.ConnectAsync(ServiceAddress, ServicePort);
            using (NetworkStream stream = client.GetStream())
            {
                byte[] request = Encoding.UTF8.GetBytes($"SET_TIMER {numericUpDownTimer.Value}");
                await stream.WriteAsync(request, 0, request.Length);

                byte[] response = new byte[1024];
                int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                if (responseStr == "TIMER_SET")
                {
                    if (chkAutoRun.Checked)
                    {
                        lblProfileStatus.Text = numericUpDownTimer.Value == 0 ?
                            "Chạy tất cả ngay lập tức" :
                            $"Chế độ chạy tự động: BẬT ({numericUpDownTimer.Value} giờ)";
                    }
                }
                else
                {
                    richTextLog.AppendText($"Không thể đặt thời gian tự động: {responseStr}\n");
                }
            }
        }
    }
    catch (Exception ex)
    {
        richTextLog.AppendText($"Lỗi khi đặt thời gian tự động: {ex.Message}\n");
    }
}

private void AttachEventHandlers()
{
    cboProfiles.SelectedIndexChanged += cboProfiles_SelectedIndexChanged;
    btnBrowse.Click += btnBrowse_Click;
    btnSave.Click += btnSave_Click;
    btnDelete.Click += btnDelete_Click;
    btnRun.Click += btnRun_Click;
    btnStop.Click += btnStop_Click;
    btnAbout.Click += OnBtnAboutClick;
    btnRunAll.Click += btnRunAll_Click;
    btnServiceControl.Click += btnServiceControl_Click;

    numericUpDownTimer.ValueChanged += numericUpDownTimer_ValueChanged;
    chkAutoRun.CheckedChanged += chkAutoRun_CheckedChanged;
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

    btnServiceControl.BackColor = Color.FromArgb(100, 100, 100);
    btnServiceControl.ForeColor = Color.White;
    btnServiceControl.MouseEnter += (s, e) => btnServiceControl.BackColor = Color.FromArgb(80, 80, 80);
    btnServiceControl.MouseLeave += (s, e) => btnServiceControl.BackColor = Color.FromArgb(100, 100, 100);
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

    // Không giải mã mật khẩu trực tiếp mà dùng dấu sao để bảo mật
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

private async void btnSave_Click(object sender, EventArgs e)
{
    await SaveProfileToService();
}

private void OnBtnAboutClick(object sender, EventArgs e)
{
    MessageBox.Show(
        "STEAM CMD SERVICE\n" +
        "Phiên bản Windows Service\n\n" +
        "Dịch vụ này cho phép bạn tự động cập nhật game và ứng dụng Steam ngay cả khi không đăng nhập vào máy tính.\n\n" +
        "Tác giả: Gà Luộc",
        "Thông tin",
        MessageBoxButtons.OK,
        MessageBoxIcon.Information);
}

protected override void OnFormClosing(FormClosingEventArgs e)
{
    // Dừng các timer
    if (logsRefreshTimer != null)
    {
        logsRefreshTimer.Stop();
        logsRefreshTimer.Dispose();
    }

    if (statusRefreshTimer != null)
    {
        statusRefreshTimer.Stop();
        statusRefreshTimer.Dispose();
    }

    base.OnFormClosing(e);
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
            using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
            {
                cs.Write(cipherBytes, 0, cipherBytes.Length);
                cs.Close();
            }
            return Encoding.Unicode.GetString(ms.ToArray());
        }
    }
}

// Phương thức cập nhật UI dựa trên trạng thái kết nối
private void UpdateUIBasedOnConnectionState()
{
    btnServiceControl.Text = serviceState.IsConnected ? "Dừng Service" : "Khởi động Service";
    btnRun.Enabled = serviceState.IsConnected && !serviceState.IsProcessRunning;
    btnRunAll.Enabled = serviceState.IsConnected && !serviceState.IsProcessRunning;
    btnStop.Enabled = serviceState.IsConnected && serviceState.IsProcessRunning;
    btnSave.Enabled = serviceState.IsConnected;
    btnDelete.Enabled = serviceState.IsConnected && currentProfile != null;
    cboProfiles.Enabled = serviceState.IsConnected;
    progressBar.Visible = serviceState.IsProcessRunning;

    if (serviceState.IsProcessRunning)
    {
        progressBar.Style = ProgressBarStyle.Marquee;
    }
    else
    {
        progressBar.Style = ProgressBarStyle.Continuous;
    }

    // Cập nhật thông tin AutoRun
    chkAutoRun.Checked = serviceState.IsAutoRunEnabled;
    numericUpDownTimer.Value = serviceState.AutoRunHours;
    label8.Text = serviceState.AutoRunHours == 0 ? "chạy ngay" : "giờ";
}

// Phương thức kiểm tra và tạo thư mục nếu không tồn tại
private void EnsureDirectoryExists(string path)
{
    if (!Directory.Exists(path))
    {
        try
        {
            Directory.CreateDirectory(path);
            richTextLog.AppendText($"Đã tạo thư mục: {path}\n");
        }
        catch (Exception ex)
        {
            richTextLog.AppendText($"Lỗi khi tạo thư mục {path}: {ex.Message}\n");
        }
    }
}

// Phương thức xác thực dữ liệu nhập vào
private bool ValidateProfileData()
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

    bool isNewProfile = currentProfile == null;
    if (isNewProfile)
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

    return true;
}

// Phương thức reset giao diện người dùng khi mất kết nối
private void ResetUIForDisconnectedState()
{
    isConnectedToService = false;
    btnServiceControl.Text = "Khởi động Service";
    statusLabel.Text = "Không có kết nối đến service";

    btnRun.Enabled = false;
    btnRunAll.Enabled = false;
    btnStop.Enabled = false;
    progressBar.Visible = false;

    serviceState.IsConnected = false;
    serviceState.IsProcessRunning = false;

    UpdateUIBasedOnConnectionState();
}

// Phương thức kiểm tra cổng tcp đã được sử dụng chưa
private bool IsTcpPortInUse(int port)
{
    try
    {
        var ipGlobalProperties = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties();
        var ipEndPoints = ipGlobalProperties.GetActiveTcpListeners();

        return ipEndPoints.Any(endPoint => endPoint.Port == port);
    }
    catch
    {
        return false;
    }
}

// Phương thức kiểm tra nếu service đã được cài đặt
private bool IsServiceInstalled()
{
    try
    {
        ServiceController[] services = ServiceController.GetServices();
        return services.Any(s => s.ServiceName == ServiceName);
    }
    catch
    {
        return false;
    }
}

// Phương thức kiểm tra trạng thái service
private ServiceControllerStatus GetServiceStatus()
{
    try
    {
        using (ServiceController sc = new ServiceController(ServiceName))
        {
            return sc.Status;
        }
    }
    catch
    {
        return ServiceControllerStatus.Stopped;
    }
}

// Phương thức gỡ cài đặt service
private async Task UninstallService()
{
    try
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "sc.exe",
            Arguments = $"delete {ServiceName}",
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(psi))
        {
            process.WaitForExit();
            if (process.ExitCode == 0)
            {
                MessageBox.Show(
                    "Dịch vụ đã được gỡ cài đặt thành công.",
                    "Gỡ cài đặt thành công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                ResetUIForDisconnectedState();
            }
            else
            {
                MessageBox.Show(
                    "Không thể gỡ cài đặt dịch vụ. Mã lỗi: " + process.ExitCode,
                    "Lỗi gỡ cài đặt",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Lỗi khi gỡ cài đặt dịch vụ: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}

// Bổ sung menu contextual cho gỡ cài đặt service
private void AddServiceContextMenu()
{
    ContextMenuStrip serviceMenu = new ContextMenuStrip();
    ToolStripMenuItem uninstallItem = new ToolStripMenuItem("Gỡ cài đặt Service");
    uninstallItem.Click += async (s, e) =>
    {
        if (MessageBox.Show(
            "Bạn có chắc chắn muốn gỡ cài đặt dịch vụ SteamCmdService?\n" +
            "Việc này sẽ dừng dịch vụ và xóa nó khỏi hệ thống.",
            "Xác nhận gỡ cài đặt",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning) == DialogResult.Yes)
        {
            await UninstallService();
        }
    };

    serviceMenu.Items.Add(uninstallItem);
    btnServiceControl.ContextMenuStrip = serviceMenu;
}
    }
}iveBytes pdb = new Rfc2898DeriveBytes(encryptionKey, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
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
        Rfc2898Derusing System;
        using System.Collections.Generic;
        using System.Diagnostics;
        using System.Drawing;
        using System.IO;
        using System.Net.Sockets;
        using System.Security.Cryptography;
        using System.ServiceProcess;
        using System.Text;
        using System.Threading.Tasks;
        using System.Windows.Forms;
        using System.Xml.Serialization;

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
    }

    public partial class MainForm : Form
    {
        private class ConnectionState
        {
            public bool IsConnected { get; set; }
            public bool IsProcessRunning { get; set; }
            public string CurrentProfile { get; set; }
            public bool IsAutoRunEnabled { get; set; }
            public int AutoRunHours { get; set; }
            public DateTime LastRefresh { get; set; }
        }

        private const string ServiceName = "SteamCmdService";
        private const string ServiceAddress = "localhost";
        private const int ServicePort = 61188;
        private readonly string configFolder;
        private readonly string encryptionKey = "yourEncryptionKey123!@#";
        private List<GameProfile> profiles = new List<GameProfile>();
        private GameProfile currentProfile = null;
        private System.Timers.Timer logsRefreshTimer;
        private System.Timers.Timer statusRefreshTimer;
        private bool autoRefreshLogs = true;
        private bool isConnectedToService = false;
        private string lastStatus = "";
        private ConnectionState serviceState = new ConnectionState
        {
            IsConnected = false,
            IsProcessRunning = false,
            CurrentProfile = null,
            IsAutoRunEnabled = false,
            AutoRunHours = 1,
            LastRefresh = DateTime.MinValue
        };

        // Thêm biến lưu thông tin đăng nhập gốc
        private string originalUsername = "";
        private string originalPassword = "";

        public MainForm()
        {
            InitializeComponent();

            // Khởi tạo đường dẫn
            configFolder = Path.Combine(Application.StartupPath, "Profiles");

            if (!Directory.Exists(configFolder))
            {
                Directory.CreateDirectory(configFolder);
            }

            ApplyModernStyle();
            SetupRefreshTimers();
            CheckServiceStatus();

            // Thêm menu contextual cho service control
            AddServiceContextMenu();

            // Khôi phục lại các event handlers
            AttachEventHandlers();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Khởi tạo UI
            statusLabel.Text = "Đang kiểm tra trạng thái service...";
            lblProfileStatus.Text = "Chưa kết nối đến service";

            // Khởi tạo thư mục nếu cần
            EnsureDirectoryExists(configFolder);

            // Kiểm tra trạng thái kết nối
            CheckServiceStatus();

            // Khởi động các tác vụ nền
            StartBackgroundTasks();
        }

        private void SetupRefreshTimers()
        {
            // Timer để cập nhật logs từ service
            logsRefreshTimer = new System.Timers.Timer(1000);
            logsRefreshTimer.Elapsed += async (s, e) =>
            {
                if (autoRefreshLogs && isConnectedToService)
                {
                    await RefreshLogsFromService();
                }
            };
            logsRefreshTimer.AutoReset = true;
            logsRefreshTimer.Start();

            // Timer để cập nhật trạng thái từ service
            statusRefreshTimer = new System.Timers.Timer(3000);
            statusRefreshTimer.Elapsed += async (s, e) =>
            {
                if (isConnectedToService)
                {
                    await RefreshStatusFromService();
                }
            };
            statusRefreshTimer.AutoReset = true;
            statusRefreshTimer.Start();
        }

        // Phương thức bắt đầu các tác vụ nền khi form khởi tạo
        private void StartBackgroundTasks()
        {
            // Khởi tạo task làm mới trạng thái kết nối định kỳ
            Task.Run(async () => await RefreshServiceConnectionPeriodically());

            // Khởi tạo task làm mới log định kỳ
            Task.Run(async () => await RefreshLogsFromServicePeriodically());
        }

        // Phương thức làm mới trạng thái kết nối với service định kỳ
        private async Task RefreshServiceConnectionPeriodically()
        {
            while (true)
            {
                try
                {
                    // Nếu đã kết nối trước đó, kiểm tra kết nối
                    if (isConnectedToService)
                    {
                        await RefreshStatusFromService();
                    }
                    // Nếu chưa kết nối, thử kết nối lại
                    else
                    {
                        await ConnectToService();
                    }
                }
                catch (Exception)
                {
                    // Nếu có lỗi, đánh dấu là không kết nối
                    if (isConnectedToService)
                    {
                        this.Invoke(new Action(() =>
                        {
                            isConnectedToService = false;
                            btnServiceControl.Text = "Khởi động Service";
                            statusLabel.Text = "Mất kết nối đến service";
                            UpdateUIBasedOnConnectionState();
                        }));
                    }
                }

                // Chờ một khoảng thời gian trước khi kiểm tra lại
                await Task.Delay(10000); // 10 giây
            }
        }

        // Phương thức lấy và hiển thị thông tin log từ service
        private async Task RefreshLogsFromServicePeriodically()
        {
            while (true)
            {
                if (autoRefreshLogs && isConnectedToService)
                {
                    await RefreshLogsFromService();
                }
                await Task.Delay(2000); // 2 giây
            }
        }

        private void CheckServiceStatus()
        {
            try
            {
                ServiceController sc = new ServiceController(ServiceName);
                lblProfileStatus.Text = $"Trạng thái dịch vụ: {GetServiceStatusText(sc.Status)}";

                // Thử kết nối tới service nếu nó đang chạy
                if (sc.Status == ServiceControllerStatus.Running)
                {
                    Task.Run(async () =>
                    {
                        await ConnectToService();
                        await RefreshStatusFromService();
                        await GetProfilesFromService();
                    });
                }
                else
                {
                    isConnectedToService = false;
                    btnServiceControl.Text = "Khởi động Service";
                    statusLabel.Text = "Service đang dừng. Nhấn 'Khởi động Service' để bắt đầu.";
                }
            }
            catch (Exception ex)
            {
                lblProfileStatus.Text = "Dịch vụ chưa được cài đặt hoặc lỗi kết nối.";
                statusLabel.Text = ex.Message;
                isConnectedToService = false;
                btnServiceControl.Text = "Cài đặt Service";
            }
        }

        private string GetServiceStatusText(ServiceControllerStatus status)
        {
            switch (status)
            {
                case ServiceControllerStatus.Running:
                    return "Đang chạy";
                case ServiceControllerStatus.Stopped:
                    return "Đã dừng";
                case ServiceControllerStatus.Paused:
                    return "Tạm dừng";
                case ServiceControllerStatus.StartPending:
                    return "Đang khởi động...";
                case ServiceControllerStatus.StopPending:
                    return "Đang dừng...";
                case ServiceControllerStatus.ContinuePending:
                    return "Đang tiếp tục...";
                case ServiceControllerStatus.PausePending:
                    return "Đang tạm dừng...";
                default:
                    return "Không xác định";
            }
        }

        private async Task ConnectToService()
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(ServiceAddress, ServicePort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] request = Encoding.UTF8.GetBytes("GET_STATUS");
                        await stream.WriteAsync(request, 0, request.Length);

                        byte[] response = new byte[1024];
                        int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                        if (bytesRead > 0)
                        {
                            this.Invoke(new Action(() =>
                            {
                                isConnectedToService = true;
                                btnServiceControl.Text = "Dừng Service";
                                statusLabel.Text = "Đã kết nối tới service";
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    isConnectedToService = false;
                    btnServiceControl.Text = "Khởi động Service";
                    statusLabel.Text = $"Không thể kết nối tới service: {ex.Message}";
                }));
            }
        }

        private async Task GetProfilesFromService()
        {
            if (!isConnectedToService) return;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(ServiceAddress, ServicePort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] request = Encoding.UTF8.GetBytes("GET_PROFILES");
                        await stream.WriteAsync(request, 0, request.Length);

                        byte[] response = new byte[4096];
                        int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                        string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                        this.Invoke(new Action(() =>
                        {
                            // Xóa danh sách hiện tại
                            profiles.Clear();
                            cboProfiles.Items.Clear();
                            cboProfiles.Items.Add("-- Tạo cấu hình mới --");

                            // Thêm các profile từ service
                            if (responseStr != "NO_PROFILES")
                            {
                                string[] profileNames = responseStr.Split(',');
                                foreach (string profileName in profileNames)
                                {
                                    cboProfiles.Items.Add(profileName);
                                    // Lấy chi tiết profile sau
                                    Task.Run(async () => await GetProfileDetailsFromService(profileName));
                                }
                            }

                            if (cboProfiles.Items.Count > 0) cboProfiles.SelectedIndex = 0;
                            statusLabel.Text = "Đã tải danh sách cấu hình từ service";
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    statusLabel.Text = $"Lỗi khi lấy danh sách cấu hình: {ex.Message}";
                }));
            }
        }

        private async Task GetProfileDetailsFromService(string profileName)
        {
            if (!isConnectedToService) return;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(ServiceAddress, ServicePort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] request = Encoding.UTF8.GetBytes($"GET_PROFILE_DETAILS {profileName}");
                        await stream.WriteAsync(request, 0, request.Length);

                        byte[] response = new byte[8192];
                        int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                        string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                        if (responseStr == "PROFILE_NOT_FOUND") return;

                        // Đọc profile XML
                        XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                        using (StringReader reader = new StringReader(responseStr))
                        {
                            GameProfile profile = (GameProfile)serializer.Deserialize(reader);
                            this.Invoke(new Action(() =>
                            {
                                // Thêm vào danh sách profile
                                int existingIndex = profiles.FindIndex(p => p.ProfileName == profile.ProfileName);
                                if (existingIndex >= 0)
                                {
                                    profiles[existingIndex] = profile;
                                }
                                else
                                {
                                    profiles.Add(profile);
                                }
                            }));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                this.Invoke(new Action(() =>
                {
                    richTextLog.AppendText($"Lỗi khi lấy chi tiết cấu hình {profileName}: {ex.Message}\n");
                }));
            }
        }

        private async Task<bool> SaveProfileToService()
        {
            if (!isConnectedToService)
            {
                MessageBox.Show("Không có kết nối đến dịch vụ. Vui lòng khởi động dịch vụ trước.", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            if (!ValidateProfileData())
            {
                return false;
            }

            bool isNewProfile = currentProfile == null;
            GameProfile profile = currentProfile ?? new GameProfile();

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
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(ServiceAddress, ServicePort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        // Serialize profile to XML
                        XmlSerializer serializer = new XmlSerializer(typeof(GameProfile));
                        string profileXml;
                        using (StringWriter writer = new StringWriter())
                        {
                            serializer.Serialize(writer, profile);
                            profileXml = writer.ToString();
                        }

                        byte[] request = Encoding.UTF8.GetBytes("SAVE_PROFILE " + profileXml);
                        await stream.WriteAsync(request, 0, request.Length);

                        byte[] response = new byte[1024];
                        int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                        string responseStr = Encoding.UTF8.GetString(response, 0, bytesRead).Trim();

                        if (responseStr == "PROFILE_SAVED")
                        {
                            // Update local state
                            if (isNewProfile)
                            {
                                profiles.Add(profile);
                                cboProfiles.Items.Add(profile.ProfileName);
                                cboProfiles.SelectedIndex = cboProfiles.Items.Count - 1;
                            }
                            currentProfile = profile;
                            statusLabel.Text = "Đã lưu cấu hình: " + profile.ProfileName;
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("Lỗi khi lưu cấu hình: " + responseStr, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi khi lưu cấu hình: " + ex.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private async Task RefreshLogsFromService()
        {
            if (!isConnectedToService) return;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(ServiceAddress, ServicePort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] request = Encoding.UTF8.GetBytes("GET_LOGS");
                        await stream.WriteAsync(request, 0, request.Length);

                        byte[] response = new byte[32768]; // 32KB buffer for logs
                        int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                        string logs = Encoding.UTF8.GetString(response, 0, bytesRead);

                        if (!string.IsNullOrEmpty(logs))
                        {
                            this.Invoke(new Action(() =>
                            {
                                richTextLog.AppendText(logs);
                                richTextLog.SelectionStart = richTextLog.Text.Length;
                                richTextLog.ScrollToCaret();
                            }));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore log refresh errors to prevent flooding the UI with error messages
            }
        }

        private async Task RefreshStatusFromService()
        {
            if (!isConnectedToService) return;

            try
            {
                using (TcpClient client = new TcpClient())
                {
                    await client.ConnectAsync(ServiceAddress, ServicePort);
                    using (NetworkStream stream = client.GetStream())
                    {
                        byte[] request = Encoding.UTF8.GetBytes("GET_STATUS");
                        await stream.WriteAsync(request, 0, request.Length);

                        byte[] response = new byte[1024];
                        int bytesRead = await stream.ReadAsync(response, 0, response.Length);
                        string status = Encoding.UTF8.GetString(response, 0, bytesRead);

                        if (status != lastStatus)
                        {
                            lastStatus = status;

                            this.Invoke(new Action(() =>
                            {
                                ParseAndDisplayStatus(status);
                            }));
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Ignore status refresh errors
                this.Invoke(new Action(() =>
                {
                    isConnectedToService = false;
                    btnServiceControl.Text = "Khởi động Service";
                }));
            }
        }

        private void ParseAndDisplayStatus(string status)
        {