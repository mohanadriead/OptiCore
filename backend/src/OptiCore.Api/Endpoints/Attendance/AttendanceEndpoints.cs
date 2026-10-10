using OptiCore.Api.Security;
using OptiCore.Application.Attendance;

namespace OptiCore.Api.Endpoints.Attendance;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance").RequireAuthorization("Manager");
        group.MapPost("/{employeeNumber:int}/check-in", async (int employeeNumber, HttpContext context,
            IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.CheckInAsync(employeeNumber, CurrentEmployee.Read(context), ct)));
        group.MapPost("/{employeeNumber:int}/check-out", async (int employeeNumber, HttpContext context,
            IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.CheckOutAsync(employeeNumber, CurrentEmployee.Read(context), ct)));
        return app;
    }
}
