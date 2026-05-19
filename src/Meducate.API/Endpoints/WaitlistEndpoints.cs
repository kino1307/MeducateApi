using Meducate.Domain.Services;
using Microsoft.AspNetCore.Authorization;

namespace Meducate.API.Endpoints;

internal static class WaitlistEndpoints
{
    private sealed record WaitlistRequest(string Email);

    internal static WebApplication MapWaitlistEndpoints(this WebApplication app)
    {
        app.MapPost("/api/waitlist", [AllowAnonymous] async (
            WaitlistRequest request,
            IEmailService emailSvc,
            ILogger<Program> logger,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@') || request.Email.Length > 320)
                return Results.Problem("A valid email address is required.", statusCode: StatusCodes.Status400BadRequest);

            try
            {
                await emailSvc.SendWaitlistNotificationAsync(request.Email.Trim());
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send waitlist notification for {Email}", request.Email);
                return Results.Problem("Unable to join the waitlist at this time. Please try again later.",
                    statusCode: StatusCodes.Status502BadGateway);
            }

            return Results.Ok(new { message = "You're on the list. We'll be in touch when Pro launches." });
        })
        .WithName("JoinWaitlist")
        .WithSummary("Join Pro waitlist")
        .Produces(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status502BadGateway)
        .WithTags("Waitlist");

        return app;
    }
}
