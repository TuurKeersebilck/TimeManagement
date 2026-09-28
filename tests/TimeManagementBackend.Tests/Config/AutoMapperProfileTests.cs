using AutoMapper;
using TimeManagementBackend.Models;
using TimeManagementBackend.Models.DTOs;
using TimeManagementBackend.Tests.Infrastructure;

namespace TimeManagementBackend.Tests.Config;

public class AutoMapperProfileTests
{
    private readonly IMapper _mapper = TestMapper.Create();

    [Fact]
    public void Configuration_IsValid()
    {
        // Catches a DTO property added without a matching source member — the failure mode
        // where a new field silently ships as null instead of the value it should carry.
        TestMapper.CreateConfiguration().AssertConfigurationIsValid();
    }

    [Fact]
    public void WorkSession_MapsBreaksAndBothStampPairs()
    {
        var clockIn = new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);
        var session = new WorkSession
        {
            Id = 7,
            Date = new DateOnly(2026, 3, 2),
            ClockIn = clockIn,
            ClockInServerStamp = clockIn.AddMinutes(2),
            ClockOut = clockIn.AddHours(8),
            ClockOutServerStamp = clockIn.AddHours(8).AddMinutes(1),
            Status = WorkSessionStatus.Closed,
            Breaks =
            [
                new BreakRecord
                {
                    Id = 11,
                    BreakStart = clockIn.AddHours(4),
                    BreakStartServerStamp = clockIn.AddHours(4),
                    BreakEnd = clockIn.AddHours(4).AddMinutes(30),
                    BreakEndServerStamp = clockIn.AddHours(4).AddMinutes(30),
                },
            ],
        };

        var dto = _mapper.Map<WorkSessionDto>(session);

        Assert.Equal(7, dto.Id);
        Assert.Equal(WorkSessionStatus.Closed, dto.Status);
        // The effective time and the immutable server stamp are distinct values and must
        // not be conflated — the audit trail depends on both surviving the mapping.
        Assert.Equal(clockIn, dto.ClockIn);
        Assert.Equal(clockIn.AddMinutes(2), dto.ClockInServerStamp);
        var mappedBreak = Assert.Single(dto.Breaks);
        Assert.Equal(11, mappedBreak.Id);
        Assert.Equal(clockIn.AddHours(4).AddMinutes(30), mappedBreak.BreakEnd);
    }

    [Fact]
    public void VacationDay_FlattensTypeNameAndColour()
    {
        var day = new VacationDay
        {
            Id = 3,
            Date = new DateOnly(2026, 7, 1),
            Amount = 0.5m,
            Note = "afternoon off",
            VacationType = new VacationType { Id = 2, Name = "Annual Leave", Color = "#112233" },
            VacationTypeId = 2,
        };

        var dto = _mapper.Map<VacationDayDto>(day);

        Assert.Equal("Annual Leave", dto.VacationTypeName);
        Assert.Equal("#112233", dto.VacationTypeColor);
        Assert.Equal(0.5m, dto.Amount);
        Assert.Equal("afternoon off", dto.Note);
    }

    [Fact]
    public void MonthlySettlement_ResolvesEmployeeAndReviewerNames()
    {
        var settlement = new MonthlySettlement
        {
            Id = 5,
            Year = 2026,
            Month = 3,
            NetBalanceHours = 4.25m,
            User = new User { FullName = "Emma Employee" },
            ReviewedByUser = new User { FullName = "Adam Admin" },
            Status = SettlementStatus.Settled,
            Outcome = SettlementOutcome.Paid,
        };

        var dto = _mapper.Map<MonthlySettlementDto>(settlement);

        Assert.Equal("Emma Employee", dto.EmployeeName);
        Assert.Equal("Adam Admin", dto.ReviewedByName);
        Assert.Equal(4.25m, dto.NetBalanceHours);
    }

    [Fact]
    public void MonthlySettlement_ToleratesMissingNavigationProperties()
    {
        // Settlements are frequently projected without Include(), so a null User must not throw.
        var dto = _mapper.Map<MonthlySettlementDto>(new MonthlySettlement { Id = 1, Year = 2026, Month = 1 });

        Assert.Equal(string.Empty, dto.EmployeeName);
        Assert.Null(dto.ReviewedByName);
    }

    [Fact]
    public void TimeBankAdjustment_ResolvesCreatorName_AndToleratesNullCreator()
    {
        var withCreator = _mapper.Map<TimeBankAdjustmentDto>(new TimeBankAdjustment
        {
            Id = 1,
            Hours = -2m,
            Reason = "correction",
            EffectiveDate = new DateOnly(2026, 3, 1),
            CreatedByUser = new User { FullName = "Adam Admin" },
        });

        // CreatedByUser is deliberately nullable so deleting an admin doesn't cascade.
        var withoutCreator = _mapper.Map<TimeBankAdjustmentDto>(new TimeBankAdjustment
        {
            Id = 2,
            Hours = 1m,
            Reason = "carry-over",
            EffectiveDate = new DateOnly(2026, 3, 1),
        });

        Assert.Equal("Adam Admin", withCreator.CreatedByName);
        Assert.Equal(-2m, withCreator.Hours);
        Assert.Null(withoutCreator.CreatedByName);
    }

    [Fact]
    public void VacationType_CountsAssignedEmployees()
    {
        var type = new VacationType
        {
            Id = 1,
            Name = "Annual Leave",
            EmployeeBalances = [new EmployeeVacationBalance(), new EmployeeVacationBalance()],
        };

        Assert.Equal(2, _mapper.Map<VacationTypeDto>(type).AssignedEmployeeCount);
    }
}
