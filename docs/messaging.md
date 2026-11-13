# Messaging: exchanges, queues and dead-letter queues

All events go through RabbitMQ (`rabbitmq:3-management` in docker-compose). Open the management UI at http://localhost:15672 (user and password from `RABBITMQ_USER` / `RABBITMQ_PASSWORD`, `guest` / `guest` in `.env.example`).

## Topology

| Exchange | Type | Purpose |
|---|---|---|
| `store.events` | topic, durable | every integration event, routed by its routing key (`order.placed`, `stock.reserved`, …) |
| `store.dlx` | direct, durable | dead-letter exchange; each consumer queue dead-letters into it with its own name as routing key |

Every consumer owns one durable queue bound to `store.events`, and every queue has its own dead-letter queue bound to `store.dlx`:

| Service | Queue | Routing key | Dead-letter queue |
|---|---|---|---|
| Inventory | `inventory.order-placed` | `order.placed` | `inventory.order-placed.dlq` |
| Inventory | `inventory.stock-release-requested` | `stock.release-requested` | `inventory.stock-release-requested.dlq` |
| Payment | `payment.charge-requested` | `payment.charge-requested` | `payment.charge-requested.dlq` |
| Payment | `payment.refund-requested` | `payment.refund-requested` | `payment.refund-requested.dlq` |
| Order | `order.stock-reserved` | `stock.reserved` | `order.stock-reserved.dlq` |
| Order | `order.stock-reservation-failed` | `stock.reservation-failed` | `order.stock-reservation-failed.dlq` |
| Order | `order.payment-charged` | `payment.charged` | `order.payment-charged.dlq` |
| Order | `order.payment-failed` | `payment.failed` | `order.payment-failed.dlq` |
| Order | `order.stock-released` | `stock.released` | `order.stock-released.dlq` |
| Order | `order.payment-refunded` | `payment.refunded` | `order.payment-refunded.dlq` |

`order.confirmed` and `order.cancelled` are notifications: they are published (with publisher confirms) for anyone interested, and no queue is required.

Queues are declared by the consuming service at startup, so they appear as soon as the services are healthy. Verified after `docker compose up --build` through the management API (`GET /api/queues`):

```
inventory.order-placed                     durable=True dlx=store.dlx consumers=1 messages=0
inventory.order-placed.dlq                 durable=True dlx=-         consumers=0 messages=0
inventory.stock-release-requested          durable=True dlx=store.dlx consumers=1 messages=0
inventory.stock-release-requested.dlq      durable=True dlx=-         consumers=0 messages=0
order.payment-charged                      durable=True dlx=store.dlx consumers=1 messages=0
order.payment-charged.dlq                  durable=True dlx=-         consumers=0 messages=0
order.payment-failed                       durable=True dlx=store.dlx consumers=1 messages=0
order.payment-failed.dlq                   durable=True dlx=-         consumers=0 messages=0
order.payment-refunded                     durable=True dlx=store.dlx consumers=1 messages=0
order.payment-refunded.dlq                 durable=True dlx=-         consumers=0 messages=0
order.stock-released                       durable=True dlx=store.dlx consumers=1 messages=0
order.stock-released.dlq                   durable=True dlx=-         consumers=0 messages=0
order.stock-reservation-failed             durable=True dlx=store.dlx consumers=1 messages=0
order.stock-reservation-failed.dlq         durable=True dlx=-         consumers=0 messages=0
order.stock-reserved                       durable=True dlx=store.dlx consumers=1 messages=0
order.stock-reserved.dlq                   durable=True dlx=-         consumers=0 messages=0
payment.charge-requested                   durable=True dlx=store.dlx consumers=1 messages=0
payment.charge-requested.dlq               durable=True dlx=-         consumers=0 messages=0
payment.refund-requested                   durable=True dlx=store.dlx consumers=1 messages=0
payment.refund-requested.dlq               durable=True dlx=-         consumers=0 messages=0
exchange store.dlx direct durable
exchange store.events topic durable
```

## Delivery guarantees

- **Publisher confirms**: every publish waits for the broker's ack; saga commands and replies are published `mandatory`, so a message with no bound queue fails loudly instead of disappearing. Failed publishes are retried with backoff.
- **Manual ack**: consumers use `autoAck: false` and ack only after the database transaction committed.
- **Idempotency**: each consuming service records `(MessageId, Consumer)` in its `ProcessedMessages` table in the same transaction as the business change; a redelivered message is recognised and its outcome is published again with the same derived `MessageId`.
- **Retries, then DLQ**: a handler that throws is retried (requeue with 250 ms, 500 ms … backoff); after `RABBITMQ_MAX_DELIVERY_ATTEMPTS` (3) failures the message is nacked with `requeue: false` and lands in `<queue>.dlq`. A body that is not valid JSON for the event goes to the DLQ immediately. Nothing is dropped silently.

## Forcing a message into a DLQ

### From the management UI

1. Exchanges → `store.events` → **Publish message**.
2. Routing key: `payment.refund-requested`. Headers: `X-Correlation-ID` = `dlq-demo-001`. Properties: `content_type` = `application/json`.
3. Payload (a refund for a payment that does not exist):
   ```json
   {"messageId":"0f0f0f0f-0000-4000-a000-000000000001","correlationId":"dlq-demo-002","orderId":"0f0f0f0f-0000-4000-a000-000000000002","occurredAt":"2026-11-13T12:00:00Z","paymentId":"0f0f0f0f-0000-4000-a000-000000000003","amount":1,"currency":"PHP","reason":"forced DLQ demo"}
   ```
   or simply `this is not json` for an immediate dead-letter.
4. **Publish message**, then Queues → `payment.refund-requested.dlq` → **Get messages** shows it, with its `X-Correlation-ID` header.

### From the command line

```bash
curl -u guest:guest -H "Content-Type: application/json" -X POST http://localhost:15672/api/exchanges/%2F/store.events/publish -d "{\"properties\":{\"content_type\":\"application/json\",\"headers\":{\"X-Correlation-ID\":\"dlq-demo-001\"}},\"routing_key\":\"payment.refund-requested\",\"payload\":\"this is not json\",\"payload_encoding\":\"string\"}"
```

Result from a run (`payment.refund-requested.dlq messages=2` afterwards):

```
fail: Payment.Api.Messaging.RefundPaymentRequestedConsumer[0] => CorrelationId:dlq-demo-001 Message (null) on payment.refund-requested is not a valid RefundPaymentRequested; sending it to payment.refund-requested.dlq System.Text.Json.JsonException …
warn: Payment.Api.Messaging.RefundPaymentRequestedConsumer[0] => CorrelationId:dlq-demo-002 MessageId:0f0f0f0f-… EventType:RefundPaymentRequested Queue:payment.refund-requested RefundPaymentRequested 0f0f0f0f-… failed (attempt 1 of 3); requeueing in 250 ms
warn: Payment.Api.Messaging.RefundPaymentRequestedConsumer[0] => CorrelationId:dlq-demo-002 … failed (attempt 2 of 3); requeueing in 500 ms
fail: Payment.Api.Messaging.RefundPaymentRequestedConsumer[0] => CorrelationId:dlq-demo-002 … failed 3 time(s); sending it to payment.refund-requested.dlq
```

To replay a dead-lettered message after fixing its cause, use the management UI's **Move messages** (shovel plugin) or Get → copy payload → publish it again to `store.events` with the original routing key.
