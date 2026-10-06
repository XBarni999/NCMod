# Changelog

## 1.2.0 Р Р†Р вЂљРІР‚Сњ test build, 2026-10-06

- Fix pilot death after air start by rebasing each living pilot's velocity history alongside the aircraft, preventing artificial lethal G-force damage from the speed discontinuity. Normal G-force damage remains active.
- Fix aircraft destruction during air starts: move all registered attached part bodies, including unparented jointed AeroParts, together before physics simulation. Clear interpolation across the jump and reject joints to external loads.
- Fix collapsed menu content; default to 640 Р“вЂ” 520 px, support title-bar dragging and bottom-right resizing, and save the selected dimensions.
- Add a dark tabbed trainer menu with a scrollable body and automatic cursor handling.
- Add current-aircraft air starts at 500, 1000, 2000 and 5000 m above local terrain, plus an optional automatic preset for newly entered aircraft.
- Select starting airspeed by rotorcraft, propeller / propfan or jet class and aircraft takeoff, approach and maximum speed parameters.
- Remove camera translation and pitch from thrust / braking, suppress the vanilla acceleration spring and acceleration-derived jerk shake, and preserve native combat / aerodynamic shake.
- Add gentle turn following and transonic buffet; fix Mach 1 crossing detection and offset feedback into native free-look smoothing.
- Sample acceleration against physics time and remove per-frame engine polling and its component scan.

Validation: Release build against Nuclear Option 0.34.1 passes. Menu appearance, heavy-load air starts, rotorcraft controls, camera comfort, nearby explosions and multiplayer behavior require in-game testing. NOMNOM manifest and public release follow user flight validation.
