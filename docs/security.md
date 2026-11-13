# Security: gateway and service authorization

Identity issues HMAC-SHA256 JWTs (`sub`, `email`, `name`, `role`). The same `JWT_ISSUER`, `JWT_AUDIENCE` and `JWT_SIGNING_KEY` from `.env` are used by the gateway and by every service, so a token is checked twice:

1. **Gateway (Ocelot)**: routes with `AuthenticationOptions.AuthenticationProviderKeys: ["Bearer"]` reject a missing, expired or forged token with **401**; routes with `RouteClaimsRequirement` reject a valid token without the required role with **403** (`ClaimValueNotAuthorizedError`) before the request reaches a service.
2. **Services**: every controller action carries `[Authorize(Roles = ...)]` (or `[AllowAnonymous]` for public reads) and validates the token itself, so calling a service port directly (5001–5005, development only) is protected the same way.

## Route rules in `ocelot.json`

| Access | Routes |
|---|---|
| Public | `GET /catalog/v1/products`, `GET /catalog/v1/products/{id}`, `GET /inventory/v1/stock`, `GET /inventory/v1/stock/{id}`, `POST /identity/v1/auth/register`, `POST /identity/v1/auth/login` (5 per minute per client, then 429) |
| Any signed-in user | `GET /identity/v1/auth/me`, `GET /orders/v1/orders/{id}` (the service returns 404 unless it is the caller's order or the caller is Admin) |
| `role: Customer` | `POST /orders/v1/orders`, `GET /orders/v1/orders/mine` |
| `role: Admin` | `POST /catalog/v1/products`, `PUT /catalog/v1/products/{id}`, `GET/PUT /catalog/v1/chaos`, `POST /inventory/v1/stock/{id}/adjustments`, `GET /inventory/v1/reservations/{orderId}`, `GET/PUT /orders/v1/admin/failure-mode`, `GET /payments/v1/payments`, `GET /payments/v1/payments/orders/{orderId}`, `GET/PUT /payments/v1/admin/failure-mode` |

## Verified through the gateway

Tokens from `POST /identity/v1/auth/login` for the seeded accounts (`customer@store.local`, `admin@store.local`), run against `docker compose up --build`:

```
METHOD ROUTE                                                        ANON  CUSTOMER  ADMIN
GET    /catalog/v1/products                                         200   200       200
GET    /inventory/v1/stock                                          200   200       200
GET    /identity/v1/auth/me                                         401   200       200
POST   /catalog/v1/products                                         401   403       201
PUT    /catalog/v1/products/{productId}                             401   403       200
GET    /catalog/v1/chaos                                            401   403       200
POST   /inventory/v1/stock/{productId}/adjustments                  401   403       200
GET    /inventory/v1/reservations/{orderId}                         401   403       404 (no reservation for that id)
GET    /orders/v1/orders/mine                                       401   200       403
GET    /orders/v1/admin/failure-mode                                401   403       200
GET    /payments/v1/payments                                        401   403       200
GET    /payments/v1/admin/failure-mode                              401   403       200
```

The Customer's 403s are produced by the gateway; the service never sees the request:

```
ResponderMiddleware found 1 error -> ClaimValueNotAuthorizedError: claim value: Customer is not the same as required value: Admin for type: role Setting error response for request: GET /payments/v1/payments
```

A token signed with a different key is rejected with 401 at the gateway.

## Verified at the services (second layer)

Calling a service directly, bypassing the gateway:

| Call | Result |
|---|---|
| `GET http://localhost:5005/api/v1/admin/failure-mode`, no token | 401 Problem Details with `correlationId` |
| same, Customer token | 403 |
| `POST http://localhost:5002/api/v1/products`, Customer token | 403 |

Service ports exist for Swagger during development; in a deployment only the gateway (and the Storefront) would be published.

## Demo steps (401 and 403)

```bash
# 401: no token
curl -i http://localhost:5000/payments/v1/admin/failure-mode

# 403: customer token on an Admin route
TOKEN=$(curl -s -H "Content-Type: application/json" -d "{\"email\":\"customer@store.local\",\"password\":\"Customer#Dev2026\"}" http://localhost:5000/identity/v1/auth/login | jq -r .accessToken)
curl -i -H "Authorization: Bearer $TOKEN" -X PUT -H "Content-Type: application/json" -d "{\"mode\":\"DeclineCharge\"}" http://localhost:5000/payments/v1/admin/failure-mode
```
