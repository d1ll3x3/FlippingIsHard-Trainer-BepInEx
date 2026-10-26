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
- **🔦 Help Beam in Singleplayer** - The game's multiplayer-only help beam works in solo play too, with the exact base-game rules: **hold Right-Click** to keep the beam up (release = it disappears), it can only be summoned **while touching something** (ground or wall), and **moving away from the beam breaks your phone**, just like in multiplayer. It only lifts the game's "playing solo" check, so the game's own beam logic runs unchanged, and the beams placed around the map are left alone. Toggle the feature with `N` (D-Down). Perfect for practicing the mechanic before going online (no effect in real multiplayer lobbies, where the beam already works).
- **🌐 Multiplayer Compatible** - Detects and acts only on your local/owned player (via network ownership / active camera). Teleport, fly, save and auto-repair all work in online lobbies; the repair also replicates so other players see your phone reassemble.
- **⚙️ In-Game Customizable Keybinds** - Press `B` (or `L1+R1` on a gamepad) to open the fully featured Bind Menu!
  - Remap **every** trainer bind directly inside the game, fly controls included, with **keyboard or gamepad** (incl. 2-button gamepad combos).
  - Automatically isolates inputs so you don't accidentally move while editing.
  - Auto-creates and saves to `com.flippingishard.trainer.json` in BepInEx config.
- **👻 Ghost Mode** - Turn it on with `H`: its binds then show on the HUD and work. Stand where you want and set the map's **start** (`Y`) and **end zone** (`P`; its size is in the menu's **Ghost** tab). The Teleport bind (`R`) then takes you back to the start instead of your saved position: the attempt is recorded from your first input until the phone enters the end zone, saved, and kept as the map's **PB** when it is your best. Your PB (or the ghost you pick in the tab) races you from the same moment.
  - Record (`U`), play (`I`) and spectate (`O`, the camera follows the ghost while your phone waits) any ghost. Turning ghost mode off stops them.
  - The Ghost tab lists every ghost (PB, recordings, imports) with **Export** / **Import** through the Windows file dialog. Files use the same `.fihghost` format as [FIH Ghost](https://github.com/d1ll3x3/FIH_Ghost), so ghosts move between both mods.
  - Ghosts are saved in a `Ghosts` folder next to the trainer's `.dll`. Moving the start or the end keeps the old PB in the list as a recording, and the next finished attempt is the new PB.
- **🗺️ Level & NPC Teleports** - The menu's **Teleports** tab (`LB`/`RB` on a gamepad switch tabs) takes you to the start of any story level or in front of any NPC; L10 and the Narrator land in front of the boss door. Same targets as [FIH Ghost](https://github.com/d1ll3x3/FIH_Ghost). Story map only.
- **🎮 Full Gamepad Support** - Xbox & PlayStation controllers, with the overlay/menu auto-switching to the device you're using.
- **📍 Real-time Telemetry** - HUD in the top-right corner showing current `SPEED`, `HEIGHT`, and `XYZ` position
- **🎮 Smart Overlay** - Clean, modern HUD showing your binds, active toggles, and saved position state
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
| Teleport to saved position (ghost mode: restart at the ghost start) | `R` | X | Square |
| Toggle Fly Mode              | `F`         | L3        | L3          |
| Toggle Keep Velocity         | `V`         | D-Left    | D-Left      |
| Toggle Keep Angle            | `C`         | D-Right   | D-Right     |
| Toggle Auto-Repair           | `G`         | D-Up      | D-Up        |
| Toggle Help Beam SP          | `N`         | D-Down    | D-Down      |
| Toggle Ghost Mode            | `H`         | LB + Y    | L1 + Triangle |
| Set the ghost start (ghost mode) | `Y`     | LB + D-Left | L1 + D-Left |
| Set the ghost end (ghost mode)   | `P`     | LB + B    | L1 + Circle |
| Record / stop a ghost        | `U`         | LB + D-Up | L1 + D-Up   |
| Play / stop a ghost          | `I`         | LB + D-Right | L1 + D-Right |
| Spectate / stop a ghost      | `O`         | LB + D-Down | L1 + D-Down |

All binds, including the fly controls below, are remappable in the **Keybinds Menu**, with
**2-button gamepad combos** (hold any button and press another). Click the Keyboard or Gamepad
cell of an action and press the new key/button; **right-click** a cell to clear it. On a
gamepad, navigate with the D-Pad/stick, **A/Cross** to rebind, **Y/Triangle** to clear,
**B/Circle** to cancel an assignment or close the menu.

- **Fly Mode Controls** (defaults, all remappable):
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
├── BindMenuRenderer.cs          # In-game interactive UI: keybinds, teleports and ghost tabs
├── TeleportTargets.cs           # Level and NPC teleport targets (same as FIH Ghost)
├── GhostMode.cs                 # Ghost mode: start/end, attempts and PB, record, play, spectate, export/import
├── GhostCourse.cs               # Ghost mode start and end per map (JSON)
├── GhostLibrary.cs              # The ghosts listed in the Ghost tab
├── GhostRecording.cs            # .fihghost file format (same as FIH Ghost)
├── GhostRecorder.cs             # Records the phone every physics tick
├── GhostPlayback.cs             # Plays a recording on a ghost body
├── GhostBody.cs                 # Translucent copy of the bare phone
├── LineBox.cs                   # Start marker and end zone wireframes
├── NativeFileDialog.cs          # Windows open/save dialogs for import/export
├── GameObjectFinder.cs          # Smart, lag-free local-player finder (multiplayer-aware)
├── InputHandler.cs              # Keyboard / gamepad input
├── OverlayRenderer.cs           # HUD Renderer
├── PhoneRepair.cs               # Phone auto-repair via the game's destruction FSM
├── HelpBeamUnlock.cs            # Harmony patch that lets the help beam spawn in solo play
├── TrainerTheme.cs              # Shared UI palette, rounded shapes and font
└── README.md                    # This file
```

## 🔧 Building from Source
If you want to compile the mod yourself:
1. Clone the repository.
2. Ensure you have the **.NET 6.0 SDK** installed.
3. Provide the reference assemblies in a `lib/` folder (not shipped, as they are game/Unity binaries). Copy them from your game's `BepInEx/interop` folder: `Assembly-CSharp.dll`, `Il2CppSystem.dll`, `Il2Cppmscorlib.dll`, `Unity.InputSystem.dll`, `Unity.Cinemachine.dll`, `Unity.Splines.dll`, `Unity.Mathematics.dll`, and the `UnityEngine.*Module.dll` files referenced in the `.csproj`.
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
