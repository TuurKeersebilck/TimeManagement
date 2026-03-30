namespace TimeManagementBackend.Models.DTOs;

public class AdminTimeLogDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public TimeSpan? BreakStart { get; set; }
    public TimeSpan? BreakEnd { get; set; }
    public string? Description { get; set; }

    public double TotalHours
    {
        get
        {
            var worked = EndTime - StartTime;
            if (BreakStart.HasValue && BreakEnd.HasValue)
                worked -= BreakEnd.Value - BreakStart.Value;
            return worked.TotalHours;
        }
    }
}

public class EmployeeDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class VacationTypeDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
    public int AssignedEmployeeCount { get; set; }
}

public class VacationTypeCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
}

public class VacationTypeUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Color { get; set; }
}

public class EmployeeVacationBalanceDto
{
    public int Id { get; set; }
    public int VacationTypeId { get; set; }
    public string VacationTypeName { get; set; } = string.Empty;
    public string? VacationTypeColor { get; set; }
    public decimal YearlyBalance { get; set; }
}

public class AssignVacationTypeDto
{
    public int VacationTypeId { get; set; }
    public decimal YearlyBalance { get; set; }
}

public class UpdateVacationBalanceDto
{
    public decimal YearlyBalance { get; set; }
}

public class AdminVacationDayDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public int VacationTypeId { get; set; }
    public string VacationTypeName { get; set; } = string.Empty;
    public string? VacationTypeColor { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
