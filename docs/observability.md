# Correlation and resilience

How a single request is followed across every service, and how a call to Catalog recovers from injected failures. The log lines below were captured from a local run of the five services against PostgreSQL 16 and RabbitMQ (long ids shortened with `…`).

## How the correlation id travels

| Hop | Mechanism | Code |
|---|---|---|
| Client → gateway | Ocelot `RequestIdKey` / gateway middleware creates `X-Correlation-ID` when the client does not send one | `src/Gateway/Gateway.Api` |
| Gateway → service | Header forwarded; `CorrelationIdMiddleware` reads it (or creates one), returns it in the response and opens a `CorrelationId:<id>` logging scope | `Store.Observability/CorrelationIdMiddleware.cs` |
| Service → service (HTTP) | `CorrelationIdDelegatingHandler` adds the header to generated-client calls (Order → Catalog, Inventory → Catalog) | `Store.Observability/CorrelationIdDelegatingHandler.cs` |
| Service → RabbitMQ | `RabbitMqEventPublisher` writes `X-Correlation-ID` as a message header and the AMQP `correlation_id` property | `Store.Messaging/RabbitMqEventPublisher.cs` |
| RabbitMQ → service | `EventConsumer<T>` restores the header into `ICorrelationIdAccessor` and opens a `CorrelationId MessageId EventType Queue` scope around the handler | `Store.Messaging/EventConsumer.cs` |
| Errors | Every Problem Details response carries `"correlationId"` | `Store.Observability/ObservabilityExtensions.cs` |

Every log line written inside those scopes shows `=> CorrelationId:<id>`, so one search finds the request everywhere:

```bash
docker compose logs --no-log-prefix | grep "CorrelationId:demo-trace-001"
```

## One order across four services

`POST /orders/v1/orders` sent with `X-Correlation-ID: demo-trace-001`:

```
[Order]     17:41:06.794 info: System.Net.Http.HttpClient.ICatalogClient.ClientHandler[100] => CorrelationId:demo-trace-001 Sending HTTP request GET http://localhost:5002/api/v1/products/ac000000-…-000000000006
[Order]     17:41:06.805 info: Order.Api.Application.OrderPlacementService[0] => CorrelationId:demo-trace-001 Placed order 1e912f4b-… for customer a1678533-…: 1 line(s), total 499.00 PHP
[Order]     17:41:06.805 info: Store.Messaging.RabbitMqEventPublisher[0] => CorrelationId:demo-trace-001 Published OrderPlaced d44b09c6-… for order 1e912f4b-… to store.events with routing key order.placed (confirmed)
[Inventory] 17:41:06.810 info: Inventory.Api.Messaging.OrderPlacedConsumer[0] => CorrelationId:demo-trace-001 MessageId:d44b09c6-… EventType:OrderPlaced Queue:inventory.order-placed Reserved 1 product line(s) for order 1e912f4b-…
[Inventory] 17:41:06.812 info: Store.Messaging.RabbitMqEventPublisher[0] => CorrelationId:demo-trace-001 MessageId:d44b09c6-… EventType:OrderPlaced Queue:inventory.order-placed Published StockReserved 078d4819-…
[Order]     17:41:06.818 info: Order.Api.Saga.OrderSagaOrchestrator[0] => CorrelationId:demo-trace-001 MessageId:078d4819-… EventType:StockReserved Queue:order.stock-reserved Order 1e912f4b-…: Pending -> StockReserved after StockReserved
[Order]     17:41:06.823 info: Store.Messaging.RabbitMqEventPublisher[0] => CorrelationId:demo-trace-001 MessageId:078d4819-… EventType:StockReserved Queue:order.stock-reserved Published ChargePaymentRequested 3846524f-…
[Payment]   17:41:06.827 info: Payment.Api.Messaging.ChargePaymentRequestedConsumer[0] => CorrelationId:demo-trace-001 MessageId:3846524f-… EventType:ChargePaymentRequested Queue:payment.charge-requested Charged 499.00 PHP for order 1e912f4b-…
[Payment]   17:41:06.828 info: Store.Messaging.RabbitMqEventPublisher[0] => CorrelationId:demo-trace-001 MessageId:3846524f-… EventType:ChargePaymentRequested Queue:payment.charge-requested Published PaymentCharged 0cce3a2a-…
[Order]     17:41:06.836 info: Order.Api.Saga.OrderSagaOrchestrator[0] => CorrelationId:demo-trace-001 MessageId:0cce3a2a-… EventType:PaymentCharged Queue:order.payment-charged Order 1e912f4b-…: StockReserved -> Confirmed after PaymentCharged
[Order]     17:41:06.839 info: Store.Messaging.RabbitMqEventPublisher[0] => CorrelationId:demo-trace-001 MessageId:0cce3a2a-… EventType:PaymentCharged Queue:order.payment-charged Published OrderConfirmed 92…
```

