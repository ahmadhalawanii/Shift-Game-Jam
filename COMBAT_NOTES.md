# Combat notes

Stages C/D extend the supplied Stage A/B foundation. Character, dash, weapon damage/ammo/reload balance and free aiming are retained.

## Controls (from `Assets/Scripts/LucidInput.cs`, the only place bindings live)

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move / look | WASD / mouse | left stick / right stick |
| Dream: hold 0.6 s, release Lumen Bow. Nightmare: fire | Left mouse | right trigger |
| Nightmare: aim. Dream: Ward / timed Perfect Catch | Right mouse | left trigger |
| Smoke dash | Left Shift | A |
| Jump | Space | D-pad down |
| Swap stance | Q | left bumper |
| Reload (retry after defeat or victory) | R | X |
| Dream: Lucid Pulse. Nightmare: Burst | E | B |
| Dream: Bloom. Nightmare: Shadowstep | Left Ctrl | Y |
| Overdrive | F | D-pad up |
| Weapon select | 1, 2, 3, wheel, mouse button 4 | right bumper, D-pad left and right |
| Swap shoulder | C | right stick click |
| Pause | Esc | Start |
| Recenter on boss | — | left stick click |

Esc > Controls lists current bindings, supports rebinding, invert Y and hold/toggle aim/Ward. Overrides persist locally. Sensitivity is under Settings. HUD follows the active device and rebindings.
Debug: F5/F6/F7 phases, V attack cycle, F8 DPS dummy. Existing debug shortcuts remain.

## Tuning values

| Value | Where | Now |
|---|---|---|
| Dash distance / time | `PlayerController` | 6.5 m in 0.2 s |
| Dash charges / recharge | `PlayerController` | 2, 2.5 s each |
| Invulnerable after reforming | `PlayerController.dashGrace` | 0.1 s |
| First shot after a dash | `PlayerController.dashShotBonus`, `dashShotWindow` | x1.25 within 0.6 s |
| Burning cloud (Nightmare, not the boss) | `SmokeDash` | 8 damage per second, 2.2 m, 1 s |
| Dash FOV kick / edge blur | `SmokeDash.fovKick`, `edgeBlur` | 7 degrees / 1 |
| Aim camera offset | `ThirdPersonCamera` | 0.6 right, 1.7 up, 2.2 back, FOV 57, 0.2 s blend |
| Input buffer | `LucidInput.Buffer` | 0.12 s |
| Look | `LucidInput` | sensitivity 0.12, invertY, aimToggle |

Combat tuning: `Assets/Resources/BalanceSheet.asset`; attack patterns and existing swap cadence: `BossAttackData.asset`.

- Ward 100, drain 8–20, break 2.5 s, lowered regeneration 10/s after 1 s; Dream dash mist restores Ward for 1 s.
- Six explicit pink seeds per Dream window: 4 HP each, Perfect Catch 8 within 0.15 s, healing capped at 25/window. Each supplies 6 Ward and 5 Power.
- Bow draw 0.6 s, interval 1.4 s; three tags, lifetime 8 s, re-tag lockout 4 s. Nightmare cash-in x1.6 and +10 Power.
- Pulse 10 s; Bloom 14 s with 6 s lifetime. Bloom attracts pink seeds and distributes their healing at 2 HP/s; it cannot generate free health.
- Burst 40 Power / 25 s / 1 s stagger; Shadowstep 20 Power / 6 s / next shot x1.5; Overdrive full Dread / 45 s / once per phase / 5 s / x1.35 damage.
- Silent Swap Beat uses Open/Held Breath mixer snapshots, a 0.5 s ambience duck, heartbeat, slowed particles and temporary grading. Player cues stay audible; swap timing is unchanged.

- Checkpoints restore health, Ward, dash charges, and ammo. Reload once with R or X. Reserve ammo is unlimited throughout the game.
- Normal damage can defeat the boss in its final phase and trigger the ending.

## Balance and verification

F8 baseline burst/sustained DPS: Revolver 42 / 22.7; Scatter 40 / 18.2 at point blank (10 / 4.5 at 9 m); Chain 22 / 12.8 including overheat. Revolver rewards weak-point accuracy, Scatter proximity, Chain sustained tracking without ammo.

Stage C Play check passed. Final input-driven Play probe: 13/13 checks passed (Perfect Catch +8 HP/ammo, cyan drain, Ward break/recovery, pink-safe Pulse, seed-fed Bloom, charged bow tag, weapon cash-in, Burst, Shadowstep, Overdrive, former finisher before its removal). Compilation and runtime console were clean. Asset/GUID checker: 606 files, zero errors/warnings. A 400 stationary-bullet Editor sample at 880×377 averaged 5.90 ms/frame; this does not certify a standalone build, moving attack patterns, or dash VFX under peak load.

Full 4–6 / 6–8 minute fight targets and the under-45-second combat gate have not been validated with skilled/average human runs. Existing Stage B balance and world-swap cadence were not retuned. No standalone build has been verified in this pass.

## Cut or changed so far

- Dash chains are gone (replaced by the two charges).
- The old Dream strike/charged burst and non-pink healing are replaced by Bow/Ward/Pulse/Bloom. Wells now emit catchable pink seeds.
- Dash petals are plain glowing motes. Body-to-smoke uses a shader dissolve plus particle bursts, not VFX Graph mesh sampling (the package is not installed).
- Reload and death poses exist in the shader; reload is driven from stage B.
- Stage B free aiming is retained on keyboard and gamepad; no new aim assist was added. Audio remains synthesized. Bloom is a procedural glowing blossom, not an imported asset.

## 60-second demo route

0–10 s: Play, skip intro, smoke-dash in the glade. 10–25 s: B to boss entrance, catch pink with Ward (tap just before contact for PERFECT), then charge/release the Bow at a sigil. 25–40 s: Q to Nightmare, switch weapons with 1/2/3 or bumper, shoot the tagged sigil, reload. 40–50 s: E Burst or Left Ctrl Shadowstep with enough Power. 50–60 s: finish the boss with regular attacks to trigger the ending.
