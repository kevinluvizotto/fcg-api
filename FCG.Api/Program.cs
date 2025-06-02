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

var builder = WebApplication.CreateBuilder(args);

// 💾 Banco de Dados
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")!,
        sql => sql.EnableRetryOnFailure()
    )
);

// 🔐 JWT
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
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
app.UseHttpsRedirection();
app.UseErrorHandling();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", context =>
{
    context.Response.Redirect("/login.html");
    return Task.CompletedTask;
});

// ✅ LOGIN
app.MapPost("/login", async (LoginDto login, ApplicationDbContext db, JwtService jwt) =>
{
    var user = await db.Users.FirstOrDefaultAsync(u => u.Email == login.Email);
    if (user == null || !BCrypt.Net.BCrypt.Verify(login.PasswordHash, user.PasswordHash))
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

    if (!ValidationHelper.IsValidPassword(user.PasswordHash))
        return Results.BadRequest("Senha inválida.");

    user.Id = Guid.NewGuid();
    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/users/{user.Id}", new UserDto { Id = user.Id, Name = user.Name, Email = user.Email, Role = user.Role });
})
.WithTags("Usuários");

app.MapGet("/users", [Authorize(Roles = "Admin")] async (ApplicationDbContext db) =>
{
    var users = await db.Users.Select(u => new UserDto { Id = u.Id, Name = u.Name, Email = u.Email, Role = u.Role }).ToListAsync();
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

app.MapPut("/me/password", [Authorize] async (ClaimsPrincipal user, UpdatePasswordDto pwd, ApplicationDbContext db) =>
{
    var email = user.FindFirst(ClaimTypes.Email)?.Value;
    var dbUser = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
    if (dbUser is null) return Results.NotFound();

    if (!BCrypt.Net.BCrypt.Verify(pwd.CurrentPassword, dbUser.PasswordHash))
        return Results.BadRequest("Senha atual incorreta.");

    if (!ValidationHelper.IsValidPassword(pwd.NewPassword))
        return Results.BadRequest("Nova senha inválida.");

    dbUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(pwd.NewPassword);
    await db.SaveChangesAsync();
    return Results.Ok("Senha atualizada.");
})
.WithTags("Perfil");

app.MapPost("/users/{id}/reset-password", [Authorize(Roles = "Admin")] async (Guid id, ResetPasswordDto dto, ApplicationDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null) return Results.NotFound("Usuário não encontrado.");

    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
    await db.SaveChangesAsync();
    return Results.Ok("Senha redefinida com sucesso.");
});

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
