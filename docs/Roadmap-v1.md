# Roadmap v1

## Phase 0: Documentation Baseline

- define scope
- define architecture
- define configuration strategy
- define verification approach

## Phase 1: Solution Skeleton

- create WPF application
- create supporting class library layout if needed
- add MVVM infrastructure
- add logging baseline

## Phase 2: Project Model

- parse `sketch.yaml`
- define `uss.yaml`
- implement folder discovery
- implement import wizard state

## Phase 3: Arduino Runtime Integration

- resolve bundled `arduino-cli`
- resolve USS-owned cache/data directories
- implement compile workflow
- implement upload workflow

## Phase 4: Operator UX

- home screen
- recent projects
- compile screen
- flash screen
- diagnostics screen

## Phase 5: Verification

- clean-machine checks
- warm-cache checks
- hardware smoke tests
- usability pass with non-technical users

## v1 Deliverables

- portable Windows app
- open folder / import project flow
- compile
- flash
- compile + flash
- project-local artifacts and logs
