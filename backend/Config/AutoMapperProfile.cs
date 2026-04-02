using AutoMapper;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;

namespace TimeManagementBackend.Config;

public class AutoMapperProfile : Profile
{
    public AutoMapperProfile()
    {
        CreateMap<TimeLog, TimeLogDto>();
        CreateMap<TimeLogFormDto, TimeLog>();
        CreateMap<TimeLog, TimeLogFormDto>();
    }
}