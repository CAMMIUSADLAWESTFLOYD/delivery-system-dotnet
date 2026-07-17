using System;
using System.Collections.Generic;
using System.Linq;

namespace delivery_system_dotnet.Models.OrderStates;

// ============================================================================
//  PATRÓN STATE — Estado del pedido como jerarquía de clases.
//
//  Cada estado concreto (PendingState, PaidState, ShippedState, DeliveredState,
//  CancelledState, ...) encapsula:
//    • qué valores de BD lo representan (la BD guarda los estados en español,
//      p. ej. "Pendiente"/"PendienteRepartidor" ≡ Pending),
//    • a qué estados puede transicionar y qué rol puede hacerlo.
//
//  RealOrderManager consulta esta máquina de estados ANTES de persistir un
//  cambio: una transición inválida lanza InvalidOperationException y el
//  pedido no se toca. Así las reglas del ciclo de vida viven en una sola
//  parte del sistema en lugar de estar repartidas por los controladores.
// ============================================================================
public abstract class OrderState
{
    // Nombre canónico del estado (terminología del enunciado: Pending, Paid...).
    public abstract string Name { get; }

    // Valores de Order.Status (BD) que este estado representa.
    public abstract string[] DbStatuses { get; }

    // ¿Es un estado terminal? (no admite ninguna transición de salida)
    public virtual bool IsTerminal => false;

    // Transiciones permitidas: estado destino -> roles que pueden ejecutarla.
    protected abstract Dictionary<Type, string[]> AllowedTransitions { get; }

    public bool CanTransitionTo(OrderState target, string role)
    {
        if (IsTerminal) return false;
        // Movimiento dentro del mismo estado lógico (p. ej. Pendiente ->
        // PendienteRepartidor, ambos Pending) siempre es válido.
        if (target.GetType() == GetType()) return true;
        if (!AllowedTransitions.TryGetValue(target.GetType(), out var roles)) return false;
        return roles.Contains(role);
    }

    // ------------------------- Resolución de estados -------------------------

    private static readonly OrderState[] All =
    {
        new PendingState(),
        new PaidState(),
        new ShippedState(),
        new DeliveredState(),
        new CancelRequestedState(),
        new CancelledState(),
        new ReportedState()
    };

    // Devuelve el estado (clase) que representa un valor de Status de la BD.
    public static OrderState FromDbStatus(string dbStatus)
    {
        var state = All.FirstOrDefault(s =>
            s.DbStatuses.Contains(dbStatus, StringComparer.OrdinalIgnoreCase));
        if (state == null)
            throw new InvalidOperationException($"Estado de pedido desconocido: '{dbStatus}'.");
        return state;
    }

    // Valida la transición y devuelve el estado destino; lanza si es inválida.
    public static OrderState ValidateTransition(string currentDbStatus, string newDbStatus, string role)
    {
        var current = FromDbStatus(currentDbStatus);
        var target = FromDbStatus(newDbStatus);

        if (!current.CanTransitionTo(target, role))
        {
            throw new InvalidOperationException(
                $"Transición inválida: un pedido en estado {current.Name} ('{currentDbStatus}') " +
                $"no puede pasar a {target.Name} ('{newDbStatus}') por el rol {role}.");
        }
        return target;
    }
}

// Pedido creado, aún sin aceptar por un repartidor (equivale a "no pagado/confirmado").
public class PendingState : OrderState
{
    public override string Name => "Pending";
    public override string[] DbStatuses => new[] { "Pendiente", "PendienteRepartidor" };

    protected override Dictionary<Type, string[]> AllowedTransitions => new()
    {
        // El repartidor acepta (o el admin fuerza la aceptación) => pasa a Paid.
        { typeof(PaidState), new[] { "Delivery", "Admin" } },
        // Cliente o admin pueden cancelar mientras siga pendiente.
        { typeof(CancelledState), new[] { "Client", "Admin" } }
    };
}

// Pedido aceptado/confirmado: el pago está comprometido y el local lo prepara.
public class PaidState : OrderState
{
    public override string Name => "Paid";
    public override string[] DbStatuses => new[] { "Aceptado", "Preparando" };

    protected override Dictionary<Type, string[]> AllowedTransitions => new()
    {
        { typeof(ShippedState), new[] { "Delivery", "Admin" } },
        { typeof(CancelledState), new[] { "Admin" } },
        { typeof(CancelRequestedState), new[] { "Delivery" } },
        { typeof(ReportedState), new[] { "Delivery", "Admin" } }
    };
}

// Pedido en ruta hacia el cliente.
public class ShippedState : OrderState
{
    public override string Name => "Shipped";
    public override string[] DbStatuses => new[] { "En Camino" };

    protected override Dictionary<Type, string[]> AllowedTransitions => new()
    {
        // "System" es el simulador de recorrido (entrega automática al llegar).
        { typeof(DeliveredState), new[] { "Delivery", "Admin", "System" } },
        { typeof(CancelledState), new[] { "Admin" } },
        { typeof(CancelRequestedState), new[] { "Delivery" } },
        { typeof(ReportedState), new[] { "Delivery", "Admin" } }
    };
}

// El repartidor pidió al administrador cancelar la entrega.
public class CancelRequestedState : OrderState
{
    public override string Name => "CancelRequested";
    public override string[] DbStatuses => new[] { "CancelacionSolicitada" };

    protected override Dictionary<Type, string[]> AllowedTransitions => new()
    {
        // Solo el admin resuelve: cancela definitivamente o reanuda la entrega.
        { typeof(CancelledState), new[] { "Admin" } },
        { typeof(PaidState), new[] { "Admin" } },
        { typeof(ShippedState), new[] { "Admin" } }
    };
}

// Incidencia reportada por el repartidor: el admin decide cómo continúa.
public class ReportedState : OrderState
{
    public override string Name => "Reported";
    public override string[] DbStatuses => new[] { "Reportado" };

    protected override Dictionary<Type, string[]> AllowedTransitions => new()
    {
        { typeof(CancelledState), new[] { "Admin" } },
        { typeof(PaidState), new[] { "Admin" } },
        { typeof(ShippedState), new[] { "Admin" } }
    };
}

// Estado terminal: entregado.
public class DeliveredState : OrderState
{
    public override string Name => "Delivered";
    public override string[] DbStatuses => new[] { "Entregado", "Entregada" };
    public override bool IsTerminal => true;
    protected override Dictionary<Type, string[]> AllowedTransitions => new();
}

// Estado terminal: cancelado.
public class CancelledState : OrderState
{
    public override string Name => "Cancelled";
    public override string[] DbStatuses => new[] { "Cancelado" };
    public override bool IsTerminal => true;
    protected override Dictionary<Type, string[]> AllowedTransitions => new();
}
