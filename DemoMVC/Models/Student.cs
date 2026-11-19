
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoMVC.Models
{
    public class Student
    {
        public int Id { get; set; }

        // Mã sinh viên, ví dụ: 2821050199
        [Display(Name = "Mã sinh viên")]
        public string StudentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn năm nhập học")]
        [Range(2000, 2100, ErrorMessage = "Năm nhập học không hợp lệ")]
        [Display(Name = "Năm nhập học")]
        public int AdmissionYear { get; set; } = DateTime.Today.Year;

        [Required(ErrorMessage = "Vui lòng chọn mã hệ đào tạo")]
        [RegularExpression(@"^\d{2}$", ErrorMessage = "Mã hệ đào tạo phải gồm 2 chữ số")]
        [StringLength(2, MinimumLength = 2)]
        [Display(Name = "Mã hệ đào tạo")]
        public string EducationSystemCode { get; set; } = "21";

        [Required(ErrorMessage = "Vui lòng chọn mã ngành")]
        [RegularExpression(@"^\d{2}$", ErrorMessage = "Mã ngành phải gồm 2 chữ số")]
        [StringLength(2, MinimumLength = 2)]
        [Display(Name = "Mã ngành")]
        public string MajorCode { get; set; } = "05";

        // Họ và tên
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100,
            ErrorMessage = "Họ tên không được quá 100 ký tự")]
        [Display(Name = "Họ và tên")]
        public string Name { get; set; } = string.Empty;

        // Tuổi
        [Required(ErrorMessage = "Vui lòng nhập tuổi")]
        [Range(18, 100, ErrorMessage = "Tuổi phải từ 18 đến 100")]
        [Display(Name = "Tuổi")]
        public int? Age { get; set; }

        // Email
        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        // Khóa ngoại liên kết với khoa
        [Display(Name = "Khoa")]
        public int? DepartmentId { get; set; }

        // Đối tượng khoa của sinh viên
        [ForeignKey(nameof(DepartmentId))]
        public Department? Department { get; set; }
    }
}
