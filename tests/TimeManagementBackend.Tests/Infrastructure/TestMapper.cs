using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using TimeManagementBackend.Config;

namespace TimeManagementBackend.Tests.Infrastructure;

/// <summary>Builds the app's real AutoMapper configuration — the same profile Program.cs registers,
/// so a mapping that is broken here is broken in production too.</summary>
internal static class TestMapper
{
    public static MapperConfiguration CreateConfiguration()
    {
        var expression = new MapperConfigurationExpression();
        expression.AddProfile<AutoMapperProfile>();
        return new MapperConfiguration(expression, NullLoggerFactory.Instance);
    }

    public static IMapper Create() => new Mapper(CreateConfiguration());
}
