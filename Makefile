COMPOSE := docker compose

.PHONY: start up down reload logs logs-api logs-postgres test

start:
	$(COMPOSE) up -d --build

up:
	$(COMPOSE) up -d --build

down:
	$(COMPOSE) down --remove-orphans

reload:
	$(COMPOSE) down -v --remove-orphans && $(COMPOSE) up -d --build

logs:
	$(COMPOSE) logs -f

logs-api:
	$(COMPOSE) logs -f api

logs-postgres:
	$(COMPOSE) logs -f postgres

test:
	dotnet test CatAdoption.Api.Tests/CatAdoption.Api.Tests.csproj

