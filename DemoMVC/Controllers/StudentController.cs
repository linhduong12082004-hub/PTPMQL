using Microsoft.AspNetCore.Mvc;

namespace DemoMVC.Controllers
{
    public class StudentController : Controller
    {
        // GET: /Student
        public IActionResult Index()
        {
            return View();
        }

        // POST: /Student/Index
        [HttpPost]
        public IActionResult Index(string fullName, string school, string address)
        {
            // Gửi dữ liệu sang View bằng ViewBag
            ViewBag.FullName = fullName;
            ViewBag.School = school;
            ViewBag.Address = address;

            // Gửi riêng họ tên bằng ViewData
            ViewData["HoTen"] = fullName;

            return View();
        }
    }
}