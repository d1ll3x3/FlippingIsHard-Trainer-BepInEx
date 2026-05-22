# Flipping is Hard - BepInEx Trainer

A BepInEx 6 IL2CPP plugin for "Flipping is Hard Demo" that adds position saving/restoring and smooth fly mode for speedrun practice.

![Platform](https://img.shields.io/badge/Platform-Windows-blue) ![BepInEx](https://img.shields.io/badge/BepInEx-6.0.0--be.755-green) ![Unity](https://img.shields.io/badge/Unity-6000.0.66f1-blue) ![License](https://img.shields.io/badge/License-MIT-green)

## Features

- **Position Save/Restore** — Save any position with `Shift+R`, teleport back with `R`
- **Fly Mode** — Toggle free-camera flight with `F`
  - Camera-relative WASD movement
  - Space / Ctrl for vertical movement
  - Shift for 3x speed boost
- **HUD Overlay** — Real-time controls and XYZ coordinates on screen

## Requirements

- [BepInEx 6.0.0-be.755](https://github.com/BepInEx/BepInEx/releases) (IL2CPP build)
- .NET 6.0 SDK (to build from source)
- Game: "Flipping is Hard Demo" (Steam)

## Installation

1. Install BepInEx 6 into the game folder
2. Copy `FlippingIsHardTrainer.dll` to:
   ```
   [Game Folder]\BepInEx\plugins\FlippingIsHardTrainer\
   ```
3. Launch the game — the plugin loads automatically

## Controls

| Key | Action |
|-----|--------|
| `Shift+R` | Save position |
| `R` | Teleport to saved position |
| `F` | Toggle fly mode |
| `WASD` | Move in fly mode (camera-relative) |
| `Space` | Move up |
| `Ctrl` | Move down |
| `Shift` | Speed boost (3x) |

## Building from Source

1. Run `setup-libs.bat` to copy the required DLLs from your game installation
2. Run `build.bat` to compile and deploy automatically

> The `lib/` folder is not included in the repo. You must run `setup-libs.bat` first.

## License

MIT — free for personal and educational use.

**Disclaimer**: For single-player practice only. Use responsibly.
