# Configuration Model

## Goals

The configuration model must be:

- explicit
- inspectable
- easy to validate
- friendly to both GUI and future automation

## Files

### `sketch.yaml`

This remains the Arduino-native definition of:

- board profile
- pinned core/platform version
- pinned library versions

For v1, USS should prefer using this file instead of inventing a replacement for Arduino build metadata.

### `uss.yaml`

This file should be added by USS when a project is imported or configured.

It defines USS-specific behavior such as:

- display name
- project type
- default action settings
- artifact layout
- default upload preferences

## Proposed `uss.yaml` Shape

```yaml
version: 1

project:
  name: LilygoT
  kind: arduino
  family: esp32
  profile: main

artifacts:
  output_dir: build/out
  log_dir: build/logs
  work_dir: build/work

upload:
  port: auto
  verify_after_upload: true
```

## Family Detection

For Arduino projects, the family should be derived from `fqbn`.

Examples:

- `esp32:esp32:*` -> `esp32`
- `stm32:stm32:*` -> `stm32`

If no `sketch.yaml` exists yet, the import wizard asks the user for family and board selection, then writes both files.

## Import Wizard Rules

### If `uss.yaml` exists

- open project directly
- validate and report issues

### If only `sketch.yaml` exists

- read it
- infer family and profile
- offer to create `uss.yaml`

### If only Arduino sketch files exist

- ask user to select board family and board
- generate initial `sketch.yaml`
- generate `uss.yaml`

### If unsupported folder

- show that USS cannot manage this folder in v1

## Why `toolchain.env` Is Not the Long-Term Choice

`toolchain.env` worked for the batch prototype, but it is not ideal for the desktop product because:

- it is not structured
- it is harder to evolve safely
- it mixes tool wiring and project semantics
- it is less convenient for schema validation

`uss.yaml` is the better long-term application-level configuration file.
