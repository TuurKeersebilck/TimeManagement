using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class EmployeeInvite
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the raw token sent in the invite link.</summary>
    [Required]
    [MaxLength(64)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Used { get; set; }

    [Required]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByUserId))]
    public User CreatedByUser { get; set; } = null!;
}
