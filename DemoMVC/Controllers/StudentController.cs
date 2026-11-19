using DemoMVC.Data;
using DemoMVC.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace DemoMVC.Controllers
{
    public class StudentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private static readonly SemaphoreSlim StudentCodeGenerationLock = new(1, 1);
        private static readonly HashSet<string> AllowedEducationSystemCodes = new(StringComparer.Ordinal) { "21" };
        private static readonly HashSet<string> AllowedMajorCodes = new(StringComparer.Ordinal) { "05" };

        public StudentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(
            int page = 1,
            int pageSize = PaginatedList<Student>.DefaultPageSize)
        {
            var students = await PaginatedList<Student>.CreateAsync(
                _context.Students
                    .Include(student => student.Department)
                    .AsNoTracking()
                    .OrderBy(student => student.Id),
                page,
                pageSize);

            return View(students);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students
                .Include(s => s.Department)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                return NotFound();
            }

            return IsAjaxRequest()
                ? PartialView("_DetailsModal", student)
                : View(student);
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            await LoadStudentSelectionDataAsync();
            return IsAjaxRequest()
                ? PartialView("_CreateModal", new Student())
                : View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AdmissionYear,EducationSystemCode,MajorCode,Name,Age,Email,DepartmentId")] Student student)
        {
            if (!student.DepartmentId.HasValue)
            {
                ModelState.AddModelError(nameof(Student.DepartmentId), "Vui lòng chọn khoa.");
            }
            else if (!await _context.Departments.AnyAsync(d => d.DepartmentId == student.DepartmentId.Value))
            {
                ModelState.AddModelError(nameof(Student.DepartmentId), "Khoa không tồn tại.");
            }

            if (!AllowedEducationSystemCodes.Contains(student.EducationSystemCode))
            {
                ModelState.AddModelError(nameof(Student.EducationSystemCode), "Mã hệ đào tạo không được hỗ trợ.");
            }

            if (!AllowedMajorCodes.Contains(student.MajorCode))
            {
                ModelState.AddModelError(nameof(Student.MajorCode), "Mã ngành không được hỗ trợ.");
            }

            if (ModelState.IsValid)
            {
                await StudentCodeGenerationLock.WaitAsync();
                try
                {
                    var generationResult = await GenerateStudentCodeAsync(student.AdmissionYear, student.EducationSystemCode, student.MajorCode);
                    if (!generationResult.Success)
                    {
                        ModelState.AddModelError(string.Empty, generationResult.Message);
                    }
                    else
                    {
                        student.StudentCode = generationResult.Code;
                        try
                        {
                            _context.Students.Add(student);
                            await _context.SaveChangesAsync();
                            return SuccessResult("Thêm sinh viên thành công!");
                        }
                        catch (DbUpdateException)
                        {
                            ModelState.AddModelError(string.Empty, "Mã sinh viên đã được tạo bởi người khác. Vui lòng thử lại.");
                        }
                    }
                }
                finally
                {
                    StudentCodeGenerationLock.Release();
                }
            }

            await LoadStudentSelectionDataAsync(student.DepartmentId, student.EducationSystemCode, student.MajorCode, student.AdmissionYear);
            return InvalidFormResult("_CreateModal", student);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound();
            }

            await LoadStudentSelectionDataAsync(student.DepartmentId, student.EducationSystemCode, student.MajorCode, student.AdmissionYear);
            return IsAjaxRequest()
                ? PartialView("_EditModal", student)
                : View(student);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,AdmissionYear,EducationSystemCode,MajorCode,Name,Age,Email,DepartmentId")] Student student)
        {
            if (id != student.Id)
            {
                return NotFound();
            }

            var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.Id == id);
            if (existingStudent == null)
            {
                return NotFound();
            }

            if (!student.DepartmentId.HasValue)
            {
                ModelState.AddModelError(nameof(Student.DepartmentId), "Vui lòng chọn khoa.");
            }
            else if (!await _context.Departments.AnyAsync(d => d.DepartmentId == student.DepartmentId.Value))
            {
                ModelState.AddModelError(nameof(Student.DepartmentId), "Khoa không tồn tại.");
            }

            if (!AllowedEducationSystemCodes.Contains(student.EducationSystemCode))
            {
                ModelState.AddModelError(nameof(Student.EducationSystemCode), "Mã hệ đào tạo không được hỗ trợ.");
            }

            if (!AllowedMajorCodes.Contains(student.MajorCode))
            {
                ModelState.AddModelError(nameof(Student.MajorCode), "Mã ngành không được hỗ trợ.");
            }

            if (ModelState.IsValid)
            {
                var newGroupPrefix = GetGroupPrefix(student.AdmissionYear, student.EducationSystemCode, student.MajorCode);
                var oldGroupPrefix = GetGroupPrefix(existingStudent.AdmissionYear, existingStudent.EducationSystemCode, existingStudent.MajorCode);

                if (string.Equals(newGroupPrefix, oldGroupPrefix, StringComparison.Ordinal))
                {
                    student.StudentCode = existingStudent.StudentCode;
                }
                else
                {
                    await StudentCodeGenerationLock.WaitAsync();
                    try
                    {
                        var generationResult = await GenerateStudentCodeAsync(student.AdmissionYear, student.EducationSystemCode, student.MajorCode);
                        if (!generationResult.Success)
                        {
                            ModelState.AddModelError(string.Empty, generationResult.Message);
                        }
                        else
                        {
                            student.StudentCode = generationResult.Code;
                        }
                    }
                    finally
                    {
                        StudentCodeGenerationLock.Release();
                    }
                }

                if (ModelState.IsValid)
                {
                    _context.Entry(existingStudent).CurrentValues.SetValues(student);
                    await _context.SaveChangesAsync();
                    return SuccessResult("Cập nhật sinh viên thành công!");
                }
            }

            await LoadStudentSelectionDataAsync(student.DepartmentId, student.EducationSystemCode, student.MajorCode, student.AdmissionYear);
            return InvalidFormResult("_EditModal", student);
        }

        [HttpGet]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var student = await _context.Students
                .Include(s => s.Department)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (student == null)
            {
                return NotFound();
            }

            return IsAjaxRequest()
                ? PartialView("_DeleteModal", student)
                : View(student);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
            {
                return NotFound();
            }

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();
            return SuccessResult("Xóa sinh viên thành công!");
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

        private IActionResult InvalidFormResult(string partialName, Student student)
        {
            if (IsAjaxRequest())
            {
                Response.StatusCode = StatusCodes.Status400BadRequest;
                return PartialView(partialName, student);
            }

            return View(partialName == "_CreateModal" ? "Create" : "Edit", student);
        }

        private async Task LoadStudentSelectionDataAsync(
            int? selectedDepartmentId = null,
            string? selectedEducationSystemCode = null,
            string? selectedMajorCode = null,
            int? selectedAdmissionYear = null)
        {
            await LoadDepartmentsAsync(selectedDepartmentId);

            ViewBag.EducationSystems = new SelectList(new[] { "21" }, selectedEducationSystemCode ?? "21");
            ViewBag.MajorCodes = new SelectList(new[] { "05" }, selectedMajorCode ?? "05");

            var currentYear = DateTime.UtcNow.Year;
            ViewBag.AdmissionYears = new SelectList(
                Enumerable.Range(currentYear - 5, 6).Select(year => new { Value = year, Text = year.ToString() }),
                "Value",
                "Text",
                selectedAdmissionYear ?? currentYear);
        }

        private async Task LoadDepartmentsAsync(int? selectedDepartmentId = null)
        {
            var departments = await _context.Departments
                .AsNoTracking()
                .OrderBy(department => department.Name)
                .ToListAsync();

            ViewBag.Departments = new SelectList(
                departments,
                nameof(Department.DepartmentId),
                nameof(Department.Name),
                selectedDepartmentId);
        }

        private async Task<(bool Success, string Code, string Message)> GenerateStudentCodeAsync(int admissionYear, string educationSystemCode, string majorCode)
        {
            if (!AllowedEducationSystemCodes.Contains(educationSystemCode))
            {
                return (false, string.Empty, "Mã hệ đào tạo không hợp lệ.");
            }

            if (!AllowedMajorCodes.Contains(majorCode))
            {
                return (false, string.Empty, "Mã ngành không hợp lệ.");
            }

            var normalizedYear = admissionYear % 100;
            var prefix = $"{normalizedYear:00}{educationSystemCode}{majorCode}";

            var latestCode = await _context.Students
                .AsNoTracking()
                .Where(student => student.StudentCode.StartsWith(prefix))
                .OrderByDescending(student => student.StudentCode)
                .Select(student => student.StudentCode)
                .FirstOrDefaultAsync();

            var nextSequence = 1;
            if (!string.IsNullOrWhiteSpace(latestCode) && latestCode.Length >= 10)
            {
                var sequenceText = latestCode.Substring(6, 4);
                if (int.TryParse(sequenceText, out var currentSequence))
                {
                    nextSequence = currentSequence + 1;
                }
            }

            if (nextSequence > 9999)
            {
                return (false, string.Empty, "Đã đạt giới hạn số thứ tự 9999 cho nhóm mã này. Vui lòng chọn nhóm khác.");
            }

            return (true, $"{prefix}{nextSequence:0000}", string.Empty);
        }

        private static string GetGroupPrefix(int admissionYear, string educationSystemCode, string majorCode)
        {
            return $"{admissionYear % 100:00}{(educationSystemCode ?? string.Empty).Trim()}{(majorCode ?? string.Empty).Trim()}";
        }
    }
}