using Microsoft.Owin.Hosting;
using Newtonsoft.Json;
using Owin;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Windows.Forms;

namespace SteamCmdServer
{
    public partial class Form1 : Form
    {
        private string connectionString = "Data Source=profiles.db;Version=3;";
        private IDisposable server;
        private string serverUrl = "http://localhost:9000/";

        public Form1()
        {
            InitializeComponent();
            InitializeDatabase();
            LoadProfiles();
            txtServerUrl.Text = serverUrl; // Hiển thị URL mặc định
        }

        // Khởi tạo cơ sở dữ liệu SQLite
        private void InitializeDatabase()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string createTableQuery = @"
                    CREATE TABLE IF NOT EXISTS GameProfiles (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        ProfileName TEXT NOT NULL,
                        Username TEXT NOT NULL,
                        Password TEXT NOT NULL,
                        AppID TEXT NOT NULL,
                        InstallDir TEXT NOT NULL,
                        Arguments TEXT NOT NULL
                    )";
                using (var command = new SQLiteCommand(createTableQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        // Tải danh sách profile vào DataGridView
        private void LoadProfiles()
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                string selectQuery = "SELECT * FROM GameProfiles";
                using (var command = new SQLiteCommand(selectQuery, connection))
                {
                    using (var reader = command.ExecuteReader())
                    {
                        dgvProfiles.Rows.Clear();
                        while (reader.Read())
                        {
                            dgvProfiles.Rows.Add(reader["Id"], reader["ProfileName"], reader["Username"],
                                reader["Password"], reader["AppID"], reader["InstallDir"], reader["Arguments"]);
                        }
                    }
                }
            }
        }

        // Xử lý nút Thêm
        private void btnAdd_Click(object sender, EventArgs e)
        {
            using (var inputForm = new Form { Text = "Thêm Profile Mới", Size = new System.Drawing.Size(300, 400) })
            {
                TextBox txtProfileName = new TextBox { Top = 20, Left = 20, Width = 200, PlaceholderText = "Tên Profile" };
                TextBox txtUsername = new TextBox { Top = 60, Left = 20, Width = 200, PlaceholderText = "Username" };
                TextBox txtPassword = new TextBox { Top = 100, Left = 20, Width = 200, PlaceholderText = "Password" };
                TextBox txtAppID = new TextBox { Top = 140, Left = 20, Width = 200, PlaceholderText = "AppID" };
                TextBox txtInstallDir = new TextBox { Top = 180, Left = 20, Width = 200, PlaceholderText = "Thư mục cài đặt" };
                TextBox txtArguments = new TextBox { Top = 220, Left = 20, Width = 200, PlaceholderText = "Đối số" };
                Button btnSave = new Button { Text = "Lưu", Top = 260, Left = 20, Width = 80 };

                btnSave.Click += (s, ev) =>
                {
                    using (var connection = new SQLiteConnection(connectionString))
                    {
                        connection.Open();
                        string insertQuery = "INSERT INTO GameProfiles (ProfileName, Username, Password, AppID, InstallDir, Arguments) " +
                            "VALUES (@ProfileName, @Username, @Password, @AppID, @InstallDir, @Arguments)";
                        using (var command = new SQLiteCommand(insertQuery, connection))
                        {
                            command.Parameters.AddWithValue("@ProfileName", txtProfileName.Text);
                            command.Parameters.AddWithValue("@Username", txtUsername.Text);
                            command.Parameters.AddWithValue("@Password", txtPassword.Text);
                            command.Parameters.AddWithValue("@AppID", txtAppID.Text);
                            command.Parameters.AddWithValue("@InstallDir", txtInstallDir.Text);
                            command.Parameters.AddWithValue("@Arguments", txtArguments.Text);
                            command.ExecuteNonQuery();
                        }
                    }
                    inputForm.Close();
                    LoadProfiles();
                };

                inputForm.Controls.AddRange(new Control[] { txtProfileName, txtUsername, txtPassword, txtAppID, txtInstallDir, txtArguments, btnSave });
                inputForm.ShowDialog();
            }
        }

