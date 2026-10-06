# NCMod - Nuclear Option Trainer & Cockpit Physics

Latest source update: **1.2.0 test build** (2026-10-06). In-game validation is pending; no new release has been published. See [CHANGELOG.md](CHANGELOG.md) and [Releases](https://github.com/XBarni999/NCMod/releases).

[![Game Version](https://img.shields.io/badge/Nuclear%20Option-v0.34.x-blue?style=flat-square)](https://store.steampowered.com/app/2168680/Nuclear_Option/)
[![BepInEx](https://img.shields.io/badge/BepInEx-5.x-green?style=flat-square)](https://github.com/BepInEx/BepInEx)

**NCMod** is an all-in-one quality-of-life, procedural cockpit physics, and in-game trainer mod for **Nuclear Option**.

It introduces immersive G-force head movement, aerodynamic turbulence shake, sonic boom cockpit impulses, customizable damage feeds, HUD unclutter toggles, and host/single-player gameplay cheats (unlimited ammunition, fuel, rank & mission funds editing).

---

##  Features

### 1. Procedural Cockpit Physics & Head Movement
* **Calm acceleration and braking**: Longitudinal acceleration is excluded from head movement. The vanilla acceleration spring and jerk shake are suppressed while NCMod camera physics is enabled. Native damage, blast, collision and aerodynamic shake remain active.
* **Turn following**: Smooth, limited head rotation follows yaw and pitch without accumulating into the free-look pose.
* **G-Force Translation & Rotation**: Your pilot's view moves dynamically under positive/negative Gs, lateral slips, and pitch/roll rates without clipping outside the cockpit.
* **Turbulence & High-Speed Shake**: Native aerodynamic shake, a narrow transonic buffet band and high-G turns create subtle vibrations. Acceleration sampling uses physics time instead of render-frame time; engines are no longer scanned or polled for camera effects.
* **Sonic Boom Impulse**: Distinct physical jolt and enhanced sonic boom audio effect inside the cockpit when breaking Mach 1.

### 2. HUD & Marker Controls (Immersion Enhancements)
* **HUD Toggle (`F8`)**: Hide screen-space UI elements while keeping functional physical cockpit MFDs and instruments running.
* **Target Markers Toggle (`F9`)**: Hide 3D floating diamond target brackets for an authentic simulator visual experience.

### 3. Real-Time Damage Feed
* On-screen combat log displaying vehicle component damage, weapon detachments, wing snaps, and engine fires.

### 4. Trainer & Cheats Menu (`Insert` / `F10`)
The dark, draggable panel has Flight, Camera / HUD and Funds / Rank tabs, a 640 × 520 px default size, scrolling for small screens, and a cursor that unlocks while the menu is open.

Drag the title bar to move the window. Drag **◢** in its bottom-right corner to resize it; the chosen size is saved in the BepInEx config under `Menu.Width` / `Menu.Height`.

* **Air start**: Move your current aircraft into level flight at **500, 1000, 2000 or 5000 metres above local ground / sea level**. Keeps your airframe and loadout; requires single-player or host authority. Choose a height under **On next aircraft spawn** to apply automatically after entering each new aircraft, or choose **Off** (default).
* **Airframe-aware speed**: Rotorcraft start at 40 m/s (144 km/h), propeller / propfan aircraft at 100 m/s (360 km/h), jets at 180 m/s (648 km/h). Fixed-wing speed is raised to at least 1.35 times the aircraft's takeoff / approach speed and capped at 80% of its design maximum. The aircraft is levelled, engines enabled, brakes released and gear retracted. These starting presets still need flight testing with heavy loads and at 5000 m.

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
| **AirStart** | `Altitude` | `0` | Automatic air-start height: 0 off; 500/1000/2000/5000 m |
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

## рџ“њ Credits

Created by **XBarni999**.  
Developed for **Nuclear Option** by Shockfront Studios.
[original Torpedo mod by SonPamungkas](https://github.com/SonPamungkas/torpedo).
Its original code and four torpedo variants are credited to SonPamungkas.
