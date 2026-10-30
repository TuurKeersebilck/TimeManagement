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

    /// <summary>
    /// No yearly balance (e.g. sick leave): available to every employee without being
    /// assigned, and never checked against a balance. Balance rows left over from before
    /// the type was made unlimited are ignored, and apply again if it is switched back.
    /// </summary>
    public bool IsUnlimited { get; set; }

    /// <summary>
    /// Only an admin can book, change or remove days of this type (e.g. sick leave);
    /// employees still see them in their own overview.
    /// </summary>
    public bool IsAdminOnly { get; set; }

    public ICollection<EmployeeVacationBalance> EmployeeBalances { get; set; } = new List<EmployeeVacationBalance>();

    public bool IsDeleted { get; set; } = false;
    public DateTimeOffset? DeletedAt { get; set; }
}
