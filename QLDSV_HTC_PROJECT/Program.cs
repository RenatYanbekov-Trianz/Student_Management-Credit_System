using System;
using System.Windows.Forms;
using QLDSV_HTC.GUI;

namespace QLDSV_HTC
{
    static class Program
    {
        // User session information
        public static string Username = string.Empty;
        public static string Role = string.Empty;
        public static string FullName = string.Empty;
        public static string MaKhoa = string.Empty;

        /// <summary>
        /// The main entry point for the application.
        /// Updated for .NET 8 using ApplicationConfiguration.Initialize().
        /// </summary>
        [STAThread]
        static void Main()
        {
            // .NET 8 WinForms initialization - replaces legacy Application.EnableVisualStyles()
            // and Application.SetCompatibleTextRenderingDefault(false)
            ApplicationConfiguration.Initialize();
            Application.Run(new FormLogin());
        }
    }
}
