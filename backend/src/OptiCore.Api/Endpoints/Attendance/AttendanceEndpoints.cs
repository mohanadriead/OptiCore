using OptiCore.Api.Security;
using OptiCore.Application.Attendance;

namespace OptiCore.Api.Endpoints.Attendance;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/attendance").RequireAuthorization("Employee");
        group.MapGet("/{employeeNumber:int}/status", async (int employeeNumber, HttpContext context,
            IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.StatusAsync(employeeNumber, CurrentEmployee.Read(context), ct)));
        group.MapPost("/{employeeNumber:int}/check-in", async (int employeeNumber, HttpContext context,
            IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.CheckInAsync(employeeNumber, CurrentEmployee.Read(context), ct)));
        group.MapPost("/{employeeNumber:int}/check-out", async (int employeeNumber, HttpContext context,
            IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.CheckOutAsync(employeeNumber, CurrentEmployee.Read(context), ct)));
        var management = group.MapGroup("/management").RequireAuthorization("Manager");
        management.MapGet("/records", async (int? employeeNumber, DateOnly? from, DateOnly? to,
            int? page, int? pageSize, HttpContext context, IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.HistoryAsync(employeeNumber, from, to, page ?? 1, pageSize ?? 50, CurrentEmployee.Read(context), ct)));
        management.MapGet("/records/{id:guid}", async (Guid id, HttpContext context, IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.DetailsAsync(id, CurrentEmployee.Read(context), ct)));
        management.MapPost("/records/{id:guid}/corrections", async (Guid id, CorrectAttendanceRequest request,
            HttpContext context, IAttendanceService service, CancellationToken ct) =>
            Results.Ok(await service.CorrectAsync(id, request, CurrentEmployee.Read(context), ct)));
        return app;
    }
}
