# FIAP Cloud Games (FCG) API

Uma API RESTful desenvolvida em **.NET 8** para gerenciar usu�rios e seus jogos adquiridos, como parte do desafio do **Tech Challenge FIAP**. O sistema oferece autentica��o via JWT, autoriza��o baseada em perfis (User/Admin), e conta com um portal web simples constru�do com HTML/JavaScript puro.

---

## Objetivos

- Criar um backend seguro e escal�vel usando .NET 8 e Entity Framework Core
- Implementar funcionalidades essenciais para gerenciamento de usu�rios e jogos
- Aplicar boas pr�ticas de arquitetura como DDD, TDD e Clean Code
- Permitir aquisi��o de jogos por usu�rios autenticados
- Gerenciar usu�rios e jogos com permiss�es administrativas
- Expor a aplica��o em ambiente cloud (Azure App Service)

---

## Tecnologias Utilizadas

- ASP.NET Core 8 (Minimal APIs)
- Entity Framework Core
- SQL Server (Azure SQL Database)
- JWT (JSON Web Token)
- xUnit (Testes Unit�rios)
- GitHub Actions (CI/CD para Azure)
- Azure App Service (Publica��o)
- HTML + JavaScript (Frontend simples)

---

## Funcionalidades

### Usu�rios

| A��o                     | M�todo | Endpoint          | Autoriza��o  |
|--------------------------|--------|-------------------|--------------|
| Criar usu�rio            | POST   | `/users`          | P�blica      |
| Login e obter JWT        | POST   | `/login`          | P�blica      |
| Consultar perfil         | GET    | `/me`             | Autenticado  |
| Atualizar perfil         | PUT    | `/me`             | Autenticado  |
| Alterar senha            | PUT    | `/me/password`    | Autenticado  |
| Listar todos usu�rios    | GET    | `/users`          | Admin        |
| Atualizar usu�rio (Admin)| PUT    | `/users/{id}`     | Admin        |
| Excluir usu�rio          | DELETE | `/users/{id}`     | Admin        |

### Jogos

| A��o                      | M�todo | Endpoint            | Autoriza��o        |
|---------------------------|--------|---------------------|--------------------|
| Listar jogos dispon�veis  | GET    | `/games`            | P�blica            |
| Criar novo jogo           | POST   | `/games`            | Admin              |
| Excluir jogo              | DELETE | `/games/{id}`       | Admin              |
| Adquirir jogo             | POST   | `/me/games?gameId=` | User ou Admin      |
| Listar meus jogos         | GET    | `/me/games`         | User ou Admin      |
| Remover jogo da conta     | DELETE | `/me/games/{id}`    | User ou Admin      |

---

## Estrutura do Projeto

```
/FCG
|
|--- Domain/               # Entidades de dom�nio (User, Game)
|--- Infrastructure/       # Data Context (EF Core)
|--- Application/          # DTOs, helpers
|--- FCG.Api/              # API principal (.NET 8 minimal APIs)
|   |--- wwwroot/          # Frontend HTML/JS puro
|   |--- Program.cs        # Endpoints e configura��o
|   |--- Services/         # Servi�os (JWT, etc.)
|   |--- appsettings.json  # Configura��es (JWT, DB)
|--- FCG.Tests/            # Projeto de testes unit�rios com xUnit
|--- .github/workflows/    # CI/CD para Azure (GitHub Actions)
```

---

## Rodando os Testes

Execute o comando abaixo na raiz do projeto:

```bash
dotnet test
```

---

## Rodando localmente

### Pr�-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [SQL Server ou Azure SQL Database]
- Visual Studio Code ou Visual Studio

### 1. Clone o reposit�rio

```bash
git clone https://github.com/kevinluvizotto/fcg-api.git
cd fcg-api
```

### 2. Configure o banco de dados

Atualize a string de conex�o no `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=...;Database=...;User Id=...;Password=...;"
}
```

### 3. Rode as migra��es (opcional)

```bash
dotnet ef database update
```

### 4. Execute a API

```bash
dotnet run --project FCG.Api
```

Acesse: `https://localhost:5001`

---

## Deploy (Azure App Service)

O projeto est� configurado para CI/CD via **GitHub Actions**. A cada push na branch `main`, a API � automaticamente publicada em:

[`https://fcg-api-c0guh9d6aqe3bkab.brazilsouth-01.azurewebsites.net`](https://fcg-api-c0guh9d6aqe3bkab.brazilsouth-01.azurewebsites.net)

---

## Autor

**Kevin Luvizotto**  
Principal Automation Developer  
P�s-gradua��o em Arquitetura de Software (.NET) - FIAP  
[linkedin.com/in/kevin-luvizotto](https://linkedin.com/in/kevin-luvizotto)

---

## Licen�a

Este projeto � acad�mico e n�o possui fins comerciais.
