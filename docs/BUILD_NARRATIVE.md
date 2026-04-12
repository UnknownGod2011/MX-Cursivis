# Cursivis Build Narrative

## Product Idea

Cursivis is a cursor-native workflow system for Logitech MX hardware, especially MX Creative Console, MX Master 4, and Actions Ring.

The core interaction model is simple:

> **Selection = Context, Trigger = Intent, Cursivis = Action**

Instead of moving into a separate chat interface and manually restating context, the user selects what they are already working on and triggers Cursivis directly from Logitech hardware.

## What The System Delivers

Cursivis can:

- summarize long text
- explain or improve selected code
- translate foreign-language content
- refine drafts and emails
- analyze selected images
- combine selection with voice or typed refinement
- execute browser workflows through `Take Action`

The value is not just response generation. It is keeping the workflow in place while the system understands what the user selected and carries the next step forward.

## Why Logitech Matters

The project is built around Logitech's control surfaces:

- `Trigger` for immediate action
- `Talk` for contextual refinement
- `Snip-it` for image and region selection
- `Take Action` for execution in the live workflow
- dial, wheel, and ring interactions for option navigation

That is what makes Cursivis feel different from a generic AI tool with shortcuts. Logitech hardware becomes the intent layer of the workflow.

## System Shape

Cursivis is composed of:

- a Windows companion app in WPF
- a packaged Logitech plugin built with the Logi Actions SDK
- a browser execution layer for current-tab and fallback automation flows
- a reasoning backend for text, image, voice, and action planning
- a shared IPC layer connecting the plugin, companion, and backend

## Key Engineering Challenges

The most important technical challenges were:

- keeping Smart Mode genuinely useful and context-aware
- making Guided Mode compact, fast, and easy to navigate around the orb
- preserving the latest selection reliably across real applications
- making voice feel like refinement of live context instead of a separate assistant
- making `Take Action` safe and reliable inside real browser workflows

## Closing Perspective

Cursivis explores a specific product direction:

**What if Logitech control surfaces became an intelligent workflow layer instead of a shortcut layer?**

That is the core of the system. The cursor stops being only a pointer, and starts becoming the beginning of action.
