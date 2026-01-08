using BCrypt.Net;
using Domain.Entities;
using FCG.Api.DTOs;
using FCG.Api.Helpers;
using FCG.Api.Middlewares;
using FCG.Api.Services;
using Infrastructure.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text;

static bool LooksLikeBcrypt(string? hash)
{
    if (string.IsNullOrWhiteSpace(hash)) return false;
    return hash.StartsWith("$2a$") || hash.StartsWith("$2b$") || hash.StartsWith("$2y$");
}

var builder = WebApplication.CreateBuilder(args);

// ✅ Não force porta 80 no DEV (deixe o launchSettings mandar)
// Em produção/container, tente respeitar PORT/WEBSITES_PORT
if (!builder.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("WEBSITES_PORT")
              ?? Environment.GetEnvironmentVariable("PORT");

    if (!string.IsNullOrWhiteSpace(port))
        builder.WebHost.UseUrls($"http://*:{port}");
}

// 💾 Banco de Dados
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        sql => sql.EnableRetryOnFailure()
    )
);

// 🔐 JWT
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Config Jwt:Issuer não encontrada.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Config Jwt:Audience não encontrada.");
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Config Jwt:Key não encontrada.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<JwtService>();

// 📄 Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "FCG API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header usando Bearer.\n\nExemplo: Bearer {seu_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

// 🌐 Middlewares
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "FCG API v1");
    c.RoutePrefix = "swagger";
});

app.UseDefaultFiles();
app.UseStaticFiles();

// ✅ Evita warning/redirect quebrado no DEV quando rodando em HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseErrorHandling();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", context =>
{
    context.Response.Redirect("/login.html");
    return Task.CompletedTask;
});

// ✅ LOGIN (aceita senha em texto simples; migra legado para bcrypt)
app.MapPost("/login", async (LoginDto login, ApplicationDbContext db, JwtService jwt, ILoggerFactory loggerFactory) =>
{
    var logger = loggerFactory.CreateLogger("Login");

    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == login.Email);
    if (user is null)
        return Results.Unauthorized();

    var stored = user.PasswordHash ?? string.Empty;

    // Aqui, login.PasswordHash = senha digitada (texto puro)
    var inputPassword = login.PasswordHash;

    bool ok;

    if (LooksLikeBcrypt(stored))
    {
        // caminho normal
        ok = BCrypt.Net.BCrypt.Verify(inputPassword, stored);
    }
    else
    {
        // legado (texto puro no DB)
        ok = inputPassword == stored;

        // migra para bcrypt na primeira autenticação bem sucedida
        if (ok)
        {
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(inputPassword);
            await db.SaveChangesAsync();
            logger.LogInformation("Usuário {Email} migrou senha de texto puro para BCrypt.", user.Email);
        }
    }

    if (!ok)
        return Results.Unauthorized();

    var token = jwt.GenerateToken(user.Email, user.Role);
    return Results.Ok(new { token });
})
.WithTags("Autenticação");

// ✅ USERS
app.MapPost("/users", async (User user, ApplicationDbContext db) =>
{
    if (await db.Users.AnyAsync(u => u.Email == user.Email))
        return Results.BadRequest("E-mail já em uso.");

    // user.PasswordHash = senha em texto puro (nome do campo legado)
    if (!ValidationHelper.IsValidPassword(user.PasswordHash))
        return Results.BadRequest("Senha inválida.");

    user.Id = Guid.NewGuid();

    // salva sempre como bcrypt
    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);

    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}",
        new UserDto { Id = user.Id, Name = user.Name, Email = user.Email, Role = user.Role });
})
.WithTags("Usuários");

app.MapGet("/users", [Authorize(Roles = "Admin")] async (ApplicationDbContext db) =>
{
    var users = await db.Users
        .Select(u => new UserDto { Id = u.Id, Name = u.Name, Email = u.Email, Role = u.Role })
        .ToListAsync();

    return Results.Ok(users);
})
.WithTags("Usuários");

app.MapPut("/users/{id}", [Authorize(Roles = "Admin")] async (Guid id, UserUpdateDto input, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound("Usuário não encontrado.");

    user.Name = input.Name;
    user.Email = input.Email;
    user.Role = input.Role;
    await db.SaveChangesAsync();

    return Results.Ok("Usuário atualizado com sucesso.");
})
.WithTags("Usuários");

app.MapDelete("/users/{id}", [Authorize(Roles = "Admin")] async (Guid id, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound("Usuário não encontrado.");

    db.Users.Remove(user);
    await db.SaveChangesAsync();

    return Results.Ok("Usuário removido.");
})
.WithTags("Usuários");

app.MapGet("/me", [Authorize] async (ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null) return Results.NotFound();

    return Results.Ok(new UserDto { Id = dbUser.Id, Name = dbUser.Name, Email = dbUser.Email, Role = dbUser.Role });
})
.WithTags("Perfil");

app.MapPut("/me", [Authorize] async (ClaimsPrincipal user, UpdateUserDto input, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null) return Results.NotFound();

    if (input.Email != dbUser.Email && await db.Users.AnyAsync(u => u.Email == input.Email))
        return Results.BadRequest("E-mail já em uso por outro usuário.");

    dbUser.Name = input.Name;
    dbUser.Email = input.Email;
    await db.SaveChangesAsync();

    return Results.Ok();
})
.WithTags("Perfil");

