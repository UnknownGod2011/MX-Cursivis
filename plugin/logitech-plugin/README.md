# Logitech Plugin (C#)

## Purpose

Capture Logitech hardware intent and forward it to the local companion app.

## Scope

- Trigger types: `tap`, `long_press`, `long_press_start`, `long_press_end`, `dial_press`, `dial_tick`
- IPC sender client for trigger payloads
- Optional haptic feedback mapping

## Input

- Logitech device event from Logi Actions SDK

## Output

- `TriggerEvent` JSON payload to companion IPC endpoint

## Status

`src/Cursivis.Logitech.Bridge` is implemented as a functional trigger bridge that sends Logitech-style trigger events over local WebSocket to the companion app.

It now also subscribes to companion haptic events over:

- `ws://127.0.0.1:48712/cursivis-haptics/`

This keeps trigger and haptic validation aligned with the same local runtime used by the companion app and the packaged plugin.

`src/CursivisPlugin` is generated with `LogiPluginTool` and contains a real Logi Actions SDK C# plugin project with Cursivis actions:

- `Cursivis Trigger` -> sends `tap`
- `Cursivis Long Press` -> sends `long_press`
- `Cursivis Long Press Start` -> sends `long_press_start`
- `Cursivis Long Press End` -> sends `long_press_end`
- `Cursivis Dial` adjustment -> sends `dial_tick` and `dial_press`

These actions forward events to `ws://127.0.0.1:48711/cursivis-trigger/`.

## Current readiness

The Logitech plugin path is working end-to-end on a machine with Logi Options+ installed:

- `src/CursivisPlugin` builds against the current Logi Actions SDK runtime
- the Debug/Release build hot-loads into Logi Plugin Service through the generated `.link` file
- dynamic Cursivis actions are discovered by the Logitech host
- companion haptic events are translated into Logitech plugin events
- the plugin packages and verifies successfully as `Cursivis.lplug4`

## Hardware validation notes

The packaged plugin is ready to install today. Final tactile and ergonomic refinement remains device-specific and benefits from real supported MX hardware:

- Actions Ring ergonomics validation
- MX-specific haptic feel validation
- final profile tuning for MX Creative Console / supported MX devices

## Real plugin build / package workflow

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-logitech-plugin.ps1 -Configuration Release
```

Optional install step:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-logitech-plugin.ps1 -Configuration Release -InstallPackage
```

This workflow:

- builds the real Logitech plugin
- packs it into `plugin/logitech-plugin/dist/Cursivis.lplug4`
- verifies the package with `LogiPluginTool`
- prints the plugin log path for quick inspection

Direct package-only install path:

```powershell
& "$env:USERPROFILE\.dotnet\tools\logiplugintool.exe" install ".\plugin\logitech-plugin\dist\Cursivis.lplug4"
```

Use that direct install path only after the rest of the local stack is ready, because the plugin is expected to work together with the companion runtime, browser extension, and Logitech control mapping.

Prerequisites:

- Logi Options+ installed
- `PluginApi.dll` available under `C:\Program Files\Logi\LogiPluginService\`
- `logiplugintool.exe` available under `%USERPROFILE%\.dotnet\tools\`

Useful runtime paths:

- plugin log:
  - `%LOCALAPPDATA%\Logi\LogiPluginService\Logs\plugin_logs\Cursivis.log`
- debug `.link` file:
  - `%LOCALAPPDATA%\Logi\LogiPluginService\Plugins\CursivisPlugin.link`

## Default Logitech UX

The current hardware-ready control design is documented in:

- `plugin/logitech-plugin/CONTROL_MAP.md`

This gives the project a clear, hardware-native interaction model for MX Creative Console, MX Master 4, and Actions Ring.

Current reference interaction goals:

- one programmable MX Master 4 button as the instant Cursivis trigger
- one nested Actions Ring folder for `Trigger`, `Talk`, `Take Action`, `Snip-it`, and `Settings`
- thumb wheel or wheel-driven navigation for Guided mode
- haptic confirmation for selection changes and execution state

## Bridge Run

```powershell
cd plugin/logitech-plugin/src/Cursivis.Logitech.Bridge
dotnet run
```

Controls:

- `T` = tap trigger
- `L` = long press (single event)
- `S` = long press start (press-and-hold begin)
- `E` = long press end (press-and-hold release)
- `P` = dial press
- `A` = dial tick -1
- `D` = dial tick +1
- `Q` = quit

## Logi SDK Plugin Notes

Logi SDK plugin project path:

- `plugin/logitech-plugin/src/CursivisPlugin/src`

Build this project on a machine with Logi Plugin Service installed (for `PluginApi.dll` reference).

## Virtual validation path

Without MX hardware, the most useful validation path is:

1. run the Cursivis companion stack
2. build/package the real plugin with `build-logitech-plugin.ps1`
3. confirm the plugin loads in Logi Plugin Service
4. drive synthetic trigger events through the local trigger WebSocket
5. confirm `action_change`, `processing_start`, and `processing_complete` appear in `Cursivis.log`

Example synthetic trigger:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\send-logitech-trigger.ps1 -PressType dial_tick -DialDelta 1
```

Note:

- use supported MX hardware for final Actions Ring and haptic-feel validation
