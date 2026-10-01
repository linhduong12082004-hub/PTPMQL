using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class NhanVien
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã nhân viên")]
        [StringLength(10, ErrorMessage = "Mã nhân viên không quá 10 ký tự")]
        [Display(Name = "Mã nhân viên")]
        public string MaNhanVien { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên không quá 100 ký tự")]
        [Display(Name = "Họ tên")]
        public string HoTen { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập chức vụ")]
        [StringLength(100, ErrorMessage = "Chức vụ không quá 100 ký tự")]
        [Display(Name = "Chức vụ")]
        public string ChucVu { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập phòng ban")]
        [StringLength(100, ErrorMessage = "Phòng ban không quá 100 ký tự")]
        [Display(Name = "Phòng ban")]
        public string PhongBan { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập lương")]
        [Range(0, 1000000000, ErrorMessage = "Lương không hợp lệ")]
        [Display(Name = "Lương")]
        public decimal Luong { get; set; }
    }
}