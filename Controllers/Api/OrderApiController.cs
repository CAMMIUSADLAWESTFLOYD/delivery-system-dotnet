using System;
using Microsoft.AspNetCore.Mvc;
using delivery_system_dotnet.Interfaces;
using delivery_system_dotnet.DTOs.Response;

namespace delivery_system_dotnet.Controllers.Api;

[ApiController]
[Route("api/v1/orders")]
public class OrderApiController : ControllerBase
{
    private readonly IOrderManager _orderManager;

    public OrderApiController(IOrderManager orderManager)
    {
        _orderManager = orderManager;
    }

    [HttpPost("{orderId}/status")]
    public IActionResult ChangeStatus(string orderId, [FromQuery] string userRole, [FromQuery] string newStatus)
    {
        try
        {
            if (string.IsNullOrEmpty(userRole) || string.IsNullOrEmpty(newStatus))
            {
                return BadRequest(new ApiResponseDto(false, "El rol de usuario y el nuevo estado son requeridos."));
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
