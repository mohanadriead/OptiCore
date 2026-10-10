using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OptiCore.Application.Attendance;
using OptiCore.Application.Employees;
using OptiCore.Infrastructure.Attendance;
using OptiCore.Tests.Employees;

namespace OptiCore.Tests.Attendance;

public sealed class AttendanceWorkerTests
{
    [Fact]
    public async Task WorkerRecoversImmediatelyOnStartup()
    {
        var employees = new FakeEmployeeRepository();
        var actor = employees.Seed();
        var records = new FakeAttendanceRepository();
        var clock = new TestClock(DateTimeOffset.Parse("2026-07-01T10:00:00Z"));
        await new AttendanceService(records, employees, clock, new()).CheckInAsync(1, actor.Id, default);
        clock.Now = clock.Now.AddDays(2);
        var services = new ServiceCollection();
        services.AddSingleton<IEmployeeRepository>(employees);
        services.AddSingleton<IAttendanceRepository>(records);
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<AttendanceMidnightPolicy>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        await using var provider = services.BuildServiceProvider();
        using var worker = new AttendanceRecoveryWorker(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<AttendanceRecoveryWorker>.Instance);
        await worker.StartAsync(default);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (records.Rows[0].CheckOutAtUtc is null) await Task.Delay(10, timeout.Token);
        await worker.StopAsync(timeout.Token);
        Assert.True(records.Rows[0].WasCheckoutAutomatic);
        Assert.Equal(clock.Now, records.Rows[0].CheckoutProcessedAtUtc);
    }
}
