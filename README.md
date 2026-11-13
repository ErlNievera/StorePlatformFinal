# Store Platform Capstone

**CruzDusty_StorePlatformCapstone** — a microservices store platform built with .NET 10, RabbitMQ, PostgreSQL 16 and an Ocelot API gateway, with an ASP.NET Core MVC storefront.

UST Angelicum College — Capstone project

## Team

| Member | GitHub | Services | Shared pieces |
|---|---|---|---|
| Stefan Pimentel | [@PimentelStef](https://github.com/PimentelStef) | Catalog, Gateway, Docker Compose | Store.Observability |
| Erl Lorence Nievera | [@ErlNievera](https://github.com/ErlNievera) | Inventory, Payment | Store.Contracts.Events, Store.Messaging |
| Klarence Villar | [@KlarenceVillar](https://github.com/KlarenceVillar) | Identity, Order (saga), Storefront | — |

## What the system does

A customer browses active products, registers or logs in, and places an order. The order goes through an orchestrated saga driven by events over RabbitMQ:

```
Pending → StockReserved → PaymentCharged → Confirmed
```

When a step fails, the saga compensates and records why:

| Failure | Compensation | Final status |
|---|---|---|
| Not enough stock | nothing to undo | Cancelled |
| Payment declined | release reserved stock | Cancelled |
| Failure after payment | refund payment + release stock | Cancelled |

Admins can switch the payment and order failure modes on and off at runtime, through the gateway, to demonstrate compensation.

## Components

| Component | Responsibility |
|---|---|
| Identity.Api | Register, login, issue JWTs with roles `Customer` and `Admin` |
| Catalog.Api | Products (name, price, active flag) — the source of truth for what can be sold |
| Inventory.Api | Stock on hand and reservations per product |
| Order.Api | Orders, order lines, status history; orchestrates the saga |
| Payment.Api | Charges and refunds, with an on-demand failure switch |
| Gateway.Api | Ocelot gateway — the single entry point; validates JWTs and routes to versioned services |
| Storefront.Web | ASP.NET Core MVC storefront that calls only the gateway |
| Store.Contracts.Events | Event records shared by publishers and consumers |
| Store.Messaging | RabbitMQ connection, publisher confirms, consumer base class, DLX/DLQ topology, idempotency |
| Store.Observability | Correlation ID middleware and handler, Problem Details, logging scopes |

Each service owns its own PostgreSQL 16 database (database-per-service). Services never read each other's databases; they talk through REST via generated clients or through events.

## Repository layout

```
CruzDusty_StorePlatformCapstone.slnx   solution (all projects)
global.json                            pins the .NET 10 SDK band
.env.example                           local-dev configuration (copied to .env by setup.cmd)
setup.cmd                              one-time setup for Windows
contracts/                             OpenAPI 3.0 contracts, one per service (written before controllers)
docs/                                  architecture write-up and diagrams
postman/                               Postman collection and environment
http/                                  flows.http for the REST Client / Rider / Visual Studio
Evidence/                              demo checklist and capture instructions
src/
  Gateway/Gateway.Api
  Services/Identity/Identity.Api
  Services/Catalog/Catalog.Api
  Services/Inventory/Inventory.Api
  Services/Order/Order.Api
  Services/Payment/Payment.Api
  Shared/Store.Contracts.Events
  Shared/Store.Messaging
  Shared/Store.Observability
  Storefront/Storefront.Web
```

## API contracts

Every service is described by an OpenAPI 3.0 contract in `contracts/`, merged before its controllers were written. Controllers match the contracts (paths, operationIds, schemas, status codes), and every cross-service or Storefront call goes through a client generated from them.

| Contract | Gateway base URL | Service base URL (dev) |
|---|---|---|
| `contracts/identity.v1.yaml` | `http://localhost:5000/identity/v1` | `http://localhost:5001/api/v1` |
| `contracts/catalog.v1.yaml` | `http://localhost:5000/catalog/v1` | `http://localhost:5002/api/v1` |
| `contracts/inventory.v1.yaml` | `http://localhost:5000/inventory/v1` | `http://localhost:5003/api/v1` |
| `contracts/order.v1.yaml` | `http://localhost:5000/orders/v1` | `http://localhost:5004/api/v1` |
| `contracts/payment.v1.yaml` | `http://localhost:5000/payments/v1` | `http://localhost:5005/api/v1` |

Paths in each contract are relative to these base URLs, so a generated client switches between the gateway and a service by changing only its base address. Errors are `application/problem+json` with a `correlationId` extension.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) with Compose v2
- Git

## Getting started

```
git clone https://github.com/ErlNievera/StorePlatformFinal.git
cd StorePlatformFinal
setup.cmd
docker compose up --build
```

`setup.cmd` checks that .NET and Docker are installed, creates `.env` from `.env.example` if it does not exist, builds the solution, and validates `docker-compose.yml`.

`.env` is gitignored. Only `.env.example` is committed, and every value in it is a local-development default.

## Planned port map

| Component | Host port |
|---|---|
| Gateway (the only API entry point) | 5000 |
| Identity.Api Swagger (dev) | 5001 |
| Catalog.Api Swagger (dev) | 5002 |
| Inventory.Api Swagger (dev) | 5003 |
| Order.Api Swagger (dev) | 5004 |
| Payment.Api Swagger (dev) | 5005 |
| Storefront | 5100 |
| RabbitMQ management UI | 15672 |

Databases are not exposed to the host.

## How we work

- `main` is protected. All work after the first commit goes through pull requests with one approving review.
- Branch names: `<github-username>/phase<N>-<short-topic>`, for example `PimentelStef/phase1-catalog-contract`.
- Conventional commit messages: `feat(catalog): ...`, `fix(saga): ...`, `docs(readme): ...`, `chore(compose): ...`, `test(postman): ...`.
- Contract-first: each service's OpenAPI contract in `contracts/` merges before any controller for that service is written.
- `dotnet build CruzDusty_StorePlatformCapstone.slnx` must pass before every push.

Review rotation:

| PR author | Reviewer |
|---|---|
| Stefan Pimentel | @KlarenceVillar |
| Erl Lorence Nievera | @PimentelStef |
| Klarence Villar | @ErlNievera |

## Delivery phases

| Phase | Content |
|---|---|
| 0 | Repository setup |
| 1 | OpenAPI contracts and event records |
| 2 | Shared projects, service skeletons, databases, migrations and seed data |
| 3 | Controllers, generated clients, resilience |
| 4 | Events and the order saga |
| 5 | Dockerfiles, Docker Compose, Ocelot gateway, security |
| 6 | Storefront, documentation, Postman and HTTP flows |
| 7 | Integration testing, evidence, v1.0 release |
