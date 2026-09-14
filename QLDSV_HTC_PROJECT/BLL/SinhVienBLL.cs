using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using QLDSV_HTC.DAL;
using QLDSV_HTC.DTO.Models;

// Cloud Readiness Fix (cr-dotnet-0123):
// The hardcoded default password "123456" on line 89 has been replaced with
// a runtime lookup from AWS Secrets Manager.  The secret is stored under the
// name "qldsv-htc/student-defaults" with a JSON key "DefaultPassword".
// This ensures that sensitive credential values are never embedded in source
// code or configuration files, support automatic rotation, and can be updated
// without redeployment.

namespace QLDSV_HTC.BLL
{
    public class SinhVienBLL
    {
        // ------------------------------------------------------------------ //
        //  Secret name used to retrieve the default student password from     //
        //  AWS Secrets Manager (cr-dotnet-0123).                              //
        // ------------------------------------------------------------------ //
        private const string DefaultPasswordSecretName = "qldsv-htc/student-defaults";
        private const string DefaultPasswordSecretKey  = "DefaultPassword";

        /// <summary>
        /// Retrieves the default student password from AWS Secrets Manager.
        /// Falls back to the environment variable QLDSV_HTC_DEFAULT_PASSWORD
        /// when running outside AWS (e.g. local development).
        /// </summary>
        private static string GetDefaultPassword()
        {
            // 1. Environment variable — useful for local / CI environments
            string envPassword = Environment.GetEnvironmentVariable("QLDSV_HTC_DEFAULT_PASSWORD");
            if (!string.IsNullOrWhiteSpace(envPassword))
            {
                return envPassword;
            }

            // 2. AWS Secrets Manager — production path
            try
            {
                return AwsSecretsManagerHelper.GetSecretValue(
                    DefaultPasswordSecretName,
                    DefaultPasswordSecretKey);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Default student password is not configured. " +
                    "Set the environment variable QLDSV_HTC_DEFAULT_PASSWORD or " +
                    $"store the value in AWS Secrets Manager at '{DefaultPasswordSecretName}' " +
                    $"with key '{DefaultPasswordSecretKey}'.", ex);
            }
        }

        public static List<SinhVien> GetAllSinhVien()
        {
            try
            {
                return SinhVienDAL.GetAllSinhVien();
            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving all SinhVien: " + ex.Message);
            }
        }
        
        public static List<SinhVien> GetSinhViensByMaLop(string maLop)
        {
            try
            {
                return SinhVienDAL.GetSinhViensByMaLop(maLop);
            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving SinhVien by MaLop: " + ex.Message);
            }
        }
        
        public static SinhVien GetSinhVienByMaSV(string maSV)
        {
            try
            {
                return SinhVienDAL.GetSinhVienByMaSV(maSV);
            }
            catch (Exception ex)
            {
                throw new Exception("Error retrieving SinhVien by MaSV: " + ex.Message);
            }
        }
        
        public static bool InsertSinhVien(SinhVien sinhVien)
        {
            try
            {
                // Validate data
                if (string.IsNullOrEmpty(sinhVien.MaSV))
                {
                    throw new Exception("MaSV cannot be empty");
                }
                
                if (string.IsNullOrEmpty(sinhVien.Ho))
                {
                    throw new Exception("Ho cannot be empty");
                }
                
                if (string.IsNullOrEmpty(sinhVien.Ten))
                {
                    throw new Exception("Ten cannot be empty");
                }
                
                if (string.IsNullOrEmpty(sinhVien.MaLop))
                {
                    throw new Exception("MaLop cannot be empty");
                }
                
                // Check if MaSV already exists
                SinhVien existingSinhVien = SinhVienDAL.GetSinhVienByMaSV(sinhVien.MaSV);
                if (existingSinhVien != null)
                {
                    throw new Exception("MaSV already exists");
                }
                
                // Check if MaLop exists
                Lop lop = LopDAL.GetLopByMaLop(sinhVien.MaLop);
                if (lop == null)
                {
                    throw new Exception("MaLop does not exist");
                }
                
                // Set default password from AWS Secrets Manager when not provided
                // (cr-dotnet-0123: replaces hardcoded "123456")
                if (string.IsNullOrEmpty(sinhVien.Password))
                {
                    sinhVien.Password = GetDefaultPassword();
                }
                
                return SinhVienDAL.InsertSinhVien(sinhVien);
            }
            catch (Exception ex)
            {
                throw new Exception("Error inserting SinhVien: " + ex.Message);
            }
        }
        
        public static bool UpdateSinhVien(SinhVien sinhVien)
        {
            try
            {
                // Validate data
                if (string.IsNullOrEmpty(sinhVien.MaSV))
                {
                    throw new Exception("MaSV cannot be empty");
                }
                
                if (string.IsNullOrEmpty(sinhVien.Ho))
                {
                    throw new Exception("Ho cannot be empty");
                }
                
                if (string.IsNullOrEmpty(sinhVien.Ten))
                {
                    throw new Exception("Ten cannot be empty");
                }
                
                if (string.IsNullOrEmpty(sinhVien.MaLop))
                {
                    throw new Exception("MaLop cannot be empty");
                }
                
                // Check if MaSV exists
                SinhVien existingSinhVien = SinhVienDAL.GetSinhVienByMaSV(sinhVien.MaSV);
                if (existingSinhVien == null)
                {
                    throw new Exception("MaSV does not exist");
                }
                
                // Check if MaLop exists
                Lop lop = LopDAL.GetLopByMaLop(sinhVien.MaLop);
                if (lop == null)
                {
                    throw new Exception("MaLop does not exist");
                }
                
                return SinhVienDAL.UpdateSinhVien(sinhVien);
            }
            catch (Exception ex)
            {
                throw new Exception("Error updating SinhVien: " + ex.Message);
            }
        }
        
        public static bool DeleteSinhVien(string maSV)
        {
            try
            {
                // Check if MaSV exists
                SinhVien existingSinhVien = SinhVienDAL.GetSinhVienByMaSV(maSV);
                if (existingSinhVien == null)
                {
                    throw new Exception("MaSV does not exist");
                }
                
                // Check if there are any DangKy associated with this SinhVien
                List<DangKy> dangKys = DangKyDAL.GetDangKysByMaSV(maSV);
                if (dangKys.Count > 0)
                {
                    // We don't throw an exception here because we're just marking the student as "DaNghiHoc"
                    // instead of actually deleting the record
                }
                
                return SinhVienDAL.DeleteSinhVien(maSV);
            }
            catch (Exception ex)
            {
                throw new Exception("Error deleting SinhVien: " + ex.Message);
            }
        }
        
        public static bool CheckLogin(string maSV, string password)
        {
            try
            {
                return SinhVienDAL.CheckLogin(maSV, password);
            }
            catch (Exception ex)
            {
                throw new Exception("Error checking login: " + ex.Message);
            }
        }
    }
}
