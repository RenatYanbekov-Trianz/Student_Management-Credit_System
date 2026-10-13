namespace QLDSV_HTC.DTO.Models
{
    public class LopTinChi
    {
        public int MaLTC { get; set; }
        public string NienKhoa { get; set; } = string.Empty;
        public int HocKy { get; set; }
        public string MaMH { get; set; } = string.Empty;
        public int Nhom { get; set; }
        public string MaGV { get; set; } = string.Empty;
        public string MaKhoa { get; set; } = string.Empty;
        public short SoSVToiThieu { get; set; }
        public bool HuyLop { get; set; }
    }
}