        // Xử lý nút Sửa
        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (dgvProfiles.SelectedRows.Count > 0)
            {
                var row = dgvProfiles.SelectedRows[0];
                int id = Convert.ToInt32(row.Cells["Id"].Value);

                using (var inputForm = new Form { Text = "Sửa Profile", Size = new System.Drawing.Size(300, 400) })
                {
                    TextBox txtProfileName = new TextBox { Top = 20, Left = 20, Width = 200, Text = row.Cells["ProfileName"].Value.ToString() };
                    TextBox txtUsername = new TextBox { Top = 60, Left = 20, Width = 200, Text = row.Cells["Username"].Value.ToString() };
                    TextBox txtPassword = new TextBox { Top = 100, Left = 20, Width = 200, Text = row.Cells["Password"].Value.ToString() };
                    TextBox txtAppID = new TextBox { Top = 140, Left = 20, Width = 200, Text = row.Cells["AppID"].Value.ToString() };
                    TextBox txtInstallDir = new TextBox { Top = 180, Left = 20, Width = 200, Text = row.Cells["InstallDir"].Value.ToString() };
                    TextBox txtArguments = new TextBox { Top = 220, Left = 20, Width = 200, Text = row.Cells["Arguments"].Value.ToString() };
                    Button btnSave = new Button { Text = "Lưu", Top = 260, Left = 20, Width = 80 };

                    btnSave.Click += (s, ev) =>
                    {
                        using (var connection = new SQLiteConnection(connectionString))
                        {
                            connection.Open();
                            string updateQuery = "UPDATE GameProfiles SET ProfileName = @ProfileName, Username = @Username, " +
                                "Password = @Password, AppID = @AppID, InstallDir = @InstallDir, Arguments = @Arguments WHERE Id = @Id";
                            using (var command = new SQLiteCommand(updateQuery, connection))
                            {
                                command.Parameters.AddWithValue("@ProfileName", txtProfileName.Text);
                                command.Parameters.AddWithValue("@Username", txtUsername.Text);
                                command.Parameters.AddWithValue("@Password", txtPassword.Text);
                                command.Parameters.AddWithValue("@AppID", txtAppID.Text);
                                command.Parameters.AddWithValue("@InstallDir", txtInstallDir.Text);
                                command.Parameters.AddWithValue("@Arguments", txtArguments.Text);
                                command.Parameters.AddWithValue("@Id", id);
                                command.ExecuteNonQuery();
                            }
                        }
                        inputForm.Close();
                        LoadProfiles();
                    };

                    inputForm.Controls.AddRange(new Control[] { txtProfileName, txtUsername, txtPassword, txtAppID, txtInstallDir, txtArguments, btnSave });
                    inputForm.ShowDialog();
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một profile để sửa!");
            }
        }

        // Xử lý nút Xóa
        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvProfiles.SelectedRows.Count > 0)
            {
                var row = dgvProfiles.SelectedRows[0];
                int id = Convert.ToInt32(row.Cells["Id"].Value);

                if (MessageBox.Show("Bạn có chắc muốn xóa profile này?", "Xác nhận", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    using (var connection = new SQLiteConnection(connectionString))
                    {
                        connection.Open();
                        string deleteQuery = "DELETE FROM GameProfiles WHERE Id = @Id";
                        using (var command = new SQLiteCommand(deleteQuery, connection))
                        {
                            command.Parameters.AddWithValue("@Id", id);
                            command.ExecuteNonQuery();
                        }
                    }
                    LoadProfiles();
                }
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một profile để xóa!");
            }
        }

        // Khởi động server
        private void btnStartServer_Click(object sender, EventArgs e)
        {
            if (server == null)
            {
                server = WebApp.Start<Startup>(serverUrl);
                MessageBox.Show("Server đã khởi động tại " + serverUrl);
            }
            else
            {
                MessageBox.Show("Server đang chạy rồi!");
            }
        }

        // Dọn dẹp khi đóng form
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            server?.Dispose();
            base.OnFormClosing(e);
        }
    }

    // Cấu hình server HTTP dùng OWIN
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.Run(context =>
            {
                if (context.Request.Path.Value == "/profiles")
                {
                    var profiles = new List<Dictionary<string, string>>();
                    using (var connection = new SQLiteConnection("Data Source=profiles.db;Version=3;"))
                    {
                        connection.Open();
                        string selectQuery = "SELECT * FROM GameProfiles";
                        using (var command = new SQLiteCommand(selectQuery, connection))
                        {
                            using (var reader = command.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    profiles.Add(new Dictionary<string, string>
                                    {
                                        { "Id", reader["Id"].ToString() },
                                        { "ProfileName", reader["ProfileName"].ToString() },
                                        { "Username", reader["Username"].ToString() },
                                        { "Password", reader["Password"].ToString() },
                                        { "AppID", reader["AppID"].ToString() },
                                        { "InstallDir", reader["InstallDir"].ToString() },
                                        { "Arguments", reader["Arguments"].ToString() }
                                    });
                                }
                            }
                        }
                    }
                    var json = JsonConvert.SerializeObject(profiles);
                    context.Response.ContentType = "application/json";
                    return context.Response.WriteAsync(json);
                }
                context.Response.StatusCode = 404;
                return context.Response.WriteAsync("Not Found");
            });
        }
    }
}