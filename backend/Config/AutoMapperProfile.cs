using AutoMapper;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Config;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<WorkSession, WorkSessionDto>();
        CreateMap<BreakRecord, BreakRecordDto>();
        CreateMap<WorkDay, WorkDayDto>();

        CreateMap<TimeBankAdjustment, TimeBankAdjustmentDto>()
            .ForMember(d => d.CreatedByName, opt => opt.MapFrom(
                s => s.CreatedByUser != null ? s.CreatedByUser.FullName : null));

        CreateMap<VacationType, VacationTypeDto>()
            .ForMember(d => d.AssignedEmployeeCount, opt => opt.MapFrom(s => s.EmployeeBalances.Count()));

        CreateMap<VacationDay, VacationDayDto>()
            .ForMember(d => d.VacationTypeName, opt => opt.MapFrom(s => s.VacationType.Name))
            .ForMember(d => d.VacationTypeColor, opt => opt.MapFrom(s => s.VacationType.Color));

        CreateMap<MonthlySettlement, MonthlySettlementDto>()
            .ForMember(d => d.EmployeeName, opt => opt.MapFrom(
                s => s.User != null ? s.User.FullName : ""))
            .ForMember(d => d.ReviewedByName, opt => opt.MapFrom(
                s => s.ReviewedByUser != null ? s.ReviewedByUser.FullName : null));
    }
}