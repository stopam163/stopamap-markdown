# Roadmap

This roadmap is directional, not a promise of dates.

## 0.1.x — Core stability

- Stabilize block and inline models.
- Expand regression tests for CRLF, tables, nested emphasis, references.
- Document coordinate invariants.
- Keep the library platform-independent.

## 0.2.x — Parser coverage

- More CommonMark edge cases: setext headings, nested quotes, escaped punctuation.
- Better table alignment metadata.
- Optional strict/relaxed parsing modes.
- More reference and footnote cases.

## 0.3.x — Performance

- Benchmark parse and live-document creation.
- Reduce allocations in inline parsing.
- Add a large-document performance budget to CI.

## 0.4.x — Integrations

- Publish a minimal HTML renderer example.
- Add an Avalonia adapter sample without app-specific services.
- Document caret, selection, and IME integration recipes.

## 1.0.0 — API commitment

- Freeze the public API.
- Multi-target `net8.0` and later if demand justifies it.
- Publish a migration guide for any pre-1.0 changes.

## Out of scope

- Full HTML sanitization.
- Image decoding and storage.
- Theme systems and application settings.
- A complete desktop editor application.
