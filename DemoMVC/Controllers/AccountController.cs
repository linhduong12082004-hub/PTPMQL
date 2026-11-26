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

public class AccountController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IEmailSender emailSender,
    IEmailRequestLimiter emailRequestLimiter,
    IOptions<EmailOptions> emailOptions,
    ILogger<AccountController> logger) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (signInManager.IsSignedIn(User))
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is null || !user.IsActive || !user.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không hợp lệ.");
            return View(model);
        }

        var result = await signInManager.PasswordSignInAsync(
            user,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return RedirectToLocal(model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Tài khoản tạm thời bị khóa. Vui lòng thử lại sau.");
        }
        else
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không hợp lệ.");
        }

        return View(model);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResendConfirmation()
    {
        return View(new EmailViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ResendConfirmation(EmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (emailRequestLimiter.TryAcquire(model.Email, GetClientAddress()))
        {
            var user = await userManager.FindByEmailAsync(model.Email.Trim());
            if (user is not null && !user.EmailConfirmed && !user.IsActive)
            {
                try
                {
                    await SendConfirmationEmailAsync(user);
                }
                catch (EmailDeliveryException)
                {
                    logger.LogError("Email confirmation delivery failed; account details were omitted.");
                }
            }
        }

        ViewBag.StatusMessage =
            "Nếu tài khoản cần xác minh và email gửi đi khả dụng, hướng dẫn xác minh sẽ được gửi.";
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(string? userId, string? token)
    {
        PreventTokenCaching();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            return InvalidConfirmationLink();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return InvalidConfirmationLink();
        }

        return View(new ConfirmEmailViewModel
        {
            UserId = user.Id,
            Token = token
        });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailViewModel model)
    {
        PreventTokenCaching();
        if (!ModelState.IsValid)
        {
            return InvalidConfirmationLink();
        }

        var user = await userManager.FindByIdAsync(model.UserId);
        var token = DecodeToken(model.Token);
        if (user is null || token is null)
        {
            return InvalidConfirmationLink();
        }

        if (user.EmailConfirmed && !user.IsActive)
        {
            return View("AccountMessage", new AccountMessageViewModel
            {
                Title = "Tài khoản chưa được kích hoạt",
                Message = "Email đã xác minh nhưng tài khoản hiện không hoạt động. Vui lòng liên hệ quản trị viên.",
                Succeeded = false
            });
        }

        if (!user.EmailConfirmed)
        {
            var result = await userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
            {
                return InvalidConfirmationLink();
            }
        }

        if (!user.IsActive)
        {
            user.IsActive = true;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                logger.LogError("Email was confirmed, but activating the account failed.");
                return View("AccountMessage", new AccountMessageViewModel
                {
                    Title = "Email đã xác minh",
                    Message = "Không thể kích hoạt tài khoản lúc này. Vui lòng liên hệ quản trị viên.",
                    Succeeded = false
                });
            }
        }

        return View("AccountMessage", new AccountMessageViewModel
        {
            Title = "Email đã xác minh",
            Message = "Tài khoản đã được kích hoạt. Bạn có thể đăng nhập.",
            Succeeded = true
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new EmailViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ForgotPassword(EmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (emailRequestLimiter.TryAcquire(model.Email, GetClientAddress()))
        {
            var user = await userManager.FindByEmailAsync(model.Email.Trim());
            if (user is not null &&
                user.EmailConfirmed &&
                user.IsActive &&
                await userManager.HasPasswordAsync(user))
            {
                try
                {
                    var token = await userManager.GeneratePasswordResetTokenAsync(user);
                    var link = BuildEmailUri(nameof(ResetPassword), new
                    {
                        email = user.Email,
                        token = EncodeToken(token)
                    });

                    await emailSender.SendAccountActionAsync(
                        user.Email!,
                        "Đặt lại mật khẩu",
                        "Yêu cầu đặt lại mật khẩu",
                        "Nhấn nút bên dưới để đặt mật khẩu mới. Liên kết có hiệu lực trong một giờ.",
                        "Đặt lại mật khẩu",
                        link);
                }
                catch (EmailDeliveryException)
                {
                    logger.LogError("Password reset email delivery failed; account details were omitted.");
                }
            }
        }

        ViewBag.StatusMessage =
            "Nếu email thuộc tài khoản đủ điều kiện và dịch vụ email hoạt động, hướng dẫn đặt lại sẽ được gửi.";
        return View("ForgotPasswordConfirmation");
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> AcceptInvitation(
        string? userId,
        string? emailToken,
        string? passwordToken)
    {
        PreventTokenCaching();
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(emailToken) ||
            string.IsNullOrWhiteSpace(passwordToken))
        {
            return InvalidInvitationLink();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null || user.IsActive || await userManager.HasPasswordAsync(user))
        {
            return InvalidInvitationLink();
        }

        return View(new AcceptInvitationViewModel
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            EmailToken = emailToken,
            PasswordToken = passwordToken
        });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> AcceptInvitation(AcceptInvitationViewModel model)
    {
        PreventTokenCaching();
        if (!ModelState.IsValid)
        {
            var invitationUser = await userManager.FindByIdAsync(model.UserId);
            model.Email = invitationUser?.Email ?? string.Empty;
            return View(model);
        }

        var user = await userManager.FindByIdAsync(model.UserId);
        var emailToken = DecodeToken(model.EmailToken);
        var passwordToken = DecodeToken(model.PasswordToken);
        if (user is null ||
            user.IsActive ||
            emailToken is null ||
            passwordToken is null ||
            await userManager.HasPasswordAsync(user))
        {
            return InvalidInvitationLink();
        }

        var validEmailToken = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.EmailConfirmationTokenProvider,
            UserManager<ApplicationUser>.ConfirmEmailTokenPurpose,
            emailToken);
        var validPasswordToken = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.PasswordResetTokenProvider,
            UserManager<ApplicationUser>.ResetPasswordTokenPurpose,
            passwordToken);
        if (!validEmailToken || !validPasswordToken)
        {
            return InvalidInvitationLink();
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        if (!user.EmailConfirmed)
        {
            var confirmationResult = await userManager.ConfirmEmailAsync(user, emailToken);
            if (!confirmationResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return InvalidInvitationLink();
            }
        }

        var passwordResult = await userManager.ResetPasswordAsync(user, passwordToken, model.Password);
        if (!passwordResult.Succeeded)
        {
            await transaction.RollbackAsync();
            AddIdentityErrors(passwordResult);
            return View(model);
        }

        user.IsActive = true;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await transaction.RollbackAsync();
            logger.LogError("Invitation email was confirmed but account activation failed.");
            return View("AccountMessage", new AccountMessageViewModel
            {
                Title = "Tài khoản chưa được kích hoạt",
                Message = "Không thể kích hoạt tài khoản lúc này. Vui lòng liên hệ quản trị viên.",
                Succeeded = false
            });
        }

        await transaction.CommitAsync();

        return View("AccountMessage", new AccountMessageViewModel
        {
            Title = "Tài khoản đã được kích hoạt",
            Message = "Email đã được xác minh. Bạn có thể đăng nhập bằng mật khẩu vừa tạo.",
            Succeeded = true
        });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        PreventTokenCaching();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            return View("AccountMessage", new AccountMessageViewModel
            {
                Title = "Liên kết không hợp lệ",
                Message = "Hãy yêu cầu một liên kết đặt lại mật khẩu mới.",
                Succeeded = false
            });
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        PreventTokenCaching();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        var token = DecodeToken(model.Token);
        if (user is null || token is null || !user.IsActive || !user.EmailConfirmed)
        {
            ModelState.AddModelError(string.Empty, "Liên kết không hợp lệ hoặc đã hết hạn.");
            return View(model);
        }

        var result = await userManager.ResetPasswordAsync(user, token, model.Password);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        return View("AccountMessage", new AccountMessageViewModel
        {
            Title = "Đặt lại mật khẩu thành công",
            Message = "Bạn có thể đăng nhập bằng mật khẩu mới.",
            Succeeded = true
        });
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            await signInManager.SignOutAsync();
            return RedirectToAction(nameof(Login));
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        ViewBag.StatusMessage = "Mật khẩu đã được cập nhật.";
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Email = user.Email ?? string.Empty
        });
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddIdentityErrors(result);
            return View(model);
        }

        ViewBag.StatusMessage = "Thông tin tài khoản đã được cập nhật.";
        model.Email = user.Email ?? string.Empty;
        return View(model);
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task SendConfirmationEmailAsync(ApplicationUser user)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var link = BuildEmailUri(nameof(ConfirmEmail), new
        {
            userId = user.Id,
            token = EncodeToken(token)
        });

        await emailSender.SendAccountActionAsync(
            user.Email!,
            "Xác minh email",
            "Xác minh địa chỉ email",
            "Nhấn nút bên dưới để xác minh email và kích hoạt tài khoản. Liên kết có hiệu lực trong một giờ.",
            "Xác minh email",
            link);
    }

    private Uri BuildEmailUri(string action, object routeValues)
    {
        var path = Url.Action(action, "Account", routeValues);
        if (string.IsNullOrWhiteSpace(path) || !path.StartsWith('/'))
        {
            throw new InvalidOperationException("Could not generate a local account action URL.");
        }

        var baseUri = new Uri(emailOptions.Value.PublicBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        return new Uri(baseUri, path.TrimStart('/'));
    }

    private IActionResult InvalidConfirmationLink()
    {
        return View("AccountMessage", new AccountMessageViewModel
        {
            Title = "Không thể xác minh email",
            Message = "Liên kết không hợp lệ hoặc đã hết hạn. Hãy yêu cầu gửi lại email xác minh.",
            Succeeded = false
        });
    }

    private IActionResult InvalidInvitationLink()
    {
        return View("AccountMessage", new AccountMessageViewModel
        {
            Title = "Liên kết mời không hợp lệ",
            Message = "Liên kết không hợp lệ hoặc đã hết hạn. Hãy liên hệ quản trị viên để gửi lời mời mới.",
            Succeeded = false
        });
    }

    private void PreventTokenCaching()
    {
        Response.Headers.CacheControl = "no-store";
        Response.Headers["Referrer-Policy"] = "no-referrer";
    }

    private void AddIdentityErrors(IdentityResult result)
    {
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
    }

    private string? GetClientAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        return !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToAction("Index", "Student");
    }

    private static string EncodeToken(string token)
    {
        return WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    }

    private static string? DecodeToken(string token)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
