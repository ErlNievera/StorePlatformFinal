# Contributions

Who built what in the Store Platform capstone. Each member wrote their own row in their own pull request. The code was written with the help of Claude Code (an AI coding assistant) under each member's direction and review.

| Member | GitHub | Services | Shared pieces | Cross-boundary work | Pull requests |
|---|---|---|---|---|---|
| Stefan Pimentel | [@PimentelStef](https://github.com/PimentelStef) | Catalog.Api (products, dev-only chaos), Gateway.Api (Ocelot routes, JWT at the edge, login rate limit, QoS), all Dockerfiles and `docker-compose.yml` | Store.Observability (correlation id middleware and DelegatingHandler, Problem Details + IExceptionHandler, logging scopes, JWT validation, API versioning and per-version Swagger) | Generated Catalog client, adapter and standard resilience handler inside Order.Api and Inventory.Api; correlation id carried through RabbitMQ headers in Store.Messaging; README, architecture docs and diagrams | #1 (Phase 0 scaffold, committed under a neutral tool identity), #7, #9, #11, #13, #19, #20, #25, #27 |
