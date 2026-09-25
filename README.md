# NCMod - Nuclear Option Trainer & Cockpit Physics

Latest source update: **1.1.4**. See [CHANGELOG.md](CHANGELOG.md) and [Releases](https://github.com/XBarni999/NCMod/releases).

[![Game Version](https://img.shields.io/badge/Nuclear%20Option-v0.34.x-blue?style=flat-square)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.x-green?style=flat-square)](https://github.com/BepInEx/BepInEx)

**NCMod** is an all-in-one quality-of-life, procedural cockpit physics, and in-game trainer mod for **Nuclear Option**.

It introduces immersive G-force head movement, aerodynamic turbulence shake, sonic boom cockpit impulses, customizable damage feeds, HUD unclutter toggles, and host/single-player gameplay cheats (unlimited ammunition, fuel, rank & mission funds editing).

---

##  Features

### 1. Procedural Cockpit Physics & Head Movement
* **G-Force Translation & Rotation**: Your pilot's view moves dynamically under positive/negative Gs, lateral slips, and pitch/roll rates without clipping outside the cockpit.
* **Turbulence & High-Speed Shake**: High dynamic pressure, transonic buffeting, and high-G turns induce subtle, realistic vibrations.
* **Sonic Boom Impulse**: Distinct physical jolt and enhanced sonic boom audio effect inside the cockpit when breaking Mach 1.

### 2. HUD & Marker Controls (Immersion Enhancements)
* **HUD Toggle (`F8`)**: Hide screen-space UI elements while keeping functional physical cockpit MFDs and instruments running.
* **Target Markers Toggle (`F9`)**: Hide 3D floating diamond target brackets for an authentic simulator visual experience.

### 3. Real-Time Damage Feed
* On-screen combat log displaying vehicle component damage, weapon detachments, wing snaps, and engine fires.

### 4. Trainer & Cheats Menu (`Insert` / `F10`)
* **Unlimited Ammo**: Instant weapon replenish for your aircraft.
  Guns, missile launchers, and mounted weapons are refilled on the local aircraft.
* **Unlimited Fuel**: Lock internal and external fuel reserves.
* **Funds & Rank Adjuster**: Modify mission cash and pilot rank in real-time (requires single-player or host authority).

---

##  Keybindings & Controls

| Key | Action | Description |
|---|---|---|
| **`Insert`** or **`F10`** | **Toggle Menu** | Opens/closes the Trainer GUI window |
| **`F8`** | **Toggle HUD** | Toggles screen-space HUD overlay |
| **`F9`** | **Toggle Target Markers** | Toggles floating target indicators |

*(All keys are fully customizable in `BepInEx/config/ua.ncmod.nuclearoption.trainer.cfg`)*.

---

##  Installation

1. Install **[BepInEx 5](https://github.com/BepInEx/BepInEx/releases)** into your Nuclear Option root folder.
2. Download `NCMod.NuclearOptionTrainer.dll` from the latest release.
3. Place `NCMod.NuclearOptionTrainer.dll` into your `Nuclear Option/BepInEx/plugins/` directory.
4. Launch Nuclear Option. Press **`Insert`** or **`F10`** in-flight to open the menu.

---

##  Configuration (`ua.ncmod.nuclearoption.trainer.cfg`)

| Category | Setting | Default | Description |
|---|---|---|---|
| **CockpitPhysics** | `PositionStrength` | `1.0` | Translation strength from acceleration and Gs |
| **CockpitPhysics** | `RotationStrength` | `1.0` | Head pitch/roll rotation under Gs |
| **CockpitPhysics** | `ShakeStrength` | `1.0` | Turbulence, buffet, and vibration amplitude |
| **CockpitPhysics** | `SonicBoomShakeStrength` | `1.0` | Cockpit impulse when passing Mach 1 |
| **CockpitPhysics** | `SonicBoomVolumeMultiplier` | `1.8` | Cockpit sonic boom volume scale |
| **Cheats** | `UnlimitedAmmo` | `false` | Unlimited ammunition |
| **Cheats** | `UnlimitedFuel` | `false` | Unlimited fuel |
| **DamageFeed** | `Enabled` | `true` | Show part damage/detachment feed |

---

##  Building from Source

```powershell
# Build mod with .NET SDK
dotnet build NCMod.csproj -c Release
```

---

## 📜 Credits

Created by **XBarni999**.  
Developed for **Nuclear Option** by Shockfront Studios.
