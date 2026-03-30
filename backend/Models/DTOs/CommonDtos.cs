namespace TimeManagementBackend.Models.DTOs;

public class ErrorResponseDto
{
    public string Message { get; set; } = string.Empty;
    public string? Code { get; set; }
}
