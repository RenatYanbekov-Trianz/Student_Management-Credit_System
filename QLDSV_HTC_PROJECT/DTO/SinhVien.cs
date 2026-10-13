using System;

namespace QLDSV_HTC.DTO.Models
{
    public class SinhVien
    {
        public string MaSV { get; set; } = string.Empty;
        public string Ho { get; set; } = string.Empty;
        public string Ten { get; set; } = string.Empty;
        public string MaLop { get; set; } = string.Empty;
        public bool Phai { get; set; }
        public DateTime NgaySinh { get; set; }
        public string DiaChi { get; set; } = string.Empty;
        public bool DaNghiHoc { get; set; }
        public string Password { get; set; } = string.Empty;
    }
}
