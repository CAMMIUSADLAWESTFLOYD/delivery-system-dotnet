namespace delivery_system_dotnet.Models;

// ViewModel de presentación para el panel de gestión de administradores.
// Combina el Admin con su estado "Activo" (propiedad sombra de Person).
public class AdminItemViewModel
{
    public Admin Admin { get; set; } = null!;
    public bool Activo { get; set; }
}
