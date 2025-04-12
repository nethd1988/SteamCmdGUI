namespace SteamCmdGUI
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.cboProfiles = new System.Windows.Forms.ComboBox();
            this.btnDelete = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.txtProfileName = new System.Windows.Forms.TextBox();
            this.txtInstallDir = new System.Windows.Forms.TextBox();
            this.btnBrowse = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.txtAppID = new System.Windows.Forms.TextBox();
            this.label6 = new System.Windows.Forms.Label();
            this.txtArguments = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnRunAll = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnRun = new System.Windows.Forms.Button();
            this.btnStop = new System.Windows.Forms.Button();
            this.richTextLog = new System.Windows.Forms.RichTextBox();
            this.statusLabel = new System.Windows.Forms.Label();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.groupBoxAutoRun = new System.Windows.Forms.GroupBox();
            this.numericUpDownTimer = new System.Windows.Forms.NumericUpDown();
            this.label8 = new System.Windows.Forms.Label();
            this.chkAutoRun = new System.Windows.Forms.CheckBox();
            this.btnAbout = new System.Windows.Forms.Button();
            this.chkValidate = new System.Windows.Forms.CheckBox();
            this.lblProfileStatus = new System.Windows.Forms.Label();
            this.btnServiceControl = new System.Windows.Forms.Button();
            this.listBoxProfiles = new System.Windows.Forms.ListBox();
            this.groupBoxAutoRun.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimer)).BeginInit();
            this.SuspendLayout();

            // lblProfileStatus
            this.lblProfileStatus.AutoSize = true;
            this.lblProfileStatus.Location = new System.Drawing.Point(12, 12);
            this.lblProfileStatus.Name = "lblProfileStatus";
            this.lblProfileStatus.Size = new System.Drawing.Size(0, 13);
            this.lblProfileStatus.TabIndex = 26;

            // label1
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 38);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(75, 13);
            this.label1.TabIndex = 2;
            this.label1.Text = "Chọn cấu hình:";

            // cboProfiles
            this.cboProfiles.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboProfiles.FormattingEnabled = true;
            this.cboProfiles.Location = new System.Drawing.Point(120, 35);
            this.cboProfiles.Name = "cboProfiles";
            this.cboProfiles.Size = new System.Drawing.Size(200, 21);
            this.cboProfiles.TabIndex = 0;

            // btnDelete
            this.btnDelete.Location = new System.Drawing.Point(326, 34);
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size(80, 23);
            this.btnDelete.TabIndex = 1;
            this.btnDelete.Text = "Xóa";
            this.btnDelete.UseVisualStyleBackColor = true;

            // label2
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(12, 65);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(71, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Tên cấu hình:";

            // txtProfileName
            this.txtProfileName.Location = new System.Drawing.Point(120, 62);
            this.txtProfileName.Name = "txtProfileName";
            this.txtProfileName.Size = new System.Drawing.Size(286, 20);
            this.txtProfileName.TabIndex = 4;

            // label3
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 91);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(81, 13);
            this.label3.TabIndex = 7;
            this.label3.Text = "Đường dẫn cài đặt:";

            // txtInstallDir
            this.txtInstallDir.Location = new System.Drawing.Point(120, 88);
            this.txtInstallDir.Name = "txtInstallDir";
            this.txtInstallDir.Size = new System.Drawing.Size(200, 20);
            this.txtInstallDir.TabIndex = 5;

            // btnBrowse
            this.btnBrowse.Location = new System.Drawing.Point(326, 87);
            this.btnBrowse.Name = "btnBrowse";
            this.btnBrowse.Size = new System.Drawing.Size(80, 23);
            this.btnBrowse.TabIndex = 6;
            this.btnBrowse.Text = "Duyệt...";
            this.btnBrowse.UseVisualStyleBackColor = true;

            // label4
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(12, 117);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(72, 13);
            this.label4.TabIndex = 9;
            this.label4.Text = "Tên đăng nhập:";

            // txtUsername
            this.txtUsername.Location = new System.Drawing.Point(120, 114);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(286, 20);
            this.txtUsername.TabIndex = 8;

            // label5
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(12, 143);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(52, 13);
            this.label5.TabIndex = 11;
            this.label5.Text = "Mật khẩu:";

            // txtPassword
            this.txtPassword.Location = new System.Drawing.Point(120, 140);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(286, 20);
            this.txtPassword.TabIndex = 10;
            this.txtPassword.UseSystemPasswordChar = true;

            // label6
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(12, 169);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(52, 13);
            this.label6.TabIndex = 14;
            this.label6.Text = "ID Game:";

            // txtAppID
            this.txtAppID.Location = new System.Drawing.Point(120, 166);
            this.txtAppID.Name = "txtAppID";
            this.txtAppID.Size = new System.Drawing.Size(286, 20);
            this.txtAppID.TabIndex = 12;

            // label7
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(12, 195);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(74, 13);
            this.label7.TabIndex = 16;
            this.label7.Text = "Tham số bổ sung:";

            // txtArguments
            this.txtArguments.Location = new System.Drawing.Point(120, 192);
            this.txtArguments.Name = "txtArguments";
            this.txtArguments.Size = new System.Drawing.Size(286, 20);
            this.txtArguments.TabIndex = 15;
            this.txtArguments.Text = "-norepairfiles -noverifyfiles";

            // chkValidate
            this.chkValidate.AutoSize = true;
            this.chkValidate.Location = new System.Drawing.Point(120, 218);
            this.chkValidate.Name = "chkValidate";
            this.chkValidate.Size = new System.Drawing.Size(92, 17);
            this.chkValidate.TabIndex = 25;
            this.chkValidate.Text = "Kiểm tra game";
            this.chkValidate.UseVisualStyleBackColor = true;

            // btnRunAll
            this.btnRunAll.Location = new System.Drawing.Point(12, 245);
            this.btnRunAll.Name = "btnRunAll";
            this.btnRunAll.Size = new System.Drawing.Size(80, 23);
            this.btnRunAll.TabIndex = 17;
            this.btnRunAll.Text = "Chạy tất cả";
            this.btnRunAll.UseVisualStyleBackColor = true;

            // btnSave
            this.btnSave.Location = new System.Drawing.Point(98, 245);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(80, 23);
            this.btnSave.TabIndex = 18;
            this.btnSave.Text = "Lưu";
            this.btnSave.UseVisualStyleBackColor = true;

            // btnRun
            this.btnRun.Location = new System.Drawing.Point(184, 245);
            this.btnRun.Name = "btnRun";
            this.btnRun.Size = new System.Drawing.Size(110, 23);
            this.btnRun.TabIndex = 19;
            this.btnRun.Text = "CHẠY STEAMCMD";
            this.btnRun.UseVisualStyleBackColor = true;

            // btnStop
            this.btnStop.Location = new System.Drawing.Point(300, 245);
            this.btnStop.Name = "btnStop";
            this.btnStop.Size = new System.Drawing.Size(106, 23);
            this.btnStop.TabIndex = 20;
            this.btnStop.Text = "DỪNG";
            this.btnStop.UseVisualStyleBackColor = true;
            this.btnStop.Enabled = false;

            // richTextLog
            this.richTextLog.Location = new System.Drawing.Point(12, 274);
            this.richTextLog.Name = "richTextLog";
            this.richTextLog.Size = new System.Drawing.Size(394, 127);
            this.richTextLog.TabIndex = 21;
            this.richTextLog.Text = "";
            this.richTextLog.ReadOnly = true;

            // statusLabel
            this.statusLabel.AutoSize = true;
            this.statusLabel.Location = new System.Drawing.Point(12, 407);
            this.statusLabel.Name = "statusLabel";
            this.statusLabel.Size = new System.Drawing.Size(0, 13);
            this.statusLabel.TabIndex = 22;

            // progressBar
            this.progressBar.Location = new System.Drawing.Point(12, 423);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(394, 10);
            this.progressBar.TabIndex = 23;
            this.progressBar.Visible = false;

            // groupBoxAutoRun
            this.groupBoxAutoRun.Controls.Add(this.numericUpDownTimer);
            this.groupBoxAutoRun.Controls.Add(this.label8);
            this.groupBoxAutoRun.Controls.Add(this.chkAutoRun);
            this.groupBoxAutoRun.Location = new System.Drawing.Point(12, 439);
            this.groupBoxAutoRun.Name = "groupBoxAutoRun";
            this.groupBoxAutoRun.Size = new System.Drawing.Size(394, 60);
            this.groupBoxAutoRun.TabIndex = 24;
            this.groupBoxAutoRun.TabStop = false;
            this.groupBoxAutoRun.Text = "Chạy Tự Động";

            // chkAutoRun
            this.chkAutoRun.AutoSize = true;
            this.chkAutoRun.Location = new System.Drawing.Point(6, 26);
            this.chkAutoRun.Name = "chkAutoRun";
            this.chkAutoRun.Size = new System.Drawing.Size(222, 17);
            this.chkAutoRun.TabIndex = 0;
            this.chkAutoRun.Text = "Chạy tự động tất cả cấu hình sau mỗi";
            this.chkAutoRun.UseVisualStyleBackColor = true;

            // numericUpDownTimer
            this.numericUpDownTimer.Location = new System.Drawing.Point(234, 25);
            this.numericUpDownTimer.Maximum = new decimal(new int[] { 24, 0, 0, 0 });
            this.numericUpDownTimer.Minimum = new decimal(new int[] { 0, 0, 0, 0 });
            this.numericUpDownTimer.Name = "numericUpDownTimer";
            this.numericUpDownTimer.Size = new System.Drawing.Size(50, 20);
            this.numericUpDownTimer.TabIndex = 2;
            this.numericUpDownTimer.Value = new decimal(new int[] { 1, 0, 0, 0 });

            // label8
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(290, 27);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(24, 13);
            this.label8.TabIndex = 1;
            this.label8.Text = "giờ";

            // btnAbout
            this.btnAbout.Location = new System.Drawing.Point(326, 5);
            this.btnAbout.Name = "btnAbout";
            this.btnAbout.Size = new System.Drawing.Size(80, 23);
            this.btnAbout.TabIndex = 25;
            this.btnAbout.Text = "Giới thiệu";
            this.btnAbout.UseVisualStyleBackColor = true;

            // btnServiceControl
            this.btnServiceControl.Location = new System.Drawing.Point(15, 5);
            this.btnServiceControl.Name = "btnServiceControl";
            this.btnServiceControl.Size = new System.Drawing.Size(120, 23);
            this.btnServiceControl.TabIndex = 27;
            this.btnServiceControl.Text = "Khởi động Service";
            this.btnServiceControl.UseVisualStyleBackColor = true;

            // listBoxProfiles
            this.listBoxProfiles.FormattingEnabled = true;
            this.listBoxProfiles.Location = new System.Drawing.Point(426, 12);
            this.listBoxProfiles.Name = "listBoxProfiles";
            this.listBoxProfiles.Size = new System.Drawing.Size(150, 480);
            this.listBoxProfiles.TabIndex = 28;
            this.listBoxProfiles.Visible = false;

            // MainForm
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(420, 505);
            this.Controls.Add(this.listBoxProfiles);
            this.Controls.Add(this.btnServiceControl);
            this.Controls.Add(this.lblProfileStatus);
            this.Controls.Add(this.chkValidate);
            this.Controls.Add(this.btnAbout);
            this.Controls.Add(this.groupBoxAutoRun);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.statusLabel);
            this.Controls.Add(this.richTextLog);
            this.Controls.Add(this.btnStop);
            this.Controls.Add(this.btnRun);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.btnRunAll);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.txtArguments);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.txtAppID);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnBrowse);
            this.Controls.Add(this.txtInstallDir);
            this.Controls.Add(this.txtProfileName);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.btnDelete);
            this.Controls.Add(this.cboProfiles);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "STEAM AuTo GL - Service Edition";
            this.groupBoxAutoRun.ResumeLayout(false);
            this.groupBoxAutoRun.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numericUpDownTimer)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.ComboBox cboProfiles;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox txtProfileName;
        private System.Windows.Forms.TextBox txtInstallDir;
        private System.Windows.Forms.Button btnBrowse;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox txtAppID;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.TextBox txtArguments;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Button btnRunAll;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnRun;
        private System.Windows.Forms.Button btnStop;
        private System.Windows.Forms.RichTextBox richTextLog;
        private System.Windows.Forms.Label statusLabel;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.GroupBox groupBoxAutoRun;
        private System.Windows.Forms.NumericUpDown numericUpDownTimer;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.CheckBox chkAutoRun;
        private System.Windows.Forms.Button btnAbout;
        private System.Windows.Forms.CheckBox chkValidate;
        private System.Windows.Forms.Label lblProfileStatus;
        private System.Windows.Forms.Button btnServiceControl;
        private System.Windows.Forms.ListBox listBoxProfiles;
    }
}