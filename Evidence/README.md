# Evidence

Checklist and capture instructions for the capstone evidence. The screenshots and the demo video are recorded by the team; this file says exactly what to capture and where it shows each requirement.

Save screenshots in `Evidence/screenshots/` with the numbers below (for example `Evidence/screenshots/01-successful-order.png`) and put the demo video link at the end of this file.

## Before recording

```bash
docker compose down -v          # fresh databases, seeded again on start
setup.cmd
docker compose up --build       # wait until every container is "healthy": docker compose ps
```

- Storefront: http://localhost:5100 · Gateway: http://localhost:5000 · RabbitMQ: http://localhost:15672 (guest / guest)
- Accounts: `customer@store.local` / `Customer#Dev2026`, `admin@store.local` / `Admin#Dev2026`
- Keep a terminal open for logs: `docker compose logs -f --no-log-prefix order-api inventory-api payment-api`

## Checklist

### 1. Successful order

- [ ] `01-successful-order.png`: Storefront order page showing the timeline **Order placed → Stock reserved → Payment charged → Order confirmed** with timestamps, and the green "Confirmed" status.
- [ ] `01b-my-orders.png`: My Orders listing the order as Confirmed.

How: sign in as the customer, add any product, Checkout → Place order. The page updates every 2 seconds by itself; capture it once it says Confirmed.

API alternative: `http/flows.http` section 3, or Postman folder "3. Successful order", then `GET /orders/v1/orders/{id}` shows the same `history`.

### 2. On-demand compensation

- [ ] `02a-chaos-page.png`: Admin → Chaos & Saga Testing with Payment mode **DeclineCharge** (red border).
- [ ] `02b-declined-order.png`: the customer's order page: Stock reserved, *Something went wrong: Payment declined…*, red **Compensation: Stock released**, **Order cancelled**.
- [ ] `02c-fail-after-payment.png`: with Order mode **FailAfterPayment**: Payment charged, then red **Payment refunded** and **Stock released**, then Cancelled.
- [ ] `02d-insufficient-stock.png`: an order for 2 × Founders' Commemorative Medal (1 in stock): Cancelled "Insufficient stock: requested 2, available 1", no compensation.

How: the switches are on the Chaos page, or `PUT /payments/v1/admin/failure-mode {"mode":"DeclineCharge"}` and `PUT /orders/v1/admin/failure-mode {"mode":"FailAfterPayment"}` with the admin token (`GET` on the same URLs shows the current mode). No code edits and no restarts. Switch both back to normal afterwards.

### 3. Retry recovering from an injected fault

- [ ] `03a-catalog-chaos.png`: Chaos page with Catalog faults at 50% failure.
- [ ] `03b-retry-logs.png`: terminal with Order.Api retrying and Catalog injecting the faults for the same correlation id, then the order placed:
  ```
  Catalog.Api.Chaos.ChaosFilter … CorrelationId:<id> Chaos: injected 503 for GET /api/v1/products/… (failure rate 50 %)
  Order.Api.Clients.CatalogResilience … CorrelationId:<id> Retrying Catalog call (attempt 2 of 4) in 3xx ms after HTTP 503
  Order.Api.Application.OrderPlacementService … CorrelationId:<id> Placed order …
  ```
- [ ] `03c-order-ok.png`: the order still succeeded (Confirmed).

How: set Catalog faults to 50%, place an order (Storefront, or `http/flows.http` section 8 which sends `X-Correlation-ID: flows-chaos-001`), then run `docker compose logs --no-log-prefix order-api catalog-api | findstr "flows-chaos-001"` (Windows) or `grep flows-chaos-001` elsewhere. For the circuit breaker, set 100%: Order logs `Catalog circuit opened for 15 s` and the order fails fast with 503. Turn chaos off afterwards.

### 4. Correlation-id trace across services

- [ ] `04-correlation-trace.png`: one correlation id in the logs of Order, Inventory and Payment.

How: place an order through the gateway with the header `X-Correlation-ID: demo-trace-001` (`http/flows.http` lets you set it), then:

```bash
docker compose logs --no-log-prefix order-api inventory-api payment-api | findstr "demo-trace-001"
```

Lines to point at (a full captured example is in [docs/observability.md](../docs/observability.md)):

