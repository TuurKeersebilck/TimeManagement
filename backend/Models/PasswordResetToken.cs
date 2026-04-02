using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class PasswordResetToken
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    /// <summary>SHA-256 hash of the raw token sent in the reset link.</summary>
    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public bool Used { get; set; }
}
