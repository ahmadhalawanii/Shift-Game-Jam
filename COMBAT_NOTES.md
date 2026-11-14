# Combat notes

Combat redesign, built in stages A to D. Stage A (core feel) is done.

## Controls (from `Assets/Scripts/LucidInput.cs`, the only place bindings live)

| Action | Keyboard and mouse | Gamepad |
|---|---|---|
| Move / look | WASD / mouse | left stick / right stick |
| Fire | Left mouse | right trigger |
| Nightmare: aim over shoulder. Dream: shield (Ward in stage C) | Right mouse (hold) | left trigger |
| Smoke dash | Space | A |
| Jump (traversal zones; not in the design table) | Left Ctrl | D-pad down |
| Swap stance | Left Shift | left bumper |
| Reload (retry after defeat or victory) | R | X |
| Ability 1 | E | B |
| Ability 2 | Q | Y |
| Overdrive | F | D-pad up |
| Weapon select | 1, 2, 3, wheel, mouse button 4 | right bumper, D-pad left and right |
| Swap shoulder | C | right stick click |
| Pause | Esc | Start |

Reload, abilities, Overdrive, weapon select and pause are bound but do nothing until stages B to D.
Debug keys moved off the combat keys: boss phase F5, F6, F7; cycle attacks V.

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
| Input buffer | `LucidInput.Buffer` | 0.12 s (dash now; reload and abilities later) |
| Look | `LucidInput` | sensitivity 0.12, invertY, aimToggle |

These move into ScriptableObjects (`BalanceSheet`) in stage B.

## Cut or changed so far

- Dash chains are gone (replaced by the two charges).
- The Nightmare heavy burst on right mouse is gone: right mouse aims there. Dream still has the old shield and burst until stage C.
- The Dream dash mist is visual only until the Ward exists (stage C).
- Dash petals are plain glowing motes. Body-to-smoke uses a shader dissolve plus particle bursts, not VFX Graph mesh sampling (the package is not installed).
- Reload and death poses exist in the shader; reload is driven from stage B.
