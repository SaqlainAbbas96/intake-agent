# intake-agent

![CI](https://github.com/SaqlainAbbas96/intake-agent/actions/workflows/ci.yml/badge.svg)

A multi-tenant AI agent built in .NET that turns messy inbound requests (emails, call transcripts) into validated structured records, and measures its own quality with evaluations.

**Status:** early development.
**Data:** all data in this repository is synthetic.

## What it does

1. Receives a raw request.
2. Extracts structured fields with an LLM against a JSON schema.
3. Validates the output.
4. Reports accuracy, latency and cost through an evaluation harness.

## Tech stack

.NET, PostgreSQL, Docker, GitHub Actions

## Quick start

```bash
git clone https://github.com/SaqlainAbbas96/intake-agent.git
cd intake-agent
cp .env.example .env
docker compose up -d --build
curl http://localhost:8080/health
```

Run without an LLM key using mock mode: `LLM_PROVIDER=mock`

## Evaluation

Results will appear here after the first evaluation run.

## Roadmap

- [ ] Extraction API
- [ ] Evaluation harness
- [ ] Human review for low-confidence results
- [ ] Observability and cost tracking
- [ ] Cloud deployment

## Design decisions

- [ADR-001: Stack decisions](docs/adr/001-stack.md)
- [ADR-002: LLM abstraction](docs/adr/002-llm-abstraction.md)

## License

MIT
