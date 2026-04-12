# Cursivis Implementation Overview

This document summarizes how the current Cursivis system is delivered across its major capability areas.

## Core Selection Loop

Current coverage:

- live text selection capture
- trigger-driven execution from Logitech hardware or software controls
- result generation, display, copy, insert, and replace handling
- a consistent `selection -> trigger -> result` workflow across everyday tasks

## Guided Interaction Layer

Current coverage:

- Guided mode around the orb
- context-aware option expansion after the initial quick actions
- custom task fallback through voice or text-trigger refinement
- thumb-wheel / wheel-friendly option navigation model

## Multimodal Input

Current coverage:

- text-first workflows
- lasso-based image and region capture
- voice refinement layered on top of the current selection
- optional text-trigger refinement for noisy or quiet environments

## Browser Execution

Current coverage:

- structured browser action planning
- current-tab execution through the Chromium extension path
- fallback browser automation path when the live tab path is unavailable
- preview, confirmation, and refinement before execution when appropriate

## Logitech Integration

Current coverage:

- packaged Logi Actions SDK plugin
- trigger IPC between plugin and companion
- Actions Ring mapping for core Cursivis actions
- haptic event mapping for selection, execution, and completion feedback

## Runtime Hardening

Current coverage:

- startup scripts for the full local stack
- health-checkable backend and browser services
- optional API-key fallback pool support for uninterrupted sessions
- polished text, image, voice, and action flows aligned around the same interaction model
