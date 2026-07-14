using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using delivery_system_dotnet.DTOs.Response;

namespace delivery_system_dotnet.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
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
            _logger.LogError(ex, "Ha ocurrido una excepción no manejada.");
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var isApi = context.Request.Path.StartsWithSegments("/api");

        var statusCode = exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            KeyNotFoundException => HttpStatusCode.NotFound,
            ArgumentException => HttpStatusCode.BadRequest,
            InvalidOperationException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        if (isApi)
        {
            var message = exception.Message;
            if (statusCode == HttpStatusCode.InternalServerError && !_env.IsDevelopment())
            {
                message = "Ha ocurrido un error interno en el servidor.";
            }

            var responseDto = new ApiResponseDto
            {
                Success = false,
                Message = message,
                Errors = new List<string> { exception.GetType().Name }
            };

            // Si es desarrollo, podemos adjuntar más detalles
            if (_env.IsDevelopment())
            {
                responseDto.Errors.Add(exception.StackTrace ?? string.Empty);
            }

            var json = JsonSerializer.Serialize(responseDto, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
        else
        {
            // Para flujos MVC tradicionales, redirigir a una página de error o mostrar mensaje
            context.Response.Redirect($"/Auth/Login?error={Uri.EscapeDataString(exception.Message)}");
        }
    }
}
