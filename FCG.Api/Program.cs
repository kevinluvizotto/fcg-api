using BCrypt.Net;
using Domain.Entities;
using FCG.Api.DTOs;
using FCG.Api.Helpers;
using FCG.Api.Services;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 💾 Banco de Dados - Azure SQL ou Local
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure()
    )
);

// 🔐 JWT Authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };
    });

builder.Services.AddAuthorization();

// ✅ Injeção de Dependência para JwtService
builder.Services.AddScoped<JwtService>();

// 📄 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FCG API", Version = "v1" });

    // 🔐 Configuração para JWT no Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT no campo abaixo. Exemplo: **Bearer eyJhbGciOi...**"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 🚀 Dev Tools
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles(); // Habilita carregar index.html automaticamente
app.UseStaticFiles();  // Serve arquivos da pasta wwwroot
app.UseHttpsRedirection();
app.UseAuthentication();  // 🔐 Middleware de autenticação
app.UseAuthorization();   // 🔐 Middleware de autorização

// 🌐 Rotas mínimas

app.MapGet("/", () => "🚀 FCG API está rodando!");

app.MapGet("/users", [Authorize(Roles = "Admin")] async (ApplicationDbContext db) =>
{
    var users = await db.Users
        .Select(u => new UserDto
        {
            Id = u.Id,
            Name = u.Name,
            Email = u.Email,
            Role = u.Role
        })
        .ToListAsync();

    return Results.Ok(users);
});

app.MapGet("/games", async (ApplicationDbContext db) =>
{
    var games = await db.Games.ToListAsync();
    return Results.Ok(games);
});

app.MapGet("/me/games", [Authorize(Roles = "User,Admin")] async (ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    if (email is null) return Results.Unauthorized();

    var dbUser = await db.Users
        .Include(u => u.Games)
        .FirstOrDefaultAsync(u => u.Email == email);

    if (dbUser is null)
        return Results.NotFound("Usuário não encontrado.");

    var games = dbUser.Games.Select(g => new
    {
        g.Id,
        g.Title,
        g.Description,
        g.Price
    });

    return Results.Ok(games);
});

app.MapPost("/users", async (User user, ApplicationDbContext db) =>
{
    if (await db.Users.AnyAsync(u => u.Email == user.Email))
    {
        return Results.BadRequest("Este e-mail já está em uso.");
    }

    // ✅ Validação de senha forte
    if (!ValidationHelper.IsValidPassword(user.PasswordHash))
    {
        return Results.BadRequest("A senha deve conter no mínimo 8 caracteres, com letras, números e caractere especial.");
    }

    // Aplica hash com BCrypt
    var hashedPassword = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
    user.PasswordHash = hashedPassword;

    user.Id = Guid.NewGuid();
    db.Users.Add(user);
    await db.SaveChangesAsync();

    var userDto = new UserDto
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role
    };

    return Results.Created($"/users/{user.Id}", userDto);

});

// 🔐 Endpoint de login com JWT
app.MapPost("/login", async (LoginDto login, ApplicationDbContext db, JwtService jwt) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == login.Email);

    if (user is null || !BCrypt.Net.BCrypt.Verify(login.PasswordHash, user.PasswordHash))
    {
        return Results.Unauthorized();
    }

    var token = jwt.GenerateToken(user.Email, user.Role);
    return Results.Ok(new { token });
});

app.MapGet("/me", [Authorize] async (ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;

    if (email is null)
        return Results.Unauthorized();

    var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

    if (dbUser is null)
        return Results.NotFound();

    var userDto = new UserDto
    {
        Id = dbUser.Id,
        Name = dbUser.Name,
        Email = dbUser.Email,
        Role = dbUser.Role
    };

    return Results.Ok(userDto);
});

app.MapPut("/me", [Authorize] async (
    ClaimsPrincipal user,
    UpdateUserDto update,
    ApplicationDbContext db) =>
{
    var currentEmail = user.FindFirst(ClaimTypes.Email)?.Value;
    if (string.IsNullOrWhiteSpace(currentEmail))
        return Results.Unauthorized();

    var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == currentEmail);
    if (dbUser is null)
        return Results.NotFound();

    // ✅ Verifica se o novo e-mail já está em uso por outro usuário
    if (update.Email != dbUser.Email &&
        await db.Users.AnyAsync(u => u.Email == update.Email))
    {
        return Results.BadRequest("Este e-mail já está em uso por outro usuário.");
    }

    dbUser.Name = update.Name;
    dbUser.Email = update.Email;

    await db.SaveChangesAsync();

    var userDto = new UserDto
    {
        Id = dbUser.Id,
        Name = dbUser.Name,
        Email = dbUser.Email,
        Role = dbUser.Role
    };

    return Results.Ok(userDto);
});

