&#x20;&#x20;

# NecroLensDI (Dependency Injection Fork)

**NecroLensDI** enhances the original [NecroLens](https://github.com/Jukkales/NecroLens) plugin by implementing Dependency Injection (DI) for better modularity, maintainability, and testability. It is fully compatible with **game version 7.2**.

NecroLensDI allows you to explore Deep Dungeons (Palace of the Dead, Heaven on High, Eureka Orthos) with augmented reality-like visuals, providing enhanced spatial awareness and game information directly in your viewport.

---

## Key Enhancements

- **Dependency Injection Implementation**: Streamlined codebase enabling easier testing and future development.
- **Compatibility Updates**: Fully functional with game version 7.2, resolving compatibility issues from the original plugin.

---

## Installation

For installation instructions, please see the [DalamudPlugins](https://github.com/mariamatthews/DalamudPlugins).

---

## Features

- **Enhanced ESP (Extrasensory Perception):**
  - **Proximity mobs:** Clearly visible aggro zones.
  - **Sight mobs:** 90° front-facing visual cones showing detection zones.
  - **Sound mobs:** Alerts triggered when running nearby.
  - **Patrol indicators:** Movement directions clearly marked.
- **Dungeon Object Detection:**
  - Identification of chests, exits, returns, and key objects.
  - Invisible objects (traps, hidden chests) remain server-side.
- **Interactive Highlights:** Automatic highlighting of nearby objects.
- **Timers:** Built-in floor and respawn timers.
- **Pomander Tracking:** Tracking of static floor effects and auto-opening of safe chests.

---

## Usage

NecroLensDI activates automatically in supported duties:

- **Palace of the Dead**
- **Heaven on High**
- **Eureka Orthos**

An interface opens automatically when you enter these dungeons. If the interface closes accidentally, reopen it using:

```sh
/necrolens
```

---

## Future Roadmap

- Improved mob behavior and movement predictions
- Enhanced accuracy of aggro ranges
- Enemy AoE radar
- Automatic Pomander usage
- Trap and hoard location logging

---

### Acknowledgments

This fork is based on the original [NecroLens](https://github.com/Jukkales/NecroLens) project by **Jukkales**. Special thanks to Leonhart for the original icon design.

All modifications focus on Dependency Injection and compatibility updates for game version 7.2.

