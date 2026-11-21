# 24 Provider Management UX

Provider-agnostic cloud account experience.

## Purpose

Defines the complete user experience for discovery, onboarding, management, monitoring and removal of cloud storage providers while preserving user trust and a provider-agnostic architecture.

## Goals

- A consistent experience across OneDrive, Google Drive and future providers.
- Clear operational state.
- Hide provider-specific complexity where possible.

## Non-Goals

- Not a file browser replacement.
- Not a provider-specific management console.
- Not a credential management product.

## UX Principles

- Trust before convenience
- Never surprise the user
- Visibility over automation
- Safe defaults
- Provider neutrality
- Undo and recovery where possible

## Information Architecture

Navigation follows document 19. Provider management lives on the Providers screen. This document's original list (which named Sync Relationships and omitted Queue and History) is superseded by document 19.

## Provider Management Screen

- Connected Providers panel
- Provider Health summary
- Authentication state
- Storage consumption
- Recent activity
- Quick actions

## Provider Card Design

- Provider branding
- Account identity
- Connection status badge
- Last sync timestamp
- Health indicator
- Actions: Manage, Pause, Reconnect, Remove

## Provider Health Model

Per-provider health, distinct from the overall SyncHealth in document 17:

```text
ProviderHealth = Healthy | Syncing | Warning | Error | AuthenticationRequired | Offline
```

## Add Provider Journey

1. Select provider
2. Authenticate
3. Grant permissions
4. Verify access
5. Choose sync root
6. Review configuration
7. Activate provider

## Multi-Account UX

- Allow multiple accounts
- Clear account naming
- Visual distinction between accounts
- Independent health visibility

## Provider Capabilities

Selective sync, delta synchronisation, file versioning, recycle bin support and shared content support.

## Error UX

- Actionable messages
- Retry guidance
- Diagnostic links
- Correlation identifiers

## Authentication Renewal

- Persistent warnings
- Non-blocking reminders
- Fast re-authentication flow

## Removal UX

- Disconnect without deletion
- Optional local cleanup
- Explicit confirmation
- Recovery guidance

## Accessibility

Keyboard navigation, screen reader support, colour-independent status indicators.

## Localisation

Culture-aware formatting, resource-based strings, RTL readiness.

## Observability Integration

Provider lifecycle events, health metrics, authentication metrics and performance telemetry.

## Wireframe Concept

```text
Providers
[+] Add Provider
--------------------------------
OneDrive Personal  | Healthy
Google Drive       | Syncing
Dropbox            | Warning
--------------------------------
```

## Architecture Requirements

- Provider abstraction layer
- Capability discovery
- Independent provider plugins
- Provider metadata registry

## Acceptance Criteria

- A new provider can be added without UI redesign.
- All provider states are visible.
- The removal flow prevents accidental loss.
- The health model is consistent across providers.

## Architecture Decision

The CloudSync UI interacts exclusively with provider abstractions rather than provider implementations. Capability metadata drives UI behaviour and enables future provider onboarding without redesigning core screens.
