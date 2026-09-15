# Cat Adoption API

## Run locally

1. Start PostgreSQL.
2. Update `CatAdoption.Api/appsettings.json` if needed.
3. Run:

```bash
dotnet run --project CatAdoption.Api/CatAdoption.Api.csproj
```

## Run with Docker

Start the API and main database:

```bash
docker compose up -d
```

API URL:

```text
http://localhost:5154
```

Swagger:

```text
http://localhost:5154/swagger
```

## Run tests

Start the test database:

```bash
docker compose -f docker-compose.test.yml up -d
```

Run the tests:

```bash
dotnet test CatAdoption.Api.Tests/CatAdoption.Api.Tests.csproj
```
