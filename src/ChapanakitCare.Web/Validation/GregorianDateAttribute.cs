using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ChapanakitCare.Web.Validation;

// Visible controls use Buddhist Era. Transport fields always use Gregorian ISO,
// independent of the request culture and whether a validation error redisplays them.
public sealed class GregorianDateAttribute : ModelBinderAttribute
{
    public GregorianDateAttribute() : base(typeof(GregorianDateBinder)) { }
}

public sealed class GregorianDateBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName, value);
        if (string.IsNullOrWhiteSpace(value.FirstValue) && context.ModelMetadata.IsNullableValueType)
            context.Result = ModelBindingResult.Success(null);
        else if (DateOnly.TryParseExact(value.FirstValue, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date))
            context.Result = ModelBindingResult.Success(date);
        else
            context.ModelState.TryAddModelError(context.ModelName, "วันที่ไม่ถูกต้อง กรุณากรอกวันที่ พ.ศ. ให้ครบถ้วน");
        return Task.CompletedTask;
    }
}
