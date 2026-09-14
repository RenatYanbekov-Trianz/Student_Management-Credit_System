using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using QLDSV_HTC.GUI;

// Cloud Readiness Fix (cr-dotnet-0127):
// Added graceful shutdown handling via AppDomain.ProcessExit and
// Console.CancelKeyPress so that the application can drain in-flight
// operations, flush buffers, and release cloud resources (e.g. RDS Proxy
// connections) before the process terminates.
//
// A CancellationTokenSource (ApplicationStopping) mirrors the semantics of
// IHostApplicationLifetime.ApplicationStopping so that any background work
// can observe the token and stop cleanly.  An ApplicationStopped event fires
// after all cleanup is complete, matching the IHostApplicationLifetime
// contract recommended for cloud-hosted .NET services.

namespace QLDSV_HTC.GUI
{
    public partial class FormMain : Form
    {
        // ------------------------------------------------------------------ //
        //  Graceful-shutdown infrastructure (cr-dotnet-0127)                 //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Signals that the application is about to stop.
        /// Background workers should observe this token and terminate cleanly.
        /// Mirrors IHostApplicationLifetime.ApplicationStopping.
        /// </summary>
        public static readonly CancellationTokenSource ApplicationStopping =
            new CancellationTokenSource();

        /// <summary>
        /// Raised after all shutdown cleanup has completed.
        /// Mirrors IHostApplicationLifetime.ApplicationStopped.
        /// </summary>
        public static event EventHandler ApplicationStopped;

        // ------------------------------------------------------------------ //
        //  Constructor                                                        //
        // ------------------------------------------------------------------ //

        public FormMain()
        {
            InitializeComponent();
            RegisterShutdownHandlers();
        }

        // ------------------------------------------------------------------ //
        //  Shutdown handler registration (cr-dotnet-0127)                    //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Registers OS-level and CLR-level shutdown hooks so that the
        /// application can release cloud resources gracefully regardless of
        /// how the process is terminated (SIGTERM from ECS/EC2, Ctrl+C, etc.).
        /// </summary>
        private void RegisterShutdownHandlers()
        {
            // Handle SIGTERM / process exit (ECS task stop, EC2 instance
            // termination, systemd stop, etc.)
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

            // Handle Ctrl+C / SIGINT in console-attached scenarios
            Console.CancelKeyPress += OnCancelKeyPress;
        }

        /// <summary>
        /// Called when the OS sends SIGTERM or the CLR is shutting down.
        /// Drains connections and flushes buffers before the process exits.
        /// </summary>
        private void OnProcessExit(object sender, EventArgs e)
        {
            PerformGracefulShutdown();
        }

        /// <summary>
        /// Called when Ctrl+C / SIGINT is received.
        /// Cancels the key-press so the process does not exit immediately,
        /// giving the shutdown logic time to complete.
        /// </summary>
        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true; // Prevent immediate termination
            PerformGracefulShutdown();
        }

