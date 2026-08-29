using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace ChapanakitCare.Web.Validation;

public static class ThaiModelBindingMessages
{
    public static void Configure(DefaultModelBindingMessageProvider messages)
    {
        messages.SetAttemptedValueIsInvalidAccessor((value, field) => $"ค่า '{value}' ไม่ถูกต้องสำหรับ {field}");
        messages.SetNonPropertyAttemptedValueIsInvalidAccessor(value => $"ค่า '{value}' ไม่ถูกต้อง");
        messages.SetValueMustNotBeNullAccessor(field => $"กรุณากรอกข้อมูลในช่อง {field}");
        messages.SetMissingBindRequiredValueAccessor(field => $"กรุณากรอกข้อมูลในช่อง {field}");
        messages.SetValueIsInvalidAccessor(value => $"ค่า '{value}' ไม่ถูกต้อง");
        messages.SetUnknownValueIsInvalidAccessor(field => $"ข้อมูลในช่อง {field} ไม่ถูกต้อง");
        messages.SetNonPropertyUnknownValueIsInvalidAccessor(() => "ข้อมูลที่กรอกไม่ถูกต้อง");
    }
}