// ✅ /me/password com suporte legado (texto puro) + bcrypt
app.MapPut("/me/password", [Authorize] async (ClaimsPrincipal user, UpdatePasswordDto pwd, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null) return Results.NotFound();

    var stored = dbUser.PasswordHash ?? string.Empty;

    bool currentOk = LooksLikeBcrypt(stored)
        ? BCrypt.Net.BCrypt.Verify(pwd.CurrentPassword, stored)
        : pwd.CurrentPassword == stored;

    if (!currentOk)
        return Results.BadRequest("Senha atual incorreta.");

    if (!ValidationHelper.IsValidPassword(pwd.NewPassword))
        return Results.BadRequest("Senha inválida. Ela deve conter no mínimo 8 caracteres, incluindo letras, números e caracteres especiais.");

    dbUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(pwd.NewPassword);
    await db.SaveChangesAsync();

    return Results.Ok("Senha atualizada.");
})
.WithTags("Perfil");

app.MapPost("/users/{id}/reset-password", [Authorize(Roles = "Admin")] async (Guid id, ResetPasswordDto dto, ApplicationDbContext db) =>
{
    if (!ValidationHelper.IsValidPassword(dto.NewPassword))
        return Results.BadRequest("A senha deve ter no mínimo 8 caracteres, incluindo letras maiúsculas, minúsculas, números e caracteres especiais.");

    var user = await db.Users.FindAsync(id);
    if (user is null)
        return Results.NotFound("Usuário não encontrado.");

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
    await db.SaveChangesAsync();

    return Results.Ok("Senha redefinida com sucesso.");
})
.WithTags("Usuários");

// ✅ GAMES
app.MapGet("/games", async (ApplicationDbContext db) =>
{
    var games = await db.Games.ToListAsync();
    return Results.Ok(games);
})
.WithTags("Jogos");

app.MapPost("/games", [Authorize(Roles = "Admin")] async (Game game, ApplicationDbContext db) =>
{
    if (await db.Games.AnyAsync(g => g.Title.ToLower() == game.Title.ToLower()))
        return Results.BadRequest("Jogo já cadastrado.");

    game.Id = Guid.NewGuid();
    db.Games.Add(game);
    await db.SaveChangesAsync();

    return Results.Created($"/games/{game.Id}", game);
})
.WithTags("Jogos");

app.MapDelete("/games/{id}", [Authorize(Roles = "Admin")] async (Guid id, ApplicationDbContext db) =>
{
    var game = await db.Games.FindAsync(id);
    if (game is null) return Results.NotFound("Jogo não encontrado.");

    db.Games.Remove(game);
    await db.SaveChangesAsync();

    return Results.Ok("Jogo excluído.");
})
.WithTags("Jogos");

app.MapPut("/games/{id}", [Authorize(Roles = "Admin")] async (Guid id, Game updatedGame, ApplicationDbContext db) =>
{
    var game = await db.Games.FindAsync(id);
    if (game is null) return Results.NotFound("Jogo não encontrado.");

    if (!string.Equals(game.Title, updatedGame.Title, StringComparison.OrdinalIgnoreCase)
        && await db.Games.AnyAsync(g => g.Title.ToLower() == updatedGame.Title.ToLower()))
        return Results.BadRequest("Outro jogo com esse título já existe.");

    game.Title = updatedGame.Title;
    game.Description = updatedGame.Description;
    game.Price = updatedGame.Price;

    await db.SaveChangesAsync();

    return Results.Ok("Jogo atualizado com sucesso.");
})
.WithTags("Jogos");

// ✅ Biblioteca do Usuário
app.MapGet("/me/games", [Authorize] async (ClaimsPrincipal user, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    var dbUser = await db.Users.Include(u => u.Games).FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null) return Results.NotFound();

    return Results.Ok(dbUser.Games.Select(g => new { g.Id, g.Title, g.Description, g.Price }));
})
.WithTags("Biblioteca");

app.MapPost("/me/games", [Authorize] async (HttpContext http, ApplicationDbContext db) =>
{
    var email = http.User.FindFirst(ClaimTypes.Email)?.Value;
    var user = await db.Users.Include(u => u.Games).FirstOrDefaultAsync(u => u.Email == email);
    var gameId = http.Request.Query["gameId"];

    if (!Guid.TryParse(gameId, out var gameGuid))
        return Results.BadRequest("ID inválido.");

    var game = await db.Games.FindAsync(gameGuid);
    if (user is null || game is null) return Results.NotFound();

    if (user.Games.Any(g => g.Id == game.Id))
        return Results.BadRequest("Jogo já adquirido.");

    user.Games.Add(game);
    await db.SaveChangesAsync();

    return Results.Ok("Jogo adquirido.");
})
.WithTags("Biblioteca");

app.MapDelete("/me/games/{gameId}", [Authorize] async (ClaimsPrincipal user, Guid gameId, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    var dbUser = await db.Users.Include(u => u.Games).FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null) return Results.NotFound();

    var game = dbUser.Games.FirstOrDefault(g => g.Id == gameId);
    if (game is null) return Results.NotFound("Jogo não está na biblioteca.");

    dbUser.Games.Remove(game);
    await db.SaveChangesAsync();

    return Results.Ok("Jogo removido.");
})
.WithTags("Biblioteca");

// ✅ Teste de Erro
app.MapGet("/error-test", (HttpContext _) =>
{
    throw new Exception("Erro proposital para teste de middleware global.");
})
.WithTags("Debug");

app.Run();
