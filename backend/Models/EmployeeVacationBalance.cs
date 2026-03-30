using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TimeManagementBackend.Models;

public class EmployeeVacationBalance
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

    [Required]
    [Column(TypeName = "decimal(5,1)")]
    public decimal YearlyBalance { get; set; }
}
