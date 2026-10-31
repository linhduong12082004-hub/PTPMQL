
using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Models
{
    public class Department
    {
        [Key]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã khoa")]
        [StringLength(20)]
        [Display(Name = "Mã khoa")]
        public string DepartmentCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tên khoa không được để trống")]
        [StringLength(100, MinimumLength = 2,
            ErrorMessage = "Tên khoa phải từ 2 đến 100 ký tự")]
        [Display(Name = "Tên khoa")]
        public string Name { get; set; } = string.Empty;

        // Một khoa có nhiều sinh viên
        public ICollection<Student> Students { get; set; }
            = new List<Student>();
    }
}
