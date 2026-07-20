using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using delivery_system_dotnet.Interfaces;
using delivery_system_dotnet.DTOs.Response;

namespace delivery_system_dotnet.Controllers.Api;

[ApiController]
[Route("api/v1/orders")]
[Authorize] // requiere sesión iniciada: el rol se toma de los claims, nunca del request
[Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken] // API JSON: no usa formularios con token
public class OrderApiController : ControllerBase
{
    private readonly IOrderManager _orderManager;

    public OrderApiController(IOrderManager orderManager)
    {
        _orderManager = orderManager;
    }

    [HttpPost("{orderId}/status")]
    public IActionResult ChangeStatus(string orderId, [FromQuery] string newStatus)
    {
        try
        {
            if (string.IsNullOrEmpty(newStatus))
            {
                return BadRequest(new ApiResponseDto(false, "El nuevo estado es requerido."));
            }

            // SEGURIDAD: el rol viene EXCLUSIVAMENTE de la identidad autenticada.
            // (Antes llegaba por querystring y cualquiera podía enviar 'Admin'.)
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            if (string.IsNullOrEmpty(userRole))
            {
                return StatusCode(403, new ApiResponseDto(false, "La sesión no tiene un rol válido."));
            }

            int result = _orderManager.ChangeStatus(orderId, userRole, newStatus);
            if (result == 1)
            {
                return Ok(new ApiResponseDto(true, $"El estado de la orden {orderId} fue cambiado exitosamente a '{newStatus}'."));
            }
            else
            {
                return NotFound(new ApiResponseDto(false, $"No se encontró la orden con ID {orderId}."));
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new ApiResponseDto(false, ex.Message));
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiResponseDto(false, $"Error al actualizar el estado: {ex.Message}"));
        }
    }
}
