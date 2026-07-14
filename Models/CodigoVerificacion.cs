using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using delivery_system_dotnet.Models.Enums;

namespace delivery_system_dotnet.Models;

public class CodigoVerificacion
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(6)]
    public string Codigo { get; set; } = null!;

    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

    [Required]
    public DateTime FechaExpiracion { get; set; }

    public bool Usado { get; set; } = false;

    [Required]
    public TipoCodigo Tipo { get; set; }

    [Required]
    public string UsuarioDni { get; set; } = null!;

    [ForeignKey("UsuarioDni")]
    public Person Usuario { get; set; } = null!;
}
