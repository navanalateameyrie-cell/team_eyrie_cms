using System.Text.Json;
using CMS.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace CMS.Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, ex);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted) return;

        // Prefer the X-Correlation-Id value our middleware stamped on the request (so operators can
        // grep logs by the exact id the client saw) — fall back to TraceIdentifier for the pre-middleware edge.
        var correlationId = context.RequestServices.GetService<ICorrelationContext>()?.CorrelationId;
        if (string.IsNullOrWhiteSpace(correlationId)) correlationId = context.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        if (_env.IsDevelopment())
        {
            problem.Detail = ex.ToString();
        }

        context.Response.Clear();
        context.Response.StatusCode = problem.Status.Value;
        context.Response.ContentType = "application/problem+json";
        // Make sure the response carries the same correlation id as the body for header-based grep.
        if (!context.Response.Headers.ContainsKey(CorrelationIdMiddleware.HeaderName))
            context.Response.Headers[CorrelationIdMiddleware.HeaderName] = correlationId;
        await JsonSerializer.SerializeAsync(context.Response.Body, problem);
    }
}
