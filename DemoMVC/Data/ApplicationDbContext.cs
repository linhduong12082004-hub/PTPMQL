
using Microsoft.EntityFrameworkCore;
using DemoMVC.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; }
        public DbSet<NhanVien> NhanViens { get; set; }
        public DbSet<Department> Departments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Department>()
                .HasIndex(department => department.DepartmentCode);

            modelBuilder.Entity<Student>()
                .HasIndex(student => student.StudentCode);
        }
    }
}
