using System.ComponentModel.DataAnnotations;

namespace DemoMVC.Options;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required]
    public string SmtpHost { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; set; } = 587;

    [Required]
    public string SmtpUserName { get; set; } = string.Empty;

    [Required]
    public string SmtpPassword { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;

    [Required]
    public string SenderName { get; set; } = string.Empty;

    [Required]
    public string PublicBaseUrl { get; set; } = string.Empty;
}