app.MapPut("/me/password", [Authorize] async (
    ClaimsPrincipal user,
    UpdatePasswordDto pwd,
    ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    if (email is null) return Results.Unauthorized();

    var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null) return Results.NotFound();

    if (!BCrypt.Net.BCrypt.Verify(pwd.CurrentPassword, dbUser.PasswordHash))
        return Results.BadRequest("Senha atual incorreta.");

    if (!ValidationHelper.IsValidPassword(pwd.NewPassword))
        return Results.BadRequest("A nova senha não atende aos critérios.");

    dbUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(pwd.NewPassword);
    await db.SaveChangesAsync();

    return Results.Ok("Senha atualizada com sucesso.");
});

app.MapPut("/users/{id}", [Authorize(Roles = "Admin")] async (Guid id, UserUpdateDto input, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user == null) return Results.NotFound("Usuário não encontrado.");

    user.Name = input.Name;
    user.Email = input.Email;
    user.Role = input.Role;

    await db.SaveChangesAsync();
    return Results.Ok("Usuário atualizado com sucesso.");
});

app.MapPost("/games", [Authorize(Roles = "Admin")] async (Game game, ApplicationDbContext db) =>
{
    var exists = await db.Games.AnyAsync(g => g.Title.ToLower() == game.Title.ToLower());
    if (exists)
        return Results.BadRequest("Já existe um jogo com este título.");

    game.Id = Guid.NewGuid();
    db.Games.Add(game);
    await db.SaveChangesAsync();
    return Results.Created($"/games/{game.Id}", game);
});

app.MapPost("/me/games", [Authorize(Roles = "User,Admin")] async (HttpContext http, ApplicationDbContext db) =>
{
    var userEmail = http.User.FindFirst(ClaimTypes.Email)?.Value;
    if (userEmail is null) return Results.Unauthorized();

    var user = await db.Users.Include(u => u.Games).FirstOrDefaultAsync(u => u.Email == userEmail);
    if (user is null) return Results.NotFound("Usuário não encontrado.");

    var gameId = http.Request.Query["gameId"];
    if (string.IsNullOrWhiteSpace(gameId)) return Results.BadRequest("ID do jogo é obrigatório.");

    if (!Guid.TryParse(gameId, out var gameGuid))
        return Results.BadRequest("ID do jogo inválido.");

    var game = await db.Games.FindAsync(gameGuid);

    if (game is null) return Results.NotFound("Jogo não encontrado.");

    if (user.Games.Any(g => g.Id == game.Id))
        return Results.BadRequest("Este jogo já foi adquirido.");

    user.Games.Add(game);
    await db.SaveChangesAsync();

    return Results.Ok("Jogo adquirido com sucesso.");
});

app.MapDelete("/users/{id}", [Authorize(Roles = "Admin")] async (Guid id, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null)
        return Results.NotFound("Usuário não encontrado.");

    db.Users.Remove(user);
    await db.SaveChangesAsync();

    return Results.Ok("Usuário removido com sucesso.");
});

app.MapDelete("/games/{id}", [Authorize(Roles = "Admin")] async (Guid id, ApplicationDbContext db) =>
{
    var game = await db.Games.FindAsync(id);
    if (game == null) return Results.NotFound("Jogo não encontrado.");

    db.Games.Remove(game);
    await db.SaveChangesAsync();

    return Results.Ok("Jogo excluído com sucesso.");
});

app.MapDelete("/me/games/{gameId}", [Authorize(Roles = "User,Admin")] async (ClaimsPrincipal user, string gameId, ApplicationDbContext db) =>
{
    if (!Guid.TryParse(gameId, out var gameGuid))
        return Results.BadRequest("ID do jogo inválido.");

    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    if (email is null)
        return Results.Unauthorized();

    var dbUser = await db.Users.Include(u => u.Games).FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null)
        return Results.NotFound("Usuário não encontrado.");

    var game = dbUser.Games.FirstOrDefault(g => g.Id == gameGuid);
    if (game is null)
        return Results.NotFound("Esse jogo não está na sua biblioteca.");

    dbUser.Games.Remove(game);
    await db.SaveChangesAsync();

    return Results.Ok("Jogo removido da sua biblioteca.");
});

app.Run();
