# Architecture

## High-Level Shape

USS Desktop should be one Windows application with a layered architecture:

- Presentation layer: WPF desktop UI
- Application layer: commands, workflows, validation, orchestration
- Domain layer: project model, toolset model, build/flash intents
- Infrastructure layer: process execution, YAML parsing, file system, logs

## Main Runtime Components

### Shell

The WPF shell owns:

- window navigation
- recent projects
- command surfaces
- progress and status display
- diagnostics/log view

### Project Discovery

Responsible for opening a folder and determining whether:

- it is already a USS project
- it is an Arduino project that can be imported
- it is unsupported

### Project Configuration Service

Responsible for:

- reading `uss.yaml`
- reading `sketch.yaml`
- deriving project family from `fqbn`
- validating that the project is internally consistent

### Toolset Resolver

Responsible for:

- locating bundled `arduino-cli`
- locating USS-owned Arduino data/cache directories
- exposing resolved paths to the application layer

### Arduino Workflow Adapter

This is the v1 engine.

Responsibilities:

- compile via `arduino-cli compile`
- upload via `arduino-cli upload`
- upload prebuilt artifacts when appropriate
- collect standard output / error
- normalize success and failure messages

### Artifact Service

Responsible for stable project-local layout:

- `build/out`
- `build/logs`
- `build/work`

The app should never scatter outputs across random temp folders unless the output is explicitly transient.

### Diagnostics Service

Responsible for:

- environment snapshot
- tool versions
- project validation results
- connected serial ports
- recent operation logs

## Application Flows

### Open Folder

1. user selects folder
2. app checks for `uss.yaml`
3. if absent, app checks for `sketch.yaml` or Arduino sketch structure
4. app either opens the project or launches the import wizard

### Compile

1. validate project
2. resolve toolset
3. ensure required core/library state exists in USS cache
4. run compile
5. store logs
6. refresh artifact panel

### Flash

1. validate project
2. validate build artifacts
3. detect available ports
4. run upload through `arduino-cli`
5. store logs
6. display result

### Compile + Flash

1. compile
2. stop immediately on failure
3. flash if compile succeeds

## Configuration Strategy

The long-term model is:

- `sketch.yaml` remains the Arduino-native technical source of truth
- `uss.yaml` stores USS-specific UX and workflow metadata

This avoids hiding Arduino behavior behind a custom opaque format while still giving USS a stable UI-level configuration surface.

## Concurrency and Safety

The current prototype assumes that operators do not build simultaneously in the same shared project folder. That assumption remains valid for v1.

USS should make it visible:

- create a lightweight lock file during compile/flash
- show a warning if the project looks busy
- not attempt distributed locking in v1

## Logging Strategy

There should be two audiences:

- operators: concise status and copyable summary
- support/engineering: full logs

Recommended outputs:

- UI log pane
- project-local log files under `build/logs`
- optional app session log
