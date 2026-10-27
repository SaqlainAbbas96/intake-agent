# ADR-002: LLM abstraction

**Status:** Accepted
**Date:** 2026-10-27

## Context

intake-agent calls an LLM to extract structured fields from raw requests. The calling code needs to:

- run in tests and CI with no API key, no network and no cost
- return the same output every time for the same input, so evaluations are repeatable
- compare more than one provider and model later
- report token usage and latency for every call, because cost and speed are part of the quality story

## Decision

Define a small provider interface in the core project and keep every provider behind it:

- `ILlmProvider.CompleteAsync(LlmRequest)` returns an `LlmResponse` with the JSON content, token usage, latency, provider name and model name.
- `MockLlmProvider` is the first implementation. It returns fixture outputs keyed by case id, or a default output when no case id is given. It is deterministic and needs no key.
- The provider is chosen at startup by the `LLM_PROVIDER` setting. The default is `mock`, and an unknown value stops the application at startup with a clear error.
- Real providers are added in later work behind the same interface. Business code never references a vendor SDK directly.
- Cost is not part of the response. It is calculated from usage and a price table elsewhere, because prices change and differ by model.

## Alternatives

- **Call a vendor SDK directly from the extraction code:** the fastest start, but tests would need the network, and switching providers would touch business logic.
- **Use a general-purpose abstraction library such as `Microsoft.Extensions.AI` or Semantic Kernel:** these are good options and may sit behind my interface later. I chose a thin interface of my own first because evaluation needs usage, latency and structured output as first-class results, and I want to understand the boundary before adopting a larger one.
- **Record and replay real responses:** useful later for realistic regression tests, but it needs real calls first, so the mock comes first.

## Consequences

**Good**
- Tests and CI are free, fast and deterministic.
- Switching or comparing providers is a configuration change, not a code change.
- The evaluation harness can run the same cases against the mock and against real providers.

**Costs and risks**
- The mock can drift from how a real model behaves, so it proves wiring, not quality. Quality is measured by evaluations against real providers.
- The interface may need to change when I add features such as streaming or tool calls, and that would be a new ADR.
- I own a small amount of adapter code for each provider.
