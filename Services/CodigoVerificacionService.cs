using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using delivery_system_dotnet.Data;
using delivery_system_dotnet.Models;
using delivery_system_dotnet.Models.Enums;

namespace delivery_system_dotnet.Services;

public class CodigoVerificacionService : ICodigoVerificacionService
{
    private readonly ApplicationDbContext _context;

    public CodigoVerificacionService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerarCodigoOtpAsync(Person usuario, TipoCodigo tipo)
    {
        // Invalidar códigos anteriores del mismo tipo para el usuario
        var anteriores = await _context.CodigosVerificacion
            .Where(c => c.UsuarioDni == usuario.Dni && c.Tipo == tipo && !c.Usado)
            .ToListAsync();

        foreach (var anterior in anteriores)
        {
            anterior.Usado = true;
        }

        // Generar un código aleatorio de 6 dígitos
        var random = new Random();
        var codigo = random.Next(100000, 999999).ToString();

        var otp = new CodigoVerificacion
        {
            Codigo = codigo,
            FechaCreacion = DateTime.UtcNow,
            FechaExpiracion = DateTime.UtcNow.AddMinutes(10),
            Usado = false,
            Tipo = tipo,
            UsuarioDni = usuario.Dni
        };

        _context.CodigosVerificacion.Add(otp);
        await _context.SaveChangesAsync();

        return codigo;
    }

    public async Task<bool> ValidarCodigoOtpAsync(Person usuario, string codigo, TipoCodigo tipo)
    {
        var otp = await _context.CodigosVerificacion
            .Where(c => c.UsuarioDni == usuario.Dni 
                        && c.Codigo == codigo 
                        && c.Tipo == tipo 
                        && !c.Usado 
                        && c.FechaExpiracion >= DateTime.UtcNow)
            .FirstOrDefaultAsync();

        if (otp == null)
        {
            return false;
        }

        // Marcar como usado
        otp.Usado = true;
        await _context.SaveChangesAsync();
        return true;
    }
}
