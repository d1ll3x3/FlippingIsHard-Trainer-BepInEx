# Flipping is Hard - Practice Trainer (BepInEx Edition)

A highly optimized Unity IL2CPP BepInEx plugin for "Flipping is Hard Demo" that enables position saving/restoring and a smooth fly mode for speedrun practice. Perfect for mastering difficult sections without restarting.

![Trainer Overlay](https://img.shields.io/badge/Status-Working-brightgreen) ![Platform](https://img.shields.io/badge/Platform-Windows-blue) ![License](https://img.shields.io/badge/License-MIT-green)

## ✨ Features
- **📌 Position Save/Restore** - Save any position with `Shift+R`, teleport back with `R`
- **✈️ Smooth Fly Mode** - Toggle free-camera flight with `F` key
  - Camera-relative movement with WASD (follows where you look)
  - Vertical movement with Space/Ctrl
  - Speed boost with Shift (3x faster)
  - No gravity - stay in the air indefinitely
  - Smooth, jitter-free movement
- **📍 Real-time Coordinates** - Functional HUD in the top-right corner showing current `HEIGHT` and `XYZ` position
- **🎮 Smart Overlay** - Real-time HUD showing controls, fly mode status, and saved position
- **⚡ Ultra Optimized** - Rewritten from scratch to eliminate lag spikes and stuttering during gameplay.

## 🚀 Quick Start

### 1. **Requirements**
- [Flipping is Hard Demo](https://store.steampowered.com/app/4111020/Flipping_is_Hard/) (Steam).
- [BepInEx 6 (IL2CPP)](https://github.com/BepInEx/BepInEx/releases) installed in your game directory.

### 2. **Installation**
1. Download the latest `FlippingIsHardTrainer.dll` from the Releases tab.
2. Place the `.dll` file into your `BepInEx/plugins/` folder.
3. Start the game.

### 3. **Use In-Game**
- **`Shift + R`** → Save current position & rotation
- **`R`** → Teleport to saved position (resets physics)
- **`F`** → Toggle Fly Mode ON/OFF
- **Fly Mode Controls:**
  - `W` / `S` → Move forward/backward (relative to camera)
  - `A` / `D` → Move left/right (relative to camera)
  - `Space` → Move up (world space)
  - `Ctrl` → Move down (world space)
  - **`Shift` (hold)** → Speed boost (3x faster)

## 📁 Project Structure
```text
Trainer/
├── FlippingIsHardTrainer.csproj # .NET 6 Project configuration
├── TrainerPlugin.cs             # Main BepInEx entry point
├── TrainerController.cs         # Core logic and optimization
├── GameObjectFinder.cs          # Smart and lag-free player finder
├── InputHandler.cs              # Keyboard hook
├── OverlayRenderer.cs           # HUD Renderer
└── README.md                    # This file
```

## 🔧 Building from Source
If you want to compile the mod yourself:
1. Clone the repository.
2. Ensure you have the **.NET 6.0 SDK** installed.
3. Run the command `dotnet build -c Release` in the project root.
4. The `.dll` file will be generated in the `bin/Release/net6.0/` folder.

## 🤝 Contributing
Found a bug or have an improvement? Feel free to:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## 📄 License
MIT License - See [LICENSE](LICENSE) file for details.

Free for personal, educational, and non-commercial use.

## 🙏 Credits
- **Game**: "Flipping is Hard" by Elegant Horse Studios
- **Disclaimer**: This tool is for educational purposes and single-player practice only. Use responsibly.