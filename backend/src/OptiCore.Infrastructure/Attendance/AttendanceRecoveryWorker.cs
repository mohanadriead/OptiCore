using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OptiCore.Application.Attendance;

namespace OptiCore.Infrastructure.Attendance;

public sealed class AttendanceRecoveryWorker(IServiceScopeFactory scopes, ILogger<AttendanceRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IAttendanceService>().RecoverAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception)
            {
                // Do not log database exception details, which can contain sensitive configuration.
                logger.LogWarning("Attendance recovery failed; it will be retried.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
