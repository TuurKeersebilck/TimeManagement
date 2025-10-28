using AutoMapper;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Config;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<TimeLog, TimeLogDto>()
            .ForMember(dest => dest.TotalHours, opt => opt.MapFrom(src => (src.EndTime - src.StartTime - src.Break).TotalHours));

        CreateMap<TimeLogCreateDto, TimeLog>();
        CreateMap<TimeLogUpdateDto, TimeLog>();
        CreateMap<TimeLog, TimeLogCreateDto>();
    }
}