using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class VacationDay
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    public int VacationTypeId { get; set; }

    [ForeignKey(nameof(VacationTypeId))]
    public VacationType VacationType { get; set; } = null!;

    public DateOnly Date { get; set; }

    /// <summary>1.0 = full day, 0.5 = half day</summary>
    [Column(TypeName = "decimal(3,1)")]
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}
