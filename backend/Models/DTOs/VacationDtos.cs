namespace TimeManagementBackend.Models.DTOs;

public class VacationBalanceDto
{
    public int VacationTypeId { get; set; }
    public string VacationTypeName { get; set; } = string.Empty;
    public string? VacationTypeColor { get; set; }
    public decimal YearlyBalance { get; set; }
    public decimal UsedDays { get; set; }
    public decimal RemainingDays { get; set; }
}

public class VacationDayDto
{
    public int Id { get; set; }
    public int VacationTypeId { get; set; }
    public string VacationTypeName { get; set; } = string.Empty;
    public string? VacationTypeColor { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}

public class CreateVacationDayDto
{
    public int VacationTypeId { get; set; }
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string? Note { get; set; }
}
