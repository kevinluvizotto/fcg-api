using FCG.Api.Helpers;
using Xunit;

namespace FCG.Tests
{
    public class PasswordValidationTests
    {
        [Theory]
        [InlineData("Senha@123", true)]
        [InlineData("12345678", false)]  // Sem letras ou s�mbolos
        [InlineData("senha123", false)]  // Sem s�mbolo
        [InlineData("Senha@", false)]    // Sem n�mero
        [InlineData("S@1", false)]       // Muito curta
        [InlineData("Senha123!", true)]  // OK
        public void IsValidPassword_ShouldValidateCorrectly(string password, bool expected)
        {
            var result = ValidationHelper.IsValidPassword(password);
            Assert.Equal(expected, result);
        }
    }
}
