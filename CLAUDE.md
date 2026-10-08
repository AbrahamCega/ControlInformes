# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ASP.NET Core 8 Web API (`ControlInformes`) for managing congregation publisher reports
(*informes*), attendance (*asistencia*), groups, and monthly summaries. Generates PDF
publisher cards (iText7) and imports/exports Excel data (ClosedXML). Code, entities, and
domain language are in Spanish — match that convention.

## Documento de referencia obligatorio

`DOCUMENTACION-API.md` (raiz del repo) documenta **todos los endpoints, enums, reglas de
negocio y validaciones** vigentes. Antes de hacer cualquier cambio, leelo y usalo como
referencia. Si un cambio agrega, modifica o elimina un endpoint, un enum, una validacion o
una regla de negocio, **actualiza `DOCUMENTACION-API.md` en el mismo cambio** (incluida la
fecha de "Ultima actualizacion" de su cabecera).

## Commands

Run all commands from the repo root. Build/run target the solution; `dotnet` operations
that touch projects must reference the correct `.csproj` (see the layering caveat below).

```bash
# Build the whole solution
dotnet build ControlInformes.sln

# Run the API (Swagger UI served at /swagger in Development)
dotnet run --project ControlInformes.API

# EF Core migrations — startup project is the API, migrations live in the Data project
dotnet ef migrations add <Name> --project ControlInformes.Data --startup-project ControlInformes.API
dotnet ef database update --project ControlInformes.Data --startup-project ControlInformes.API
```

Migrations are also applied automatically at startup via `db.Database.Migrate()` in
`Program.cs`, so running the API against a reachable SQL Server will create/upgrade the schema.

There is **no test project** in the solution — there is no test command to run.

## Architecture

The solution is a classic layered (N-tier) architecture, **not** Clean Architecture/CQRS.
Dependency flow is one-directional:

```
API  →  Business (Bus*)  →  Data (Dat*)  →  Domain (entities)
  ↘──────────────┴──────────────┴──────────→  Utils (shared)
```

- **ControlInformes.API** — Controllers, JWT bearer auth, Serilog, CORS (open by default),
  `ExceptionHandlingMiddleware`. `Program.cs` wires everything via `AddBusiness()` and
  `AddData(config)`. Controllers are thin: they call a `IBus*` service and return
  `StatusCode(response.HttpCode, response)`.
- **ControlInformes.Business** — Business logic in `Bus*` classes behind `IBus*` interfaces
  (`Implementations/` + `Interfaces/`). Registered in `DependencyInjection.AddBusiness()`.
  Uses AutoMapper. Also holds `PasswordService` and `TokenService` (JWT generation).
- **ControlInformes.Data** — Data access in `Dat*` classes behind `IDat*` interfaces, plus
  `AppDbContext` (EF Core, SQL Server) in `Persistence/` and `Migrations/`. Registered in
  `DependencyInjection.AddData()`.
- **ControlInformes.Domain** — POCO entities (`Publicador`, `Grupo`, `InformeMensual`,
  `Asistencia`, `Usuario`), enums, no dependencies.
- **ControlInformes.Utils** — Cross-cutting helpers shared by all layers: `ApiResponse<T>`,
  `ErrorCatalog`, `PagedResult`, `StringExtensions`, custom exceptions.

### Naming convention
Layer membership is encoded in class prefixes: `Bus*` = business service, `Dat*` = data
access service. The corresponding interface adds an `I` (`IBusPublicador`, `IDatPublicador`).
When adding a feature, create a matching pair in each layer and register both in the
respective `DependencyInjection` file.

### Response & error contract
Every business method returns `ApiResponse<T>` (in Utils). Build responses with the static
factories — never construct success/error shapes by hand:
- `ApiResponse<T>.Ok(data, mensaje)` — 200
- `ApiResponse<T>.Fail(mensaje, codigoError, httpCode)` — generic failure (default 400)
- `ApiResponse<T>.NotFound(mensaje, codigoError)` — 404
- `ApiResponse<T>.Error(mensaje, codigoError)` — 500

Error codes are centralized in `ErrorCatalog` (e.g. `ENT_001`, `VAL_001`, `SYS_001`); use the
constants and `ErrorCatalog.GetMensaje(code)` rather than hardcoding strings. `Bus*` methods
wrap their logic in try/catch and return `ApiResponse<T>.Error(..., ErrorCatalog.ErrorInterno)`
on unexpected exceptions; `ExceptionHandlingMiddleware` is the last-resort net for anything
uncaught and serializes the same `ApiResponse` shape with camelCase JSON.

## Important caveat: unused projects

`ControlInformes.Application` and `ControlInformes.Infrastructure` exist on disk and contain a
MediatR/FluentValidation CQRS skeleton (Features/, Behaviors/, etc.), but they are **NOT part
of `ControlInformes.sln`** and are **not referenced** by the API. They are an abandoned/parallel
architecture. Do not add features there expecting them to run — all live code goes through the
Business/Data layers. `ControlInformes.Tools` (a `GenHash` console helper) is likewise outside
the solution.

## Configuration

`ControlInformes.API/appsettings.json` holds the SQL Server connection string
(`ConnectionStrings:DefaultConnection`, default `localhost\SQLEXPRESS`, DB `DB-CONTROL-INFO`),
JWT settings (`Jwt:Key/Issuer/Audience/ExpireMinutes`), and Serilog config. Auth is JWT bearer;
endpoints require a token unless decorated with `[AllowAnonymous]` (e.g. `POST /api/auth/login`).

PDF templates in `ControlInformes.API/Template/` are copied to output (`CopyToOutputDirectory`)
and consumed at runtime for card generation — keep them in sync when changing card output.
