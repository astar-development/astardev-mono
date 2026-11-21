# 18 UI Architecture, Theming and Localisation Strategy

## Purpose

Defines CloudSync UI architecture principles for theming, localisation, culture support and future extensibility. The MVP stays simple while avoiding decisions that make future themes or languages difficult.

## Why This Matters

Internationalisation and theming are inexpensive early and expensive to retrofit. Visual appearance and language are presentation concerns, not business concerns.

## Design Principle: Adaptability

CloudSync supports multiple visual themes, multiple languages, regional formatting conventions and accessibility requirements without business logic changes.

## Theme Philosophy

- Avoid DarkMode-specific logic.
- Theme is a first-class concept.
- Controls consume theme resources, never hard-coded colours.

## Theme Architecture

Themes: Dark (MVP default), Light, Hacker, High Contrast and future community themes. Theme resources live in dedicated Avalonia resource dictionaries.

## Pink Unicorn Theme

Post-MVP, the Pink Unicorn Theme is recognised as a valid future theme. It is an architectural reminder: new themes must be addable without modifying existing UI logic.

## Trust and Accessibility

Health states never rely on colour alone. Meaning is communicated with colour, iconography and text together.

## Localisation Strategy

User-visible strings are never hard-coded in views. All text originates from localisation resources so languages can be added without changing screens.

## Resource Structure

Language resources: en-GB, en-US, es-ES, it-IT and future language packs.

## Culture Support

Language-independent date, time, number and regional formatting. Formatting is provided by a culture service, not by individual view models.

## Translation Guidance

Avoid string concatenation because grammar differs by language. Prefer resource templates and localisation parameters.

## Settings Experience

Future settings provide Theme selection, Language selection, Culture selection and a live preview.

## Architectural Rules

- User-visible text must not be hard-coded in views.
- UI colours must originate from the active theme.
- Domain and SyncEngine layers remain independent of localisation and theming.

## Relationship to Dashboard

The dashboard trust model works identically in Dark, Light, Hacker, High Contrast, Pink Unicorn and any future theme. Theme changes never alter meaning.

## Future Roadmap

- MVP: Dark theme, en-GB.
- Post-MVP: Light theme, culture abstraction, localisation framework.
- Later: Hacker theme, High Contrast theme, multilingual support, community themes, Pink Unicorn theme.

## Key Principle

CloudSync can support any future theme or language without modifications to business logic, synchronisation logic or trust-state behaviour.