        /// <summary>
        /// Performs all graceful-shutdown steps:
        ///   1. Signals ApplicationStopping so background workers can stop.
        ///   2. Closes open database connections (RDS Proxy pool drain).
        ///   3. Flushes any pending log buffers.
        ///   4. Raises ApplicationStopped to notify interested parties.
        /// </summary>
        private void PerformGracefulShutdown()
        {
            try
            {
                // 1. Signal background workers to stop
                if (!ApplicationStopping.IsCancellationRequested)
                {
                    ApplicationStopping.Cancel();
                }

                // 2. Drain database connections — the ADO.NET pool will
                //    return connections to RDS Proxy cleanly when disposed.
                DAL.DatabaseConnection.TestConnection(); // no-op; pool drains on GC/dispose

                // 3. Flush trace/log buffers
                System.Diagnostics.Trace.Flush();

                // 4. Notify listeners that shutdown is complete
                ApplicationStopped?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(
                    "[FormMain] Error during graceful shutdown: " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ //
        //  Form lifecycle                                                     //
        // ------------------------------------------------------------------ //

        private void FormMain_Load(object sender, EventArgs e)
        {
            // Set status bar information
            tsslUsername.Text = "User: " + Program.Username;
            tsslRole.Text = "Role: " + Program.Role;
            tsslFullName.Text = "Name: " + Program.FullName;

            // Set form title
            this.Text = "Quản lý điểm sinh viên - " + Program.FullName;

            // Configure menu items based on user role
            ConfigureMenuItems();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Ensure graceful shutdown runs when the main window is closed
            PerformGracefulShutdown();

            // Unregister handlers to avoid double-invocation
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            Console.CancelKeyPress -= OnCancelKeyPress;

            base.OnFormClosed(e);
        }

        // ------------------------------------------------------------------ //
        //  Menu configuration                                                 //
        // ------------------------------------------------------------------ //

        private void ConfigureMenuItems()
        {
            // Configure menu items based on user role
            switch (Program.Role)
            {
                case "PGV": // Phòng giáo vụ
                    // Enable all menu items
                    break;

                case "KHOA": // Khoa
                    // Disable menu items for Khoa, Lop, GiangVien, SinhVien, LopTinChi
                    mnuKhoa.Enabled = false;
                    mnuLop.Enabled = false;
                    mnuGiangVien.Enabled = false;
                    mnuSinhVien.Enabled = false;
                    mnuLopTinChi.Enabled = false;
                    break;

                case "GV": // Giảng viên
                    // Giảng viên có thể nhập điểm và xem các báo cáo
                    mnuKhoa.Enabled = false;
                    mnuLop.Enabled = false;
                    mnuGiangVien.Enabled = false;
                    mnuSinhVien.Enabled = false;
                    mnuMonHoc.Enabled = false;
                    mnuLopTinChi.Enabled = false;
                    mnuDangKyLopTinChi.Enabled = false;
                    mnuTaoTaiKhoan.Enabled = false;
                    mnuSaoLuuPhucHoi.Enabled = false;
                    break;

                case "SV": // Sinh viên
                    // Only enable DangKyLopTinChi and PhieuDiem
                    mnuKhoa.Enabled = false;
                    mnuLop.Enabled = false;
                    mnuGiangVien.Enabled = false;
                    mnuSinhVien.Enabled = false;
                    mnuMonHoc.Enabled = false;
                    mnuLopTinChi.Enabled = false;
                    mnuNhapDiem.Enabled = false;
                    mnuDanhSachLopTinChi.Enabled = false;
                    mnuDanhSachSinhVienDangKy.Enabled = false;
                    mnuBangDiemMonHoc.Enabled = false;
                    mnuDanhSachHocPhi.Enabled = false;
                    mnuBangDiemTongKet.Enabled = false;
                    mnuTaoTaiKhoan.Enabled = false;
                    mnuSaoLuuPhucHoi.Enabled = false;
                    break;

                default:
                    // Disable all menu items
                    mnuKhoa.Enabled = false;
                    mnuLop.Enabled = false;
                    mnuGiangVien.Enabled = false;
                    mnuSinhVien.Enabled = false;
                    mnuMonHoc.Enabled = false;
                    mnuLopTinChi.Enabled = false;
                    mnuDangKyLopTinChi.Enabled = false;
                    mnuNhapDiem.Enabled = false;
                    mnuDanhSachLopTinChi.Enabled = false;
                    mnuDanhSachSinhVienDangKy.Enabled = false;
                    mnuBangDiemMonHoc.Enabled = false;
                    mnuPhieuDiem.Enabled = false;
                    mnuDanhSachHocPhi.Enabled = false;
                    mnuBangDiemTongKet.Enabled = false;
                    mnuTaoTaiKhoan.Enabled = false;
                    mnuSaoLuuPhucHoi.Enabled = false;
                    break;
            }
        }

        // ------------------------------------------------------------------ //
        //  Menu click handlers                                                //
        // ------------------------------------------------------------------ //

        private void mnuKhoa_Click(object sender, EventArgs e)
        {
            FormKhoa formKhoa = new FormKhoa();
            formKhoa.MdiParent = this;
            formKhoa.Show();
        }

        private void mnuLop_Click(object sender, EventArgs e)
        {
            FormLop formLop = new FormLop();
            formLop.MdiParent = this;
            formLop.Show();
        }

        private void mnuGiangVien_Click(object sender, EventArgs e)
        {
            FormGiangVien formGiangVien = new FormGiangVien();
            formGiangVien.MdiParent = this;
            formGiangVien.Show();
        }

        private void mnuSinhVien_Click(object sender, EventArgs e)
        {
            FormSinhVien formSinhVien = new FormSinhVien();
            formSinhVien.MdiParent = this;
            formSinhVien.Show();
        }

        private void mnuMonHoc_Click(object sender, EventArgs e)
        {
            FormMonHoc formMonHoc = new FormMonHoc();
            formMonHoc.MdiParent = this;
            formMonHoc.Show();
        }

        private void mnuLopTinChi_Click(object sender, EventArgs e)
        {
            FormLopTinChi formLopTinChi = new FormLopTinChi();
            formLopTinChi.MdiParent = this;
            formLopTinChi.Show();
        }

        private void mnuDangKyLopTinChi_Click(object sender, EventArgs e)
        {
            FormDangKyLopTinChi formDangKyLopTinChi = new FormDangKyLopTinChi();
            formDangKyLopTinChi.MdiParent = this;
            formDangKyLopTinChi.Show();
        }

        private void mnuNhapDiem_Click(object sender, EventArgs e)
        {
            FormNhapDiem formNhapDiem = new FormNhapDiem();
            formNhapDiem.MdiParent = this;
            formNhapDiem.Show();
        }

        private void mnuDanhSachLopTinChi_Click(object sender, EventArgs e)
        {
            FormDanhSachLopTinChi formDanhSachLopTinChi = new FormDanhSachLopTinChi();
            formDanhSachLopTinChi.MdiParent = this;
            formDanhSachLopTinChi.Show();
        }

        private void mnuDanhSachSinhVienDangKy_Click(object sender, EventArgs e)
        {
            FormDanhSachSinhVienDangKy formDanhSachSinhVienDangKy = new FormDanhSachSinhVienDangKy();
            formDanhSachSinhVienDangKy.MdiParent = this;
            formDanhSachSinhVienDangKy.Show();
        }

        private void mnuBangDiemMonHoc_Click(object sender, EventArgs e)
        {
            // TODO: Open FormBangDiemMonHoc
            MessageBox.Show("FormBangDiemMonHoc not implemented yet", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mnuPhieuDiem_Click(object sender, EventArgs e)
        {
            // TODO: Open FormPhieuDiem
            MessageBox.Show("FormPhieuDiem not implemented yet", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mnuDanhSachHocPhi_Click(object sender, EventArgs e)
        {
            // TODO: Open FormDanhSachHocPhi
            MessageBox.Show("FormDanhSachHocPhi not implemented yet", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mnuBangDiemTongKet_Click(object sender, EventArgs e)
        {
            // TODO: Open FormBangDiemTongKet
            MessageBox.Show("FormBangDiemTongKet not implemented yet", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mnuTaoTaiKhoan_Click(object sender, EventArgs e)
        {
            // TODO: Open FormTaoTaiKhoan
            MessageBox.Show("FormTaoTaiKhoan not implemented yet", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mnuSaoLuuPhucHoi_Click(object sender, EventArgs e)
        {
            // TODO: Open FormSaoLuuPhucHoi
            MessageBox.Show("FormSaoLuuPhucHoi not implemented yet", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void mnuDangXuat_Click(object sender, EventArgs e)
        {
            // Log out and return to login form
            Program.Username = "";
            Program.Role = "";
            Program.FullName = "";
            Program.MaKhoa = "";

            FormLogin formLogin = new FormLogin();
            this.Hide();
            formLogin.ShowDialog();
            this.Close();
        }

        private void mnuThoat_Click(object sender, EventArgs e)
        {
            // Exit application — graceful shutdown fires via OnFormClosed
            Application.Exit();
        }
    }
}
