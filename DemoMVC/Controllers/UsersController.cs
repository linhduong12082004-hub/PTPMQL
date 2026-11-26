using System.Text;
using DemoMVC.Data;
using DemoMVC.Models;
using DemoMVC.Options;
using DemoMVC.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DemoMVC.Controllers;

[Authorize(Roles = "Administrator")]
public class UsersController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IEmailSender emailSender,
    IOptions<EmailOptions> emailOptions,
    ILogger<UsersController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await userManager.Users
            .AsNoTracking()
            .OrderBy(user => user.UserName)
            .ToListAsync();

        var viewModels = new List<UserListItemViewModel>(users.Count);
        foreach (var user in users)
        {
            viewModels.Add(new UserListItemViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                FullName = user.FullName,
                EmailConfirmed = user.EmailConfirmed,
                IsActive = user.IsActive,
                Roles = string.Join(", ", await userManager.GetRolesAsync(user)),
                HasPassword = await userManager.HasPasswordAsync(user)
            });
        }

        return View(viewModels);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new UserCreateViewModel());
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserCreateViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName.Trim(),
            Email = model.Email.Trim(),
            FullName = model.FullName.Trim(),
            PhoneNumber = model.PhoneNumber?.Trim(),
            EmailConfirmed = false,
            IsActive = false
        };

        await using (var transaction = await context.Database.BeginTransactionAsync())
        {
            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(createResult);
                return View(model);
            }

            var addRoleResult = await userManager.AddToRoleAsync(user, "User");
            if (!addRoleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(addRoleResult);
                return View(model);
            }

            await transaction.CommitAsync();
        }

        try
        {
            await SendInvitationAsync(user);
            TempData["Success"] = "Đã tạo tài khoản và gửi lời mời thiết lập mật khẩu.";
        }
        catch (EmailDeliveryException)
        {
            logger.LogError("Account invitation delivery failed; account details were omitted.");
            TempData["Error"] = "Tài khoản đã tạo nhưng email mời chưa gửi được. Hãy kiểm tra cấu hình SMTP và gửi lại lời mời.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Manage(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        return View(await CreateManageViewModelAsync(user));
    }

    [HttpPost]
    public async Task<IActionResult> Manage(UserRolesViewModel model)
    {
        var user = await userManager.FindByIdAsync(model.Id);
        if (user is null)
        {
            return NotFound();
        }

        var requestedRoles = (model.SelectedRoles ?? [])
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var allowedRoles = await roleManager.Roles
            .Select(role => role.Name!)
            .ToListAsync();

        if (requestedRoles.Any(role => !allowedRoles.Contains(role, StringComparer.Ordinal)))
        {
            ModelState.AddModelError(nameof(model.SelectedRoles), "Có vai trò không hợp lệ.");
        }

        if (requestedRoles.Count == 0)
        {
            ModelState.AddModelError(nameof(model.SelectedRoles), "Tài khoản phải có ít nhất một vai trò.");
        }

        var currentUserId = userManager.GetUserId(User);
        var isCurrentUser = string.Equals(user.Id, currentUserId, StringComparison.Ordinal);
        var isCurrentlyAdministrator = await userManager.IsInRoleAsync(user, "Administrator");
        var willRemainAdministrator = requestedRoles.Contains("Administrator", StringComparer.Ordinal);

        if (isCurrentUser && (!model.IsActive || !willRemainAdministrator))
        {
            ModelState.AddModelError(string.Empty, "Không thể vô hiệu hóa hoặc gỡ quyền Administrator của tài khoản đang đăng nhập.");
        }

        if (isCurrentlyAdministrator && !willRemainAdministrator)
        {
            var administratorCount = await userManager.GetUsersInRoleAsync("Administrator");
            if (administratorCount.Count <= 1)
            {
                ModelState.AddModelError(string.Empty, "Không thể gỡ quyền Administrator cuối cùng trong hệ thống.");
            }
        }

        if (isCurrentlyAdministrator && !model.IsActive)
        {
            var activeAdministratorCount = (await userManager.GetUsersInRoleAsync("Administrator"))
                .Count(administrator => administrator.IsActive);
            if (activeAdministratorCount <= 1)
            {
                ModelState.AddModelError(string.Empty, "Không thể vô hiệu hóa Administrator đang hoạt động cuối cùng.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.UserName = user.UserName ?? string.Empty;
            model.Email = user.Email ?? string.Empty;
            model.AvailableRoles = allowedRoles;
            return View(model);
        }

        var currentRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = currentRoles.Except(requestedRoles, StringComparer.Ordinal).ToList();
        var rolesToAdd = requestedRoles.Except(currentRoles, StringComparer.Ordinal).ToList();

        await using var transaction = await context.Database.BeginTransactionAsync();
        user.FullName = model.FullName.Trim();
        user.IsActive = model.IsActive;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(updateResult);
            model.AvailableRoles = allowedRoles;
            return View(model);
        }

        if (!model.IsActive || rolesToRemove.Count > 0 || rolesToAdd.Count > 0)
        {
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(stampResult);
                model.AvailableRoles = allowedRoles;
                return View(model);
            }
        }

        if (rolesToRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(removeResult);
                model.AvailableRoles = allowedRoles;
                return View(model);
            }
        }

        if (rolesToAdd.Count > 0)
        {
            var addResult = await userManager.AddToRolesAsync(user, rolesToAdd);
            if (!addResult.Succeeded)
            {
                await transaction.RollbackAsync();
                AddIdentityErrors(addResult);
                model.AvailableRoles = allowedRoles;
                return View(model);
            }
        }

        await transaction.CommitAsync();
        TempData["Success"] = "Thông tin, trạng thái và vai trò tài khoản đã được cập nhật.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ResendInvitation(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (user.IsActive || await userManager.HasPasswordAsync(user))
        {
            TempData["Error"] = "Chỉ có thể gửi lời mời mới cho tài khoản chưa kích hoạt và chưa thiết lập mật khẩu.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await SendInvitationAsync(user);
            TempData["Success"] = "Đã gửi lại lời mời thiết lập tài khoản.";
        }
        catch (EmailDeliveryException)
        {
            logger.LogError("Account invitation delivery failed; account details were omitted.");
            TempData["Error"] = "Không gửi được email mời. Hãy kiểm tra cấu hình SMTP và thử lại.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<UserRolesViewModel> CreateManageViewModelAsync(ApplicationUser user)
    {
        return new UserRolesViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            IsActive = user.IsActive,
            SelectedRoles = (await userManager.GetRolesAsync(user)).ToList(),
            AvailableRoles = await roleManager.Roles
                .OrderBy(role => role.Name)
                .Select(role => role.Name!)
                .ToListAsync()
        };
    }

    private async Task SendInvitationAsync(ApplicationUser user)
    {
        var emailToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var passwordToken = await userManager.GeneratePasswordResetTokenAsync(user);
        var path = Url.Action("AcceptInvitation", "Account", new
        {
            userId = user.Id,
            emailToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(emailToken)),
            passwordToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(passwordToken))
        });

        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
        {
            throw new InvalidOperationException("Could not generate an account invitation URL.");
        }

        var baseUri = new Uri(emailOptions.Value.PublicBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var actionUri = new Uri(baseUri, path.TrimStart('/'));
        await emailSender.SendAccountActionAsync(
            user.Email!,
            "Kích hoạt tài khoản DemoMVC",
            "Bạn được mời tham gia DemoMVC",
            $"Xin chào {user.FullName}. Hãy xác minh email và tạo mật khẩu bằng liên kết có hiệu lực trong một giờ.",
            "Thiết lập tài khoản",
            actionUri);
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }
}
