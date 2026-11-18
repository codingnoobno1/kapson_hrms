using Vispl.HRManagement.VO.Employee;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// Employee phone numbers are unique per company, so every way of typing the same number must
/// be stored the same way or the unique index would let a duplicate through.
/// </summary>
public sealed class EmployeePhoneTests
{
    [Theory]
    [InlineData("9876543210")]
    [InlineData("98765 43210")]
    [InlineData("098765-43210")]
    [InlineData("+91 98765 43210")]
    [InlineData("919876543210")]
    [InlineData("0091 (98765) 43210")]
    [InlineData("+919876543210")]
    public void Same_number_typed_differently_is_stored_once(string typed) =>
        Assert.Equal("+919876543210", EmployeePhone.Normalize(typed));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_is_no_number(string? typed) => Assert.Null(EmployeePhone.Normalize(typed));

    [Theory]
    [InlineData("12345")]
    [InlineData("98765abc10")]
    [InlineData("+1 234")]
    public void Not_a_phone_number_is_rejected(string typed) =>
        Assert.Throws<FormatException>(() => EmployeePhone.Normalize(typed));
}
