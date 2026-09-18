# Cat Adoption

Full-stack cat adoption app with an ASP.NET Core API, React frontend, and PostgreSQL.

## Quick start with Docker

Use the root `Makefile`:

```bash
make start
```

This starts:
- `postgres` on port `5433`
- `api` on port `5154`
- `frontend` on port `3000`

Useful shortcuts:

```bash
make reload
make down
make logs
make logs-api
make logs-postgres
make test
```

## Docker Compose

If you prefer raw Docker commands:

```bash
docker compose up -d --build
```

Stop the stack:

```bash
docker compose down --remove-orphans
```

Full reset, including database volumes:

```bash
docker compose down -v --remove-orphans && docker compose up -d --build
```

## Local development

### API only

```bash
dotnet run --project CatAdoption.Api/CatAdoption.Api.csproj
```

### Frontend only

```bash
cd cat-adoption-web
npm install
npm run dev
```

## URLs

- Frontend: `http://localhost:3000`
- API: `http://localhost:5154`
- Swagger: `http://localhost:5154/swagger`

## Tests

Run the integration tests:

```bash
dotnet test CatAdoption.Api.Tests/CatAdoption.Api.Tests.csproj
```

The test suite uses SQLite in-memory and does not require the Docker database.

