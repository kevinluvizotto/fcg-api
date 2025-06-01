using Domain.Entities;
using FCG.Api.Services;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

public class LoginTests
{
    [Fact]
    public void Generate_Jwt_Token_For_Valid_User()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"Jwt:Key", "chave-super-secreta-para-testes-123456"},
            {"Jwt:Issuer", "FCG"},
            {"Jwt:Audience", "FCGUser"}
        };

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();

        var jwtService = new JwtService(configuration);
        var token = jwtService.GenerateToken("teste@example.com", "User");

        Assert.StartsWith("ey", token); // token JWT começa com "ey..."
    }
}
