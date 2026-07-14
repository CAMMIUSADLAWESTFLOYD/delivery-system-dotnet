using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace delivery_system_dotnet.Models
{
    // Formulario de alta/edición de restaurantes (Admin).
    // La imagen puede venir como archivo subido o como URL externa;
    // si llegan ambas, el archivo tiene prioridad.
    public class RestaurantFormViewModel
    {
        public Restaurant Restaurant { get; set; } = new Restaurant();

        [Display(Name = "Imagen (archivo)")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Imagen (URL)")]
        [Url(ErrorMessage = "Debe ser una URL válida (https://...)")]
        public string? ImageUrl { get; set; }
    }
}
