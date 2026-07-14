using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace delivery_system_dotnet.Models
{
    public class ProductImageViewModel
    {
        public Products Product { get; set; } = new Products();

        [Display(Name = "Imagen del Producto")]
        public IFormFile? ImageFile { get; set; }

        [Display(Name = "Imagen (URL)")]
        [Url(ErrorMessage = "Debe ser una URL válida (https://...)")]
        public string? ImageUrl { get; set; }
    }
}
