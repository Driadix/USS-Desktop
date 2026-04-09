# Product Vision

## Problem

The current batch-based prototype proves that deterministic Arduino build and flash workflows are possible, but the operator experience is weak:

- `cmd` windows feel fragile
- logs disappear too quickly
- per-project scripts are noisy and repetitive
- supportability degrades as new project types are added

The final product must keep the deterministic behavior while replacing the operator-facing workflow with a clean portable Windows application.

## Product Goal

Provide a single portable executable that opens an Arduino-based project folder and gives the operator exactly three primary actions:

- Compile
- Flash
- Compile + Flash

The app must be usable without IDE knowledge, `PATH` setup, or command-line work.

## Primary Users

- electricians
- production operators
- field technicians
- support staff

## Secondary Users

- firmware engineers who prepare project configuration
- CI/CD maintainers who may later reuse the same backend logic

## v1 Scope

### In Scope

- Windows desktop application
- Arduino projects only
- ESP32 boards through Arduino core
- STM32 boards through Arduino core
- project import wizard
- pinned toolset usage
- deterministic compilation into project-local `build` directory
- flashing through `arduino-cli upload`
- recent projects list
- diagnostics view

### Out of Scope

- non-Arduino STM32 pipelines
- direct `esptool` workflows
- direct `STM32CubeProgrammer` workflows
- custom scripting engine
- editing source code
- package manager UI for arbitrary cores and libraries

## Operator Experience Target

The app should feel like a focused utility, not an IDE.

Characteristics:

- one obvious entry point
- one obvious project status area
- one obvious action area
- logs available but not forced on the operator
- no hidden side effects
- clear next-step messaging when something is missing

## Success Criteria

- an operator can open a correctly configured project and flash hardware without external instructions
- the same project produces the same artifacts on different operator machines
- support can diagnose failures from application logs and project-local logs
- no per-project batch files are required in the steady state
