# Product

## Register

product

## Users

FHIR implementers, IG authors, and interoperability engineers who compare national or project-specific Implementation Guides (US Core, NL Core, UK Core, etc.). They work locally with Firely Terminal, already have IG packages on disk, and use Chrome or Edge during focused analysis sessions.

## Product Purpose

Compare multiple FHIR IG packages side by side without uploading data. Users pick a local folder of Firely-prepared IG subfolders, review matched resources by canonical URL, and drill into StructureDefinition element differences. Success means faster, trustworthy diff review during IG alignment or migration work.

## Brand Personality

Competent, direct, technical. Reads like good internal tooling documentation: precise labels, no marketing fluff, respects the user's expertise while still teaching the one non-obvious setup step (folder layout).

## Anti-references

- Generic healthcare landing pages (white + teal, stock medical imagery)
- SaaS onboarding wizards with illustrated step cards
- Hero metrics, gradient accents, decorative empty states
- Modals for information that belongs inline

## Design Principles

1. **Teach the setup, then get out of the way.** Onboarding explains Firely folder layout once; comparison UI takes priority after load.
2. **Local-first trust.** Always reinforce that files never leave the browser.
3. **Familiar tool patterns.** Documentation-style panels, monospace for paths and commands, semantic badges for status.
4. **Density when it earns it.** Tables and code blocks are fine; prose stays short.
5. **Chromium honesty.** State browser requirement plainly, not buried in a footnote.

## Accessibility & Inclusion

WCAG AA target. Keyboard-accessible collapsible sections, visible focus states, sufficient contrast on code blocks and badges. Respect `prefers-reduced-motion` for any expand/collapse animation.
