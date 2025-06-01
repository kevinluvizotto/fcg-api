# FIAP Cloud Games (FCG) API

Uma API RESTful desenvolvida em **.NET 8** para gerenciar usuários e seus jogos adquiridos, como parte do desafio do **Tech Challenge FIAP**. O sistema oferece autenticação via JWT, autorização baseada em perfis (User/Admin), e conta com um portal web simples construído com HTML/JavaScript puro.

---

## Objetivos

- Criar um backend seguro e escalável usando .NET 8 e Entity Framework Core
- Implementar funcionalidades essenciais para gerenciamento de usuários e jogos
- Aplicar boas práticas de arquitetura como DDD, TDD e Clean Code
- Permitir aquisição de jogos por usuários autenticados
- Gerenciar usuários e jogos com permissões administrativas
- Expor a aplicação em ambiente cloud (Azure App Service)

---

## Tecnologias Utilizadas

- ASP.NET Core 8 (Minimal APIs)
- Entity Framework Core
- SQL Server (Azure SQL Database)
- JWT (JSON Web Token)
- xUnit (Testes Unitários)
- GitHub Actions (CI/CD para Azure)
- Azure App Service (Publicação)
- HTML + JavaScript (Frontend simples)

---

## Funcionalidades

### Usuários

| Ação                     | Método | Endpoint          | Autorização  |
|--------------------------|--------|-------------------|--------------|
| Criar usuário            | POST   | `/users`          | Pública      |
| Login e obter JWT        | POST   | `/login`          | Pública      |
| Consultar perfil         | GET    | `/me`             | Autenticado  |
| Atualizar perfil         | PUT    | `/me`             | Autenticado  |
| Alterar senha            | PUT    | `/me/password`    | Autenticado  |
| Listar todos usuários    | GET    | `/users`          | Admin        |
| Atualizar usuário (Admin)| PUT    | `/users/{id}`     | Admin        |
| Excluir usuário          | DELETE | `/users/{id}`     | Admin        |

### Jogos

| Ação                      | Método | Endpoint            | Autorização        |
|---------------------------|--------|---------------------|--------------------|
| Listar jogos disponíveis  | GET    | `/games`            | Pública            |
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
|--- Domain/               # Entidades de domínio (User, Game)
|--- Infrastructure/       # Data Context (EF Core)
|--- Application/          # DTOs, helpers
|--- FCG.Api/              # API principal (.NET 8 minimal APIs)
|   |--- wwwroot/          # Frontend HTML/JS puro
|   |--- Program.cs        # Endpoints e configuração
|   |--- Services/         # Serviços (JWT, etc.)
|   |--- appsettings.json  # Configurações (JWT, DB)
|--- FCG.Tests/            # Projeto de testes unitários com xUnit
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

### Pré-requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download)
- [SQL Server ou Azure SQL Database]
- Visual Studio Code ou Visual Studio

### 1. Clone o repositório

```bash
git clone https://github.com/kevinluvizotto/fcg-api.git
cd fcg-api
```

### 2. Configure o banco de dados

Atualize a string de conexão no `appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=...;Database=...;User Id=...;Password=...;"
}
```

### 3. Rode as migrações (opcional)

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

O projeto está configurado para CI/CD via **GitHub Actions**. A cada push na branch `main`, a API é automaticamente publicada em:

[`https://fcg-api-c0guh9d6aqe3bkab.brazilsouth-01.azurewebsites.net`](https://fcg-api-c0guh9d6aqe3bkab.brazilsouth-01.azurewebsites.net)

---

## Autor

**Kevin Luvizotto**  
Principal Automation Developer  
Pós-graduação em Arquitetura de Software (.NET) - FIAP  
[linkedin.com/in/kevin-luvizotto](https://linkedin.com/in/kevin-luvizotto)

---

## Licença

Este projeto é acadêmico e não possui fins comerciais.
