using System;
using System.Collections;
using System.ComponentModel;
using System.Configuration.Install;
using System.ServiceProcess;

namespace SteamCmdService
{
    [RunInstaller(true)]
    public partial class ProjectInstaller : Installer
    {
        // Khai báo biến rõ ràng
        private ServiceProcessInstaller serviceProcessInstaller;
        private ServiceInstaller serviceInstaller;

        public ProjectInstaller()
        {
            InitializeComponent();
        }

        // Thêm phương thức InitializeComponent để tránh lỗi
        private void InitializeComponent()
        {
            // Khởi tạo đối tượng
            this.serviceProcessInstaller = new ServiceProcessInstaller();
            this.serviceInstaller = new ServiceInstaller();

            // Cấu hình ServiceProcessInstaller
            this.serviceProcessInstaller.Account = ServiceAccount.LocalSystem;
            this.serviceProcessInstaller.Password = null;
            this.serviceProcessInstaller.Username = null;

            // Cấu hình ServiceInstaller
            this.serviceInstaller.Description = "Quản lý tự động hóa SteamCMD";
            this.serviceInstaller.DisplayName = "Steam CMD Automation Service";
            this.serviceInstaller.ServiceName = "SteamCmdService";
            this.serviceInstaller.StartType = ServiceStartMode.Automatic;

            // Thêm các installer vào collection
            this.Installers.AddRange(new Installer[] {
                this.serviceProcessInstaller,
                this.serviceInstaller
            });
        }
    }
}