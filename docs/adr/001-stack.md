# ADR-001: Stack decisions

**Status:** Accepted
**Date:** 2026-10-24

## Context

intake-agent turns messy inbound requests into validated structured records using an LLM. It needs to:

- serve multiple tenants with isolated data and per-tenant schemas
- process requests in the background, because LLM calls are slow and can fail
- record every step with traces, token usage and cost
- run locally with no cloud account, and later deploy to AWS

## Decision

**Runtime: .NET 10 (LTS).** It is the platform I know best, so I can spend my time on the hard parts (evaluation, reliability, tenant isolation) rather than on the language. It is a long-term support release, with strong built-in support for dependency injection, background services, OpenTelemetry and containers.

**Database: PostgreSQL.** It supports row-level security, which fits tenant isolation at the database level. It also handles JSON columns for raw requests and extracted results, runs locally in Docker, and is available as a managed service on AWS.

**Queue: Amazon SQS, behind an interface.** Background extraction runs through a queue with a dead-letter queue and idempotent consumers. The code depends on an `IQueue` interface, with an in-memory implementation for local development and tests, and SQS for deployment. SQS matches the AWS deployment target and keeps operations simple.

## Alternatives

- **SQL Server instead of PostgreSQL:** familiar to me, but licensing and local setup are heavier, and PostgreSQL's row-level security fits this use case well.
- **MongoDB:** flexible for JSON, but weaker transactional guarantees and no row-level security equivalent for tenant isolation.
- **Azure Service Bus instead of SQS:** a strong managed option, but the deployment target is AWS, and I want this project to show AWS experience.
- **RabbitMQ or Kafka:** more powerful, but more operational weight than this workload needs. Kafka suits high-throughput streaming, which this is not.
- **In-process queue only (for example `System.Threading.Channels`):** simplest, but loses messages on restart, so it is used for local development only.
- **A Postgres-backed queue:** fewer moving parts, but I would be building queue behaviour myself instead of using a managed service.

## Consequences

**Good**
- Tenant isolation can be enforced in the database, not only in application code.
- Local development needs only Docker, with no cloud account.
- The queue interface lets me swap implementations without changing business logic.

**Costs and risks**
- Row-level security adds complexity to migrations and testing.
- Two queue implementations (in-memory and SQS) must stay behaviourally equivalent, so they need shared tests.
- SQS delivers messages at least once, so consumers must be idempotent.
- Tied to AWS for deployment, although the interface limits how much code that affects.
