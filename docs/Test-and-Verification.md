# Test and Verification

## Verification Goals

We need to verify both product behavior and operator usability.

The product is not successful if it only works technically but remains confusing in production use.

## Test Layers

### 1. Domain and Configuration Tests

Verify:

- `sketch.yaml` parsing
- `uss.yaml` parsing
- family inference from `fqbn`
- validation rules for missing or inconsistent config

### 2. Application Workflow Tests

Verify:

- open folder decisions
- compile workflow orchestration
- flash workflow orchestration
- compile + flash stop conditions
- operator-facing status transitions

### 3. Infrastructure Tests

Verify:

- `arduino-cli` command construction
- log capture
- artifact path normalization
- tool resolution logic

### 4. Manual Hardware Verification

Verify on real boards:

- ESP32 Arduino compile
- ESP32 Arduino flash
- STM32 Arduino compile
- STM32 Arduino flash
- upload of existing artifacts without rebuild

## First Manual Verification Matrix

### Project Detection

- folder with `uss.yaml`
- folder with only `sketch.yaml`
- folder with only `.ino`
- unsupported folder

### Compile

- first build on clean cache
- rebuild with warm cache
- missing core
- missing library
- invalid profile

### Flash

- one COM port available
- multiple COM ports available
- disconnected board
- stale build artifacts
- upload failure from wrong board or port

### UX

- operator can complete compile without instructions
- operator can complete flash without instructions
- failure messages identify the next action
- logs are accessible but not overwhelming

## Log and Diagnostic Outputs

Every compile/flash run should produce:

- UI-visible summary
- exit status
- structured log file
- exact command line used
- tool versions used

This is mandatory for supportability.

## Definition of Done for v1 Beta

- deterministic compile for at least one ESP32 Arduino project
- deterministic compile for at least one STM32 Arduino project
- successful upload for both families through `arduino-cli`
- operator workflow validated by non-developer users
- project import wizard validated on fresh folders
