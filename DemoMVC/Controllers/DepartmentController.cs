using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers
{
    public class DepartmentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DepartmentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .OrderBy(d => d.Name)
                .ToListAsync();

            return View(departments);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == id);

            if (department == null)
            {
                return NotFound();
            }

            return IsAjaxRequest()
                ? PartialView("_DetailsModal", department)
                : View(department);
        }

        public IActionResult Create()
        {
            return IsAjaxRequest()
                ? PartialView("_CreateModal", new Department())
                : View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("DepartmentCode,Name")] Department department)
        {
            department.DepartmentCode = department.DepartmentCode?.Trim() ?? string.Empty;
            department.Name = department.Name?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(department.DepartmentCode))
            {
                ModelState.AddModelError(nameof(Department.DepartmentCode), "Vui lòng nhập mã khoa");
            }

            if (ModelState.IsValid)
            {
                if (await _context.Departments.AnyAsync(d => d.DepartmentCode == department.DepartmentCode))
                {
                    ModelState.AddModelError(nameof(Department.DepartmentCode), "Mã khoa đã tồn tại");
                    return InvalidFormResult("_CreateModal", department);
                }

                _context.Departments.Add(department);
                await _context.SaveChangesAsync();
                return SuccessResult("Thêm khoa thành công!");
            }

            return InvalidFormResult("_CreateModal", department);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var department = await _context.Departments.FindAsync(id);
            if (department == null)
            {
                return NotFound();
            }

            return IsAjaxRequest()
                ? PartialView("_EditModal", department)
                : View(department);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("DepartmentId,DepartmentCode,Name")] Department department)
        {
            if (id != department.DepartmentId)
            {
                return NotFound();
            }

            department.DepartmentCode = department.DepartmentCode?.Trim() ?? string.Empty;
            department.Name = department.Name?.Trim() ?? string.Empty;

            if (ModelState.IsValid)
            {
                if (await _context.Departments.AnyAsync(d => d.DepartmentId != department.DepartmentId && d.DepartmentCode == department.DepartmentCode))
                {
                    ModelState.AddModelError(nameof(Department.DepartmentCode), "Mã khoa đã tồn tại");
                    return InvalidFormResult("_EditModal", department);
                }

                _context.Update(department);
                await _context.SaveChangesAsync();
                return SuccessResult("Cập nhật khoa thành công!");
            }

            return InvalidFormResult("_EditModal", department);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var department = await _context.Departments
                .FirstOrDefaultAsync(d => d.DepartmentId == id);

            if (department == null)
            {
                return NotFound();
            }

            return IsAjaxRequest()
                ? PartialView("_DeleteModal", department)
                : View(department);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var department = await _context.Departments
                .Include(d => d.Students)
                .FirstOrDefaultAsync(d => d.DepartmentId == id);

            if (department == null)
            {
                return NotFound();
            }

            if (department.Students.Any())
            {
                TempData["Error"] = "Không thể xóa khoa vì hiện còn sinh viên đang tham chiếu đến khoa này.";
                if (IsAjaxRequest())
                {
                    Response.StatusCode = StatusCodes.Status400BadRequest;
                    return Json(new { success = false, message = "Không thể xóa khoa vì hiện còn sinh viên đang tham chiếu đến khoa này." });
                }

                return RedirectToAction(nameof(Index));
            }

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return SuccessResult("Xóa khoa thành công!");
        }

        private bool IsAjaxRequest()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }

        private IActionResult SuccessResult(string message)
        {
            TempData["Success"] = message;
            return IsAjaxRequest()
                ? Json(new { success = true })
                : RedirectToAction(nameof(Index));
        }

        private IActionResult InvalidFormResult(string partialName, Department department)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return PartialView(partialName, department);
            }

            return View(partialName == "_CreateModal" ? "Create" : "Edit", department);
        }
    }
}
