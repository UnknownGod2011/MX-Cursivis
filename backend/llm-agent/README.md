# Cursivis Reasoning Backend

## Purpose

Provide structured reasoning, transformation, transcription, and browser-planning endpoints for the Logitech-first Cursivis workflow.

The current implementation is provider-backed, but the product story is broader: this backend is the reasoning layer behind Cursivis, not the headline.

## Current Responsibilities

- analyze text, image, and text+image selections
- process spoken command refinement
- rank actions for Guided mode
- choose the most useful action for Smart mode
- produce structured browser action plans for `Take Action`

## Endpoints

- `GET /health`
- `POST /analyze`
- `POST /api/intent`
- `POST /suggest-actions`
- `POST /transcribe`
- `POST /plan-browser-action`
- `WS /live`

## Notes

- `/analyze` is the main execution path
- `/suggest-actions` powers Guided mode
- `/transcribe` supports hold-to-talk
- `/plan-browser-action` converts generated results into structured browser steps

## Local Run

```powershell
cd backend/llm-agent
npm install
npm start
```

Default port:

- `8080`

## Environment

Current implementation env:

- `GOOGLE_API_KEY`
- `GOOGLE_API_KEYS` for an optional fallback key pool
- `GEMINI_MODEL`
- `GEMINI_LIVE_MODEL`
- `GEMINI_ROUTER_MODEL`
- `GEMINI_OPTIONS_MODEL`

The backend runs with a single key by default and can also rotate across a configured key pool for more reliable uninterrupted sessions.

Even though the current repository includes a provider-specific implementation today, the product framing should be understood as **Cursivis-first and Logitech-first**.
