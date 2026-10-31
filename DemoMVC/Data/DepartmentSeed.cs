
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public static class DepartmentSeed
    {
        public static async Task SeedAsync(
            ApplicationDbContext context)
        {
            // Nếu đã có dữ liệu khoa thì không thêm lại
            if (await context.Departments.AnyAsync())
                return;

            var departments = new List<Department>
            {
                new Department
                {
                    DepartmentCode = "CNTT",
                    Name = "Khoa Công nghệ thông tin"
                },
                new Department
                {
                    DepartmentCode = "DCKT",
                    Name = "Khoa Kinh tế"
                },
                new Department
                {
                    DepartmentCode = "DCDT",
                    Name = "Khoa Điện - Điện tử"
                },
                new Department
                {
                    DepartmentCode = "DCKS",
                    Name = "Khoa Cơ - Điện"
                },
                new Department
                {
                    DepartmentCode = "DCD",
                    Name = "Khoa Dầu khí"
                },
                new Department
                {
                    DepartmentCode = "DCCT",
                    Name = "Khoa Xây dựng"
                }
            };

            await context.Departments.AddRangeAsync(departments);
            await context.SaveChangesAsync();
        }
    }
}
