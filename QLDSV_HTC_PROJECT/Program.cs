using System;
using System.Windows.Forms;
using QLDSV_HTC.GUI;

namespace QLDSV_HTC
{
    static class Program
    {
        // User information
        public static string Username = string.Empty;
        public static string Role = string.Empty;
        public static string FullName = string.Empty;
        public static string MaKhoa = string.Empty;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // .NET 8 Windows Forms startup configuration
            ApplicationConfiguration.Initialize();
            Application.Run(new FormLogin());
        }
    }
}
