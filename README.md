# Cat Adoption

Пълностеково приложение за осиновяване на котки, изградено с **ASP.NET Core Web API**, **React + Vite** и **PostgreSQL**.

Проектът поддържа:
- регистрация и вход с **JWT**;
- роли на потребителите: **admin**, **care-giver**, **pet-adopter**;
- публикуване, редакция, изтриване и филтриране на обяви за котки;
- чат между `care-giver` и `pet-adopter`;
- админ панел за управление на потребители и генериране на тестови обяви;
- автоматично seed-ване на начални потребители и котки;
- интеграционни тестове за API-то.

---

## Съдържание

1. [Общ преглед](#общ-преглед)
2. [Архитектура на проекта](#архитектура-на-проекта)
3. [Функционалности](#функционалности)
4. [Роли и права](#роли-и-права)
5. [Модел на данните](#модел-на-данните)
6. [API endpoints](#api-endpoints)
7. [Frontend](#frontend)
8. [Стартиране на проекта](#стартиране-на-проекта)
9. [Конфигурация](#конфигурация)
10. [Тестове](#тестове)
11. [Полезни команди](#полезни-команди)

---

## Общ преглед

Приложението е разделено на три основни части:

- **Backend** – `CatAdoption.Api/`
- **Frontend** – `cat-adoption-web/`
- **Интеграционни тестове** – `CatAdoption.Api.Tests/`

Backend-ът предоставя REST API, а frontend-ът визуализира каталога с котки, формите за вход/регистрация, страницата за създаване и редакция на обяви и админ панела.

---

## Архитектура на проекта

### Backend

- **Framework:** ASP.NET Core 10
- **ORM:** Entity Framework Core
- **База данни:** PostgreSQL
- **Автентикация:** JWT bearer tokens
- **Документация на API:** Swagger / OpenAPI

Основни входни точки:

- `CatAdoption.Api/Program.cs` – конфигурация на услугите, CORS, JWT, Swagger, миграции и seed-ване
- `CatAdoption.Api/Data/ApplicationDbContext.cs` – EF Core DbContext
- `CatAdoption.Api/Controllers/*` – API контролери

### Frontend

- **Framework:** React
- **Build tool:** Vite
- Основен компонент: `cat-adoption-web/src/App.jsx`

Frontend-ът е изграден като един основен SPA компонент с отделни изгледи за:

- login
- register
- списък с котки
- създаване / редакция на обяви
- админ панел
- чат между `care-giver` и `pet-adopter`

Чатът включва:

- преглед на разговори между `care-giver` и `pet-adopter`
- списък с контакти за започване на нов разговор
- badge за непрочетени съобщения в навигацията
- автоматично обновяване на chat summary данните през 30 секунди и при фокус на прозореца

### Тестове

- `CatAdoption.Api.Tests/CatEndpointsTests.cs`
- `CatAdoption.Api.Tests/ChatEndpointsTests.cs`
- Използва `SQLite in-memory` и покрива основните CRUD сценарии за котки и seed-ването от админ панела.
- Chat тестовете покриват разговори, изпращане на съобщения, списък с контакти и mark as read поведение.

---

## Функционалности

### За всички потребители

- преглед на всички котки;
- филтриране по:
  - пол (`sex`)
  - цвят (`color`)
  - статус (`status`)
  - град / локация (`city`)
- пагинация;
- преглед на детайли на конкретна котка;
- виждане на собственика на обявата.

### За логнати потребители

- вход в системата чрез JWT;
- преглед на профила `/users/me`;
- редакция и изтриване на собствените обяви;
- админите могат да редактират и изтриват всяка обява.

### За `care-giver` и `admin`

- създаване на нова обява за котка;
- качване на снимка;
- редакция на статус на обява;
- статусите се нормализират към единен формат.

### За `admin`

- админ панел;
- генериране на 10 нови котки с един бутон;
- преглед на всички потребители;
- промяна на роля на потребителите;
- изтриване на потребители, с изключение на:
  - други админи;
  - собствения акаунт.

---

## Роли и права

В проекта има 3 роли:

- `admin`
- `care-giver`
- `pet-adopter`

### Поведение при регистрация

При регистрация frontend-ът позволява избор между:

- `pet-adopter`
- `care-giver`

Ако е подадена друга стойност, backend-ът я нормализира до `pet-adopter`.

`admin` не се избира при самостоятелна регистрация – той идва от seed-натите данни.

---

## Модел на данните

### `User`

Полетата са:

- `Id`
- `FirstName`
- `LastName`
- `Email`
- `PasswordHash`
- `Role`
- `CreatedAt`
- `City`
- `Cats` – колекция от обяви

### `Cat`

Полетата са:

- `Id`
- `Name`
- `Age`
- `Sex`
- `Color`
- `Description`
- `Location`
- `Status`
- `ImageUrl`
- `UserId`
- `User`

### Статуси на котка

Поддържат се следните стойности:

- `waiting-adoption`
- `in-progress`
- `adopted`

Системата приема и няколко еквивалентни текстови варианта, например:

- `available` → `waiting-adoption`
- `in progress` → `in-progress`
- `in process of adoption` → `in-progress`
- `waiting adoption` → `waiting-adoption`

### Роли

- `admin`
- `care-giver`
- `pet-adopter`

---

## API endpoints

Базовият префикс е `/api`.

### Auth

#### `POST /api/auth/register`

Регистрация на нов потребител.

**Тяло:**
- `firstName`
- `lastName`
- `email`
- `password`
- `role`

**Отговор:**
- message
- userId
- role

#### `POST /api/auth/login`

Вход с email и парола.

**Отговор:**
- message
- token
- userId
- role

### Users

#### `GET /api/users/me`

Връща профила на текущо логнатия потребител.

**Изисква:** JWT токен

### Messages / Chat

#### `GET /api/messages/contacts`

Връща потребителите от противоположната роля, с които текущият потребител може да си пише.

**Изисква:** JWT токен и роля `care-giver` или `pet-adopter`

#### `GET /api/messages/conversations`

Връща списъка с разговорите на текущия потребител.

#### `GET /api/messages/conversations/{conversationId}`

Връща съобщенията в конкретен разговор и маркира непрочетените входящи съобщения като прочетени.

#### `POST /api/messages`

Изпраща ново съобщение към избран потребител от противоположната роля.

#### `PUT /api/messages/{id}/read`

Маркира конкретно съобщение като прочетено.

### Cats

#### `GET /api/cats`

Връща списък с котки.

**Поддържани query параметри:**

- `page`
- `pageSize`
- `sex`
- `color`
- `status`
- `city`

**Отговор:**
- `data`
- `totalCount`
- `page`
- `pageSize`
- `totalPages`

#### `GET /api/cats/{id}`

Връща една конкретна котка.

#### `POST /api/cats`

Създава нова обява.

**Изисква роли:** `admin`, `care-giver`

**Content-Type:** `multipart/form-data`

Поддържа image upload като файл, който backend-ът записва като `data:` URL в `ImageUrl`.

#### `PUT /api/cats/{id}`

Редакция на обява.

**Изисква:**
- собственикът на обявата, или
- `admin`

**Content-Type:** `multipart/form-data`

#### `DELETE /api/cats/{id}`

Изтрива обява.

**Изисква:**
- собственикът на обявата, или
- `admin`

### Admin

#### `POST /api/admin/seed-cats`

Генерира 10 нови котки за текущия админ акаунт.

**Изисква:** `admin`

#### `GET /api/admin/users`

Връща всички потребители.

**Изисква:** `admin`

#### `PUT /api/admin/users/{id}/role`

Променя ролята на потребител.

**Изисква:** `admin`

Позволени роли:
- `pet-adopter`
- `care-giver`

Админ ролята не може да се задава оттук.

#### `DELETE /api/admin/users/{id}`

Изтрива потребител.

**Ограничения:**
- не може да се изтрива админ;
- не може да се изтрива текущият логнат админ;
- при изтриване на потребител се махат и неговите котки чрез EF relationship поведение.

---

## Frontend

Основният React компонент е в `cat-adoption-web/src/App.jsx`.

### Изгледи

- `login`
- `register`
- `cats`
- `newCat`
- `admin`

### Какво прави frontend-ът

- запазва JWT токена и потребителските данни в `localStorage`;
- зарежда текущия потребител чрез `/api/users/me`;
- чете и записва филтрите за котките в URL query string;
- позволява създаване и редакция на обяви чрез `FormData`;
- показва admin панел само на потребители с роля `admin`;
- автоматично избира backend URL според средата.

### API URL логика

Frontend-ът използва следния ред на избор:

1. build-time променлива `__VITE_API_BACKEND__`, ако е налична;
2. при домейн `vercel.app` използва production backend URL;
3. иначе използва локален proxy `/api`.

---

## Стартиране на проекта

## Вариант 1: с Docker Compose

Най-лесният начин е чрез root `Makefile`:

```bash
make start
```

Това стартира:

- `postgres` на порт `5433`
- `api` на порт `5154`
- `frontend` на порт `3000`

### Полезни команди

```bash
make reload
make down
make logs
make logs-api
make logs-postgres
make test
```

### Алтернатива с чист Docker Compose

```bash
docker compose up -d --build
```

Спиране на средата:

```bash
docker compose down --remove-orphans
```

Пълен reset с изчистване на volume-ите:

```bash
docker compose down -v --remove-orphans && docker compose up -d --build
```

---

## Вариант 2: локално стартиране без Docker

### Backend API

```bash
dotnet run --project CatAdoption.Api/CatAdoption.Api.csproj
```

### Frontend

```bash
cd cat-adoption-web
npm install
npm run dev
```

---

## URLs

При Docker стартиране:

- Frontend: `http://localhost:3000`
- API: `http://localhost:5154`
- Swagger: `http://localhost:5154/swagger`

---

## Конфигурация

### Backend

Основни настройки:

- `ConnectionStrings:DefaultConnection`
- `Jwt:Key`
- `Jwt:Issuer`
- `Jwt:Audience`
- `Jwt:ExpiresInMinutes`

### `appsettings.json`

По подразбиране използва:

- PostgreSQL на `localhost:5433`
- база `cat_adoption`
- потребител `catadmin`
- парола `catadmin_pwd`

### Docker Compose

В `docker-compose.yml` backend-ът получава следните environment променливи:

- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__ExpiresInMinutes`

---

## Seed данни

При нормално стартиране API-то:

- изпълнява миграции;
- seed-ва потребители, ако е необходимо;
- seed-ва първоначални котки, ако таблицата `Cats` е празна.

### Demo акаунти

Seed-натите потребители включват:

- `john@example.com` / `password123` – `admin`
- `jane@example.com` / `password456` – `care-giver`
- `alex@example.com` / `password789` – `pet-adopter`

---

## Тестове

Интеграционните тестове използват SQLite in-memory и не изискват Docker PostgreSQL.

Стартиране:

```bash
dotnet test CatAdoption.Api.Tests/CatAdoption.Api.Tests.csproj
```

Тестовете покриват:

- създаване на котка;
- филтриране;
- четене по ID;
- редакция;
- изтриване;
- seed-ване на 10 котки от админ endpoint-а.

---

## Полезни команди

```bash
make start
make logs-api
make logs-postgres
make reload
make down
make test
```

---

## Структура на проекта

```text
Cat-adoption-.Net/
├── CatAdoption.Api/           # ASP.NET Core backend
├── CatAdoption.Api.Tests/     # Интеграционни тестове
├── cat-adoption-web/          # React frontend
├── docker-compose.yml         # Цялата среда с PostgreSQL + API + frontend
├── Makefile                   # Бързи команди за стартиране и тестове
├── README.md                  # Основна документация
└── DEV_COMMANDS.md            # Допълнителни dev команди на български
```

---

## Бележки

- Снимките на котките се пазят като `data:` URL низове, а не като отделни файлове на диска.
- API-то е настроено за CORS при `localhost` и `vercel.app` домейни.
- Swagger е активен и е достъпен директно от API адреса.
- Проектът е подходящ за локална разработка, Docker workflow и демонстрационна среда.

