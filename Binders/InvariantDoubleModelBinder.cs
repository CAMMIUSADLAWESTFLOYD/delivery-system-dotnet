using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace delivery_system_dotnet.Binders;

// Model binder para double/double? con cultura INVARIANTE.
//
// Motivo: el binder por defecto parsea los valores de formulario con la cultura
// regional del servidor. En culturas con coma decimal (es-EC), una coordenada
// enviada como "0.362415" se interpretaba como 362415 (el punto se descartaba),
// corrompiendo la latitud/longitud y provocando errores de ruteo/snapping.
// Este binder acepta tanto punto como coma decimal y garantiza que las
// coordenadas capturadas por el PinSelector lleguen intactas al servidor.
public class InvariantDoubleModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);
        var value = valueResult.FirstValue;

        if (string.IsNullOrWhiteSpace(value))
        {
            // double? sin valor => null; double sin valor => sin resultado (default).
            if (Nullable.GetUnderlyingType(bindingContext.ModelType) != null)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            return Task.CompletedTask;
        }

        // Normaliza coma decimal a punto (las coordenadas nunca llevan separador de miles).
        var normalized = value.Trim().Replace(',', '.');

        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            bindingContext.Result = ModelBindingResult.Success(parsed);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Valor numérico inválido.");
        }
        return Task.CompletedTask;
    }
}

public class InvariantDoubleBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        return context.Metadata.ModelType == typeof(double) || context.Metadata.ModelType == typeof(double?)
            ? new InvariantDoubleModelBinder()
            : null;
    }
}
