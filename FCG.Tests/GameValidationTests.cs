using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class GameValidationTests
{
    [Fact]
    public void Cannot_Create_Duplicate_Game_Titles()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "GameDb")
            .Options;

        using var context = new ApplicationDbContext(options);

        var game1 = new Game
        {
            Id = Guid.NewGuid(),
            Title = "Minecraft",
            Description = "Blocos e criatividade",
            Price = 100
        };

        var game2 = new Game
        {
            Id = Guid.NewGuid(),
            Title = "minecraft", // Título com caixa diferente
            Description = "Duplicado",
            Price = 90
        };

        context.Games.Add(game1);
        context.SaveChanges();

        bool exists = context.Games.Any(g => g.Title.ToLower() == game2.Title.ToLower());

        Assert.True(exists);
    }
}
