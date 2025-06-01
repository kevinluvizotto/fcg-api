using FCG.Api.Helpers;
using Xunit;

namespace FCG.Tests
{
    public class PasswordValidationTests
    {
        [Theory]
        [InlineData("Senha@123", true)]
        [InlineData("12345678", false)]  // Sem letras ou símbolos
        [InlineData("senha123", false)]  // Sem símbolo
        [InlineData("Senha@", false)]    // Sem número
        [InlineData("S@1", false)]       // Muito curta
        [InlineData("Senha123!", true)]  // OK
        public void IsValidPassword_ShouldValidateCorrectly(string password, bool expected)
        {
            var result = ValidationHelper.IsValidPassword(password);
            Assert.Equal(expected, result);
        }
    }
}