What to point at:

1. The same `CorrelationId:demo-trace-001` appears in Order, Inventory and Payment.
2. The HTTP call to Catalog carries it (Catalog's own lines for the request show the same id).
3. Each consumed message adds `MessageId`, `EventType` and `Queue`, and the next published message id is the one the following service consumes (`d44b09c6` → `078d4819` → `3846524f` → `0cce3a2a`).

## Retry recovering from an injected fault

With chaos on (`PUT /catalog/v1/chaos {"failureRate":0.6,"delayMs":0}` as Admin), Catalog answers 60% of product reads with 503. Order.Api's typed Catalog client retries through `AddStandardResilienceHandler` and the order still succeeds:

```
[Catalog] 17:41:09.824 warn: Catalog.Api.Chaos.ChaosFilter[0] => CorrelationId:chaos-demo-001 Chaos: injected 503 for GET /api/v1/products/ac000000-…-000000000008 (failure rate 60 %)
[Order]   17:41:09.825 warn: Order.Api.Clients.CatalogResilience[0] => CorrelationId:chaos-demo-001 Retrying Catalog call (attempt 2 of 4) in 173 ms after HTTP 503
[Catalog] 17:41:09.999 warn: Catalog.Api.Chaos.ChaosFilter[0] => CorrelationId:chaos-demo-001 Chaos: injected 503 for GET /api/v1/products/ac000000-…-000000000008 (failure rate 60 %)
[Order]   17:41:10.000 warn: Order.Api.Clients.CatalogResilience[0] => CorrelationId:chaos-demo-001 Retrying Catalog call (attempt 3 of 4) in 562 ms after HTTP 503
[Catalog] 17:41:10.563 warn: Catalog.Api.Chaos.ChaosFilter[0] => CorrelationId:chaos-demo-001 Chaos: injected 503 for GET /api/v1/products/ac000000-…-000000000008 (failure rate 60 %)
[Order]   17:41:10.564 warn: Order.Api.Clients.CatalogResilience[0] => CorrelationId:chaos-demo-001 Retrying Catalog call (attempt 4 of 4) in 369 ms after HTTP 503
[Order]   17:41:10.948 info: Order.Api.Application.OrderPlacementService[0] => CorrelationId:chaos-demo-001 Placed order 7544d4be-… for customer a1678533-…: 1 line(s), total 599.00 PHP
```

Three injected 503s, three retries with growing, jittered delays (173, 562, 369 ms), then the fourth attempt succeeds and the order is placed. Over 20 orders at a 50% failure rate, all 20 were placed (19 failures injected and absorbed by retries).

### Resilience settings (Order.Api and Inventory.Api → Catalog)

| Strategy | Setting |
|---|---|
| Retry | 3 retries, exponential backoff from 300 ms with jitter; logged as `Retrying Catalog call (attempt n of 4)` |
| Attempt timeout | 2 s per attempt |
| Total timeout | 10 s per call |
| Circuit breaker | opens for 15 s when 80% of at least 10 attempts in 30 s fail; logged as `Catalog circuit opened for 15 s` |
| After giving up | the adapter throws `DownstreamServiceException`, returned to the client as `503` Problem Details with `correlationId` |

To see the circuit breaker, set `failureRate` to `1`: after about three orders the log shows `Catalog circuit opened for 15 s` and further orders fail fast with `503 Catalog is unavailable` until the break ends. Set chaos back to `{"failureRate":0,"delayMs":0}` afterwards.

## Messages that keep failing

A consumer retries a failing message (`… failed (attempt 1 of 3); requeueing in 250 ms`) and after the third failure nacks it without requeue, which moves it to `<queue>.dlq`:

```
fail: Payment.Api.Messaging.RefundPaymentRequestedConsumer[0] … RefundPaymentRequested a6002ec4-… for order 0204c591-… failed 3 time(s); sending it to payment.refund-requested.dlq
```

The dead-lettered message keeps its `X-Correlation-ID` header, so it can be matched to the original request in the RabbitMQ management UI (Queues → `payment.refund-requested.dlq` → Get messages).
