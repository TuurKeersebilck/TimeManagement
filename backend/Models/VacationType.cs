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
    public string? Color { get; set; }

    public ICollection<EmployeeVacationBalance> EmployeeBalances { get; set; } = new List<EmployeeVacationBalance>();
}
