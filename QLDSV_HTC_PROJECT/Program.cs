using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using QLDSV_HTC.GUI;
using QLDSV_HTC.Health;

namespace QLDSV_HTC
{
    static class Program
    {
        // User information
        public static string Username = "";
        public static string Role = "";
        public static string FullName = "";
        public static string MaKhoa = "";

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Start health check HTTP server for Kubernetes liveness/readiness probes.
            // Listens on GET /health (port configured via HEALTH_CHECK_PORT env var, default 8080).
            HealthCheckServer.Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Stop health check server when application exits
            Application.ApplicationExit += (sender, e) => HealthCheckServer.Stop();

            Application.Run(new FormLogin());
        }
    }
}