| Service | Log line |
|---|---|
| Order | `OrderPlacementService … CorrelationId:demo-trace-001 Placed order …` and `RabbitMqEventPublisher … Published OrderPlaced …` |
| Inventory | `OrderPlacedConsumer … CorrelationId:demo-trace-001 MessageId:… EventType:OrderPlaced Queue:inventory.order-placed Reserved 1 product line(s) …` |
| Order | `OrderSagaOrchestrator … Order …: Pending -> StockReserved after StockReserved` |
| Payment | `ChargePaymentRequestedConsumer … CorrelationId:demo-trace-001 … Charged … PHP for order …` |
| Order | `OrderSagaOrchestrator … StockReserved -> Confirmed after PaymentCharged` |

Also visible: every API response has an `X-Correlation-ID` header, and every error body has `"correlationId"`.

### 5. RabbitMQ queues, including a .dlq

- [ ] `05a-queues.png`: management UI → Queues: the ten consumer queues and ten `.dlq` queues (durable; one consumer on each consumer queue).
- [ ] `05b-exchanges.png`: Exchanges: `store.events` (topic) and `store.dlx` (direct).
- [ ] `05c-dlq-message.png`: `payment.refund-requested.dlq` with a message, opened with **Get messages** (shows the `X-Correlation-ID` header).
- [ ] `05d-dlq-logs.png`: Payment logs `failed (attempt 1 of 3)`, `(attempt 2 of 3)`, then `failed 3 time(s); sending it to payment.refund-requested.dlq`.

How to force a message into the DLQ: management UI → Exchanges → `store.events` → Publish message with routing key `payment.refund-requested`, header `X-Correlation-ID = dlq-demo-001` and payload `this is not json` (dead-lettered at once), or a refund for an unknown payment (dead-lettered after 3 attempts). The exact payload and a curl command are in [docs/messaging.md](../docs/messaging.md#forcing-a-message-into-a-dlq).

### 6. Gateway 401 and 403

- [ ] `06a-401.png`: `PUT http://localhost:5000/payments/v1/admin/failure-mode` without a token → **401**.
- [ ] `06b-403.png`: the same request with the customer's token → **403**.
- [ ] `06c-403-page.png`: in the Storefront, signed in as the customer, open http://localhost:5100/admin/chaos → "This area is for staff".
- [ ] `06d-gateway-log.png`: gateway log line `ClaimValueNotAuthorizedError: claim value: Customer is not the same as required value: Admin for type: role`.

How: `http/flows.http` section 7, or Postman folder "7. Security". The full matrix is in [docs/security.md](../docs/security.md).

## Demo walkthrough script (about 8 minutes)

Everything goes through the gateway (Storefront → gateway, or Postman / flows.http → gateway).

1. **Clean start** (1 min). Show `docker compose ps` with all 13 containers healthy after `docker compose down -v` and `docker compose up --build`. Mention: one PostgreSQL per service, RabbitMQ, five services, the gateway and the Storefront.
2. **Browse** (30 s). Open http://localhost:5100: the catalog loads through `GET /catalog/v1/products` and `/inventory/v1/stock`; point at the availability badges and the light/dark toggle.
3. **Log in** (30 s). Sign in as the customer. Mention that the JWT from Identity is stored in an HttpOnly cookie and sent to the gateway as a Bearer token.
4. **Successful order** (1.5 min). Add the Caritas Coffee Mug, Checkout, Place order. Watch the timeline move to Confirmed. Show the same history from `GET /orders/v1/orders/{id}`. Mention the price snapshot on the order line.
5. **Failure and compensation** (2.5 min).
   - Sign in as admin in a second window → Chaos & Saga Testing → Decline charges.
   - As the customer, order again: stock reserved, payment declined, **stock released (red)**, cancelled.
   - Admin → back to normal, then Fail after payment. Order again: payment charged, **refunded** and **stock released**, cancelled. Show the payment as Refunded (`GET /payments/v1/payments/orders/{id}`). Switch back to normal.
   - Optional: Founders' Commemorative Medal × 2 → cancelled for insufficient stock.
6. **Resilience and tracing** (1 min). Catalog faults at 50% → the order still succeeds; show the `Retrying Catalog call` and `Chaos: injected 503` lines sharing one `CorrelationId`. Show the RabbitMQ queues and a message in a `.dlq`.
7. **Blocked admin action** (1 min). As the customer, call `PUT /payments/v1/admin/failure-mode` → 403; without a token → 401; open `/admin/chaos` in the Storefront → staff-only page. Six wrong logins → 429 "Too many login attempts".

## Demo video

Link: add the recording link here after recording.
