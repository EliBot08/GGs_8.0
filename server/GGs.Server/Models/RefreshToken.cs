using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace GGs.Server.Models;

public sealed class RefreshToken
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string TokenHash { get; set; } = string.Empty; // SHA256

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }

    public string? DeviceId { get; set; }
    public string? UserAgent { get; set; }
}

