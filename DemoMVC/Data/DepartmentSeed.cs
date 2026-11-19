
using DemoMVC.Models;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Data
{
    public static class DepartmentSeed
    {
        public static async Task SeedAsync(
            ApplicationDbContext context)
        {
            await Task.CompletedTask;
        }
    }
}
