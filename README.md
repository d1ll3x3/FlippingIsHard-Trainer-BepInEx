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

- [BepInEx 6.0.0-be.755](https://github.com/BepInEx/BepInEx/releases) (IL2CPP build) installed in the game folder
- .NET 6.0 SDK — [download](https://dotnet.microsoft.com/download/dotnet/6.0)
- Game: "Flipping is Hard Demo" (only tested in V0.11.008 & V0.9.15)

## Installation (pre-built)

1. Download `FlippingIsHardTrainer.dll` from [Releases](../../releases)
2. Copy it to:
   ```
   [Game Folder]\BepInEx\plugins\FlippingIsHardTrainer\FlippingIsHardTrainer.dll
   ```
   Create the `FlippingIsHardTrainer` folder if it doesn't exist.
3. Launch the game — the plugin loads automatically

## Controls

| Key | Action |
|-----|--------|
| `Shift+R` | Save current position |
| `R` | Teleport to saved position |
| `F` | Toggle fly mode ON/OFF |
| `WASD` | Move in fly mode (camera-relative) |
| `Space` | Move up |
| `Ctrl` | Move down |
| `Shift` | Speed boost (3x) |

## Building from Source

### Prerequisites

- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- BepInEx 6 installed in the game folder
- The game launched at least once with BepInEx (generates the `interop/` folder)

### Steps

**1. Clone the repo**
```bash
git clone https://github.com/d1ll3x3/FlippingIsHard-Trainer-BepInEx.git
cd FlippingIsHard-Trainer-BepInEx
```

**2. Set up dependencies**

Run the setup script — it auto-detects your Steam library and copies the required DLLs from the game into `lib/`:
```bat
setup-libs.bat
```

> If your Steam library is in a non-standard location, open `setup-libs.bat` and set `GAME_PATH` manually at the top.

**3. Build and deploy**
```bat
build.bat
```

This compiles the plugin and copies the DLL directly to your game's plugin folder.

Alternatively, build manually:
```bash
dotnet build -c Debug
```
Output: `bin\Debug\net6.0\FlippingIsHardTrainer.dll`

Then copy it to `[Game Folder]\BepInEx\plugins\FlippingIsHardTrainer\`.



## License

MIT — free for personal and educational use.

**Disclaimer**: For single-player practice only. Use responsibly.
