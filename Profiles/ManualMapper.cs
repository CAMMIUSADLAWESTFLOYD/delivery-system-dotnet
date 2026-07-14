using System;
using System.Collections;
using System.Collections.Generic;
using delivery_system_dotnet.DTOs.Request;
using delivery_system_dotnet.DTOs.Response;
using delivery_system_dotnet.Models;

namespace delivery_system_dotnet.Profiles;

public interface IMapper
{
    TDestination Map<TDestination>(object source);
}

public class ManualMapper : IMapper
{
    public TDestination Map<TDestination>(object source)
    {
        if (source == null) return default!;

        // 1. DTO -> Entity
        if (typeof(TDestination) == typeof(Client) && source is RegistroClienteDto rCliente)
        {
            var client = new Client
            {
                Dni = rCliente.Dni,
                Name = rCliente.Name,
                DateBirth = rCliente.DateBirth,
                Address = rCliente.Address,
                Mail = rCliente.Mail,
                Gender = rCliente.Gender,
                Phone = rCliente.Phone,
                PayMethod = rCliente.PayMethod
            };
            return (TDestination)(object)client;
        }

        if (typeof(TDestination) == typeof(Delivery) && source is RegistroDeliveryDto rDelivery)
        {
            var delivery = new Delivery
            {
                Dni = rDelivery.Dni,
                Name = rDelivery.Name,
                DateBirth = rDelivery.DateBirth,
                Address = rDelivery.Address,
                Mail = rDelivery.Mail,
                Gender = rDelivery.Gender,
                Phone = rDelivery.Phone,
                TypeVehicle = rDelivery.TypeVehicle,
                LicensePlate = rDelivery.LicensePlate
            };
            return (TDestination)(object)delivery;
        }

        if (typeof(TDestination) == typeof(Admin) && source is RegistroAdminDto rAdmin)
        {
            var admin = new Admin
            {
                Dni = rAdmin.Dni,
                Name = rAdmin.Name,
                DateBirth = rAdmin.DateBirth,
                Address = rAdmin.Address,
                Mail = rAdmin.Mail,
                Gender = rAdmin.Gender,
                Phone = rAdmin.Phone,
                Succursal = rAdmin.Succursal,
                Rol = rAdmin.Rol,
                Salary = rAdmin.Salary
            };
            return (TDestination)(object)admin;
        }

        // 2. Entity -> DTO
        if (typeof(TDestination) == typeof(UsuarioResponseDto) && source is Person person)
        {
            string rolName = person switch
            {
                Admin => "Admin",
                Client => "Client",
                Delivery => "Delivery",
                _ => "Unknown"
            };

            var dto = new UsuarioResponseDto
            {
                Dni = person.Dni,
                Name = person.Name,
                Mail = person.Mail,
                Phone = person.Phone,
                Rol = rolName,
                Gender = person.Gender,
                Address = person.Address
            };

            if (person is Client c)
            {
                dto.PayMethod = c.PayMethod;
            }
            else if (person is Delivery d)
            {
                dto.TypeVehicle = d.TypeVehicle;
                dto.LicensePlate = d.LicensePlate;
            }
            else if (person is Admin a)
            {
                dto.Succursal = a.Succursal;
                dto.Salary = a.Salary;
            }

            return (TDestination)(object)dto;
        }

        // 3. Lists
        if (typeof(TDestination) == typeof(List<UsuarioResponseDto>) && source is IEnumerable list)
        {
            var result = new List<UsuarioResponseDto>();
            foreach (var item in list)
            {
                if (item is Person p)
                {
                    result.Add(Map<UsuarioResponseDto>(p));
                }
            }
            return (TDestination)(object)result;
        }

        throw new InvalidOperationException($"No se ha definido un mapeo manual desde {source.GetType().Name} hacia {typeof(TDestination).Name}");
    }
}
