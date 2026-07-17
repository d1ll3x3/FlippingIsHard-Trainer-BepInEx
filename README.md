# Flipping is Hard - Practice Trainer (BepInEx Edition)

A highly optimized Unity IL2CPP BepInEx plugin for "Flipping is Hard Demo" that enables position saving/restoring and a smooth fly mode for speedrun practice. Perfect for mastering difficult sections without restarting. **Works in multiplayer** — it always targets your own (local) player.

![Trainer Overlay](https://img.shields.io/badge/Status-Working-brightgreen) ![Platform](https://img.shields.io/badge/Platform-Windows-blue)

## ✨ Features
- **📌 Advanced Position Save/Restore** - Save any position with `Shift+R`, teleport back with `R`
  - Toggle `V`: Preserve physical momentum (speed) when teleporting, or drop completely still
  - Toggle `C`: Apply preserved momentum in the original global direction, or relative to your current camera view
- **✈️ Smooth Noclip Fly Mode** - Toggle free-camera ghost flight with `F` key
  - Physics-integrated movement for buttery smooth traversal
  - Noclip enabled: Fly directly through walls, floors, and obstacles
  - Camera-relative movement with WASD / Left Stick (follows where you look)
  - Vertical movement with Space/Ctrl or RT/LT (R2/L2)
  - Speed boost with Shift or B/Circle (3x faster)
  - **Momentum conserved on exit**: velocity carries over when you leave fly mode
- **🔧 Auto-Repair on Teleport** - Toggle `G` (D-Up): when teleporting back to a saved position, if your phone is broken it is reassembled and your abilities (jump, air control, etc.) are restored. Uses the game's own destruction state machine, so it's a real repair — not just visual.
- **🔦 Help Beam in Singleplayer** - The game's multiplayer-only help beam works in solo play too, with the exact base-game rules: **hold Right-Click** to keep the beam up (release = it disappears), it can only be summoned **while touching something** (ground or wall), and **moving away from the beam breaks your phone**, just like in multiplayer. Toggle the feature with `N` (D-Down). Perfect for practicing the mechanic before going online (inactive in real multiplayer lobbies, where the native beam takes over).
- **🌐 Multiplayer Compatible** - Detects and acts only on your local/owned player (via network ownership / active camera). Teleport, fly, save and auto-repair all work in online lobbies; the repair also replicates so other players see your phone reassemble.
- **⚙️ In-Game Customizable Keybinds** - Press `B` (or `L1+R1` on a gamepad) to open the fully featured Bind Menu!
  - Remap any trainer action directly inside the game, with **keyboard or gamepad** (incl. 2-button gamepad combos).
  - Automatically isolates inputs so you don't accidentally move while editing.
  - Auto-creates and saves to `com.flippingishard.trainer.json` in BepInEx config.
- **🎮 Full Gamepad Support** - Xbox & PlayStation controllers, with the overlay/menu auto-switching to the device you're using.
- **📍 Real-time Telemetry** - HUD in the top-right corner showing current `SPEED`, `HEIGHT`, and `XYZ` position
- **🎮 Smart Overlay** - Real-time UI showing controls, active toggles, and saved position state
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

The trainer supports **keyboard and gamepad** (Xbox & PlayStation). The overlay and bind
menu **auto-switch** to show the controls of whichever device you are currently using.

**Default keybinds** (Keyboard / Gamepad Xbox / PlayStation):

| Action                       | Keyboard    | Xbox      | PlayStation |
|------------------------------|-------------|-----------|-------------|
| Open Keybinds Menu           | `B`         | LB + RB   | L1 + R1     |
| Save position                | `Shift + R` | LB + X    | L1 + Square |
| Teleport to saved position   | `R`         | X         | Square      |
| Toggle Fly Mode              | `F`         | L3        | L3          |
| Toggle Keep Velocity         | `V`         | D-Left    | D-Left      |
| Toggle Keep Angle            | `C`         | D-Right   | D-Right     |
| Toggle Auto-Repair           | `G`         | D-Up      | D-Up        |
| Toggle Help Beam SP          | `N`         | D-Down    | D-Down      |

All binds are remappable in the **Keybinds Menu**, including **2-button gamepad combos**
(hold any button and press another). Navigate the menu with the D-Pad/stick,
**A/Cross** to select, **B/Circle** to cancel an assignment or close the menu.

- **Fly Mode Controls:**
  - `W` / `S` or **Left Stick** → Move forward/backward (relative to camera)
  - `A` / `D` or **Left Stick** → Move left/right (relative to camera)
  - `Space` or **RT/R2** → Move up (world space)
  - `Ctrl` or **LT/L2** → Move down (world space)
  - **`Shift` (hold)** or **B/Circle** → Speed boost (3x faster)

## 📁 Project Structure
```text
Trainer/
├── FlippingIsHardTrainer.csproj # .NET 6 Project configuration
├── TrainerPlugin.cs             # Main BepInEx entry point
├── TrainerController.cs         # Core logic and optimization
├── TrainerConfig.cs             # Configuration and JSON save/load system
├── BindMenuRenderer.cs          # In-game interactive UI for custom keybinds
├── GameObjectFinder.cs          # Smart, lag-free local-player finder (multiplayer-aware)
├── InputHandler.cs              # Keyboard / gamepad input
├── OverlayRenderer.cs           # HUD Renderer
├── PhoneRepair.cs               # Phone auto-repair via the game's destruction FSM
└── README.md                    # This file
```

## 🔧 Building from Source
If you want to compile the mod yourself:
1. Clone the repository.
2. Ensure you have the **.NET 6.0 SDK** installed.
3. Provide the reference assemblies in a `lib/` folder (not shipped, as they are game/Unity binaries). Copy them from your game's `BepInEx/interop` folder: `Assembly-CSharp.dll`, `Il2CppSystem.dll`, `Il2Cppmscorlib.dll`, `Unity.InputSystem.dll`, and the `UnityEngine.*Module.dll` files referenced in the `.csproj`.
4. Run the command `dotnet build -c Release` in the project root.
5. The `.dll` file will be generated in the `bin/Release/net6.0/` folder.

## 🤝 Contributing
Found a bug or have an improvement? Feel free to:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## 🙏 Credits
- **Game**: "Flipping is Hard" by Elegant Horse Studios
## License

[![License: AGPL v3](https://img.shields.io/badge/License-AGPL_v3-blue.svg)](https://www.gnu.org/licenses/agpl-3.0)

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0)**.
See the [LICENSE](LICENSE) file for details.
