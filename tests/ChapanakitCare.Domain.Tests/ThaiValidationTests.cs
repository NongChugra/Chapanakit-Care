using ChapanakitCare.Web.Validation;
using Microsoft.AspNetCore.Mvc;

namespace ChapanakitCare.Domain.Tests;

public sealed class ThaiValidationTests
{
    [Fact]
    public void Model_binding_failures_are_reported_in_Thai()
    {
        var options = new MvcOptions();

        ThaiModelBindingMessages.Configure(options.ModelBindingMessageProvider);

        Assert.Equal("ค่า 'abc' ไม่ถูกต้องสำหรับ จำนวน", options.ModelBindingMessageProvider.AttemptedValueIsInvalidAccessor("abc", "จำนวน"));
        Assert.Equal("กรุณากรอกข้อมูลในช่อง วันที่", options.ModelBindingMessageProvider.ValueMustNotBeNullAccessor("วันที่"));
    }
}
