using System.ComponentModel.DataAnnotations;

namespace TimeManagementBackend.Models;

public class VacationType
{
    [Key]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? Description { get; set; }

    [MaxLength(7)]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a valid hex code")]
    public string? Color { get; set; }

    public ICollection<EmployeeVacationBalance> EmployeeBalances { get; set; } = new List<EmployeeVacationBalance>();

    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}
