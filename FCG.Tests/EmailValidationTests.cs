using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

public class EmailValidationTests
{
    [Fact]
    public void Cannot_Register_User_With_Duplicate_Email()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: "UserDb")
            .Options;

        using var context = new ApplicationDbContext(options);

        var user1 = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice",
            Email = "alice@example.com",
            PasswordHash = "123456",
            Role = "User"
        };

        context.Users.Add(user1);
        context.SaveChanges();

        var user2 = new User
        {
            Id = Guid.NewGuid(),
            Name = "Alice 2",
            Email = "alice@example.com", // Mesmo email
            PasswordHash = "654321",
            Role = "User"
        };

        bool exists = context.Users.Any(u => u.Email == user2.Email);

        Assert.True(exists);
    }
}
