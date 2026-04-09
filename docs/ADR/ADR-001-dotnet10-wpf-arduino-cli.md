# ADR-001: .NET 10 + WPF + Arduino CLI

## Status

Accepted

## Context

USS Desktop needs to replace a batch-script proof of concept with a reliable Windows application aimed at non-technical operators.

The first version must remain narrow:

- Windows only
- Arduino projects only
- deterministic build and flash
- portable deployment

## Decision

Use:

- .NET 10
- WPF
- `arduino-cli` as the only build/flash runtime adapter in v1

## Rationale

### .NET 10

- already available in the target environment
- modern desktop runtime
- clean `dotnet` CLI workflow

### WPF

- best fit for Windows-first operator UX
- mature ecosystem
- strong tooling

### Arduino CLI as single adapter

- keeps system behavior conceptually small
- matches the real project constraint: Arduino-based build pipelines
- reduces support surface in v1
- avoids exposing low-level flasher tools to operators

## Consequences

### Positive

- simpler product surface
- easier operator training
- fewer branches in workflow code
- easier testing

### Negative

- some board-specific edge cases may need later adapter expansion
- non-Arduino firmware workflows stay out of scope until a later version

## Follow-Up

Future ADRs should define:

- final `uss.yaml` schema
- repository structure inside `src/`
- packaging model for bundled toolsets
