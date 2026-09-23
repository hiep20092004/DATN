# Changelog

## [Unreleased]

### Added
- Add Classic gameplay setup tools, Home and Level Editor scenes, and service configurations.
- Add gameplay and UI art, prefabs, materials, shaders, and level content.

### Changed
- Translate revive popup titles, descriptions, time bonuses, peek hint, and setup defaults into Vietnamese.
- Simplify the level flow, boosters, and gameplay UI for Classic mode.
- Use a local clock initialized safely when services first request the time.

### Removed
- Remove the one-time Classic scene, popup, and material setup Editor tools after generating their assets.
- Remove legacy container, hidden, shutter, moving-gate, and obstacle-unlock systems.

### Fixed
- Cancel panel animations when their objects are destroyed.
- Read the active scene when resolving the current placement and reject transitions to unavailable scenes.
- Stop the power-up tutorial arrow tween when the tutorial closes or its popup is disabled or destroyed, preventing access to a destroyed RectTransform.
- Fix the settings vibration toggle throwing a null reference exception by using the Nice Vibrations playback preference.
