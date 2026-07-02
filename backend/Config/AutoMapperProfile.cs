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
    }
}