using System.Text.RegularExpressions;

namespace FCG.Api.Helpers
{
    public static class ValidationHelper
    {
        public static bool IsValidPassword(string password)
        {
            // Requisitos: mínimo 8 caracteres, 1 letra, 1 número, 1 caractere especial
            var regex = new Regex(@"^(?=.*[A-Za-z])(?=.*\d)(?=.*[^A-Za-z\d]).{8,}$");
            return regex.IsMatch(password);
        }
    }
}
