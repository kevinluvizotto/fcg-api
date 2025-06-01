using Xunit;
using FCG.Api.Helpers;

public class PasswordValidationTests
{
    [Theory]
    [InlineData("Abc123!@#", true)]       // válido
    [InlineData("Abc123!", false)]        // faltando caractere especial
    [InlineData("12345678!", false)]      // faltando letras
    [InlineData("abcdefgh!", false)]      // faltando números
    [InlineData("ABC123!@#", true)]       // válido
    [InlineData("abc123", false)]         // fraco
    [InlineData("12345678", false)]       // apenas números
    public void TestPasswordStrength(string password, bool expected)
    {
        var result = ValidationHelper.IsValidPassword(password);
        Assert.Equal(expected, result);
    }
}
