# Lucid

SHIFT Game Jam, 13 to 15 Nov 2026. Theme: **Between Two Worlds**. Unity 6 (6000.0.62f1), URP.

You are a fading memory fighting through a dream that keeps flipping into a nightmare. The world swaps between **Dream** and **Nightmare**, and the way you fight swaps with it. Level 1 ends in a boss fight against the Eclipse.

**Deadline: Sunday 15 Nov, 14:45.** Submit on the SHIFT Game Jam itch.io page. Late entries are not judged.

## Open and run

1. Install Unity **6000.0.62f1** with Mac or Windows build support, URP is already set up.
2. `git lfs install`, then clone. Textures and models come through Git LFS.
3. Open the project. First import takes a few minutes.
4. Menu **Lucid > Build Props And Test Scene**. It generates every prefab, material and the scene `Assets/Scenes/Level1.unity`. Re-run it any time you change a builder script.
5. Open `Level1.unity` and press Play. Click the Game view first (the mouse is locked, Esc releases it).

## Controls

| Input | Dream | Nightmare |
|---|---|---|
| WASD | move | move |
| Mouse | look | look and aim (shards bend toward the nearest enemy) |
| Left mouse (hold) | strike (the boss is armoured, except in the wake window) | fire a stream of shards; shoot Hunter Orbs and meteors for Power |
| Right mouse (hold, release) | raise the shield and absorb seeds (Power, Resonance); release to cast a burst | charge and release a heavy burst |
| Shift | dash (brief invulnerability, chain dashes for speed) | same |
| Space | jump (hops ground shockwaves) | same |
| Q | shift world (during the 1.2 s swap warning: +3 Power) | same |
| E | at Resonance x5: Lucid Pulse. In the finale: bind the Sleeper with full Power | |
| Tab | focus the boss | |
| R after defeat/victory | retry | retry |
| `[` and `]` | test only: lower and raise memory (health) | |

Debug keys (editor and development builds only; pressing one before the fight drops you into the arena):

| Key | Action |
|---|---|
| 1, 2, 3 | jump to boss phase |
| N | force a world swap |
| T | force a Sleepwalk teleport |
| C | cycle through every attack one by one (the spec says A, but A strafes) |
| K | kill the boss |
| H | toggle hitbox drawing (turn Gizmos on in the Game view) |

## Where things are

| Path | What |
|---|---|
| `Assets/Scripts/` | Runtime code. `WorldState` is the Dream/Nightmare switch, `WorldObject` and `WorldMaterialSwap` react to it, `WorldLook` blends sky, fog and light. |
| `Assets/Scripts/PlayerController.cs` | Flow movement, dash chain, recall (flow back when you fall) and damage. |
| `Assets/Scripts/PlayerCombat.cs` | Shoot in Nightmare, shield in Dream. `Projectile`, `Enemy`, `WispEnemy`, `EnemyBullet` are the combat pieces. |
| `Assets/Scripts/MemoryAvatar.cs` | The player visual. A figure of light points animated in the shader (`Assets/Shaders/MemoryFigure.shader`). |
| `Assets/Editor/` | Builder tools. They create prefabs, materials and the level from code. See the table below. |
| `Assets/Resources/Combat/` | Combat prefabs loaded at runtime by name through `CombatAssets`. |
| `Assets/ThirdParty/PolyHaven/` | CC0 models and textures. Credited in `CREDITS.md`. |
| `docs/FreeAssetsReport.md` | Research on free assets to use next. |
| `LUCID_BRIEF.md` | Original design brief and event rules. Parts of it are out of date, see "Current state" below. |

### Builders (menu Lucid > Build Props And Test Scene)

`PropBuilder` runs the others in order: `NatureBuilder` (Poly Haven to URP prefabs and materials), `CombatBuilder` (shard, enemy orb, wisp, shield, particle burst), `DreamBuilder` (wisteria tunnel, light shafts, mirror pools, petals), `NightmareBuilder` (tendrils, towers, ash), `MemoryBuilder` (the player figure), then the scene. `LevelCombat` places the enemies and HUD.

## Current state (Fri night)

Working: Dream and Nightmare look, world swap, flow movement, dash, recall, Nightmare shooting, Dream shield and absorb, wisp enemies, animated memory figure.
Gameplay implementation added: the Eclipse encounter, Power burst, timed-shift rewards, defeat/victory and R to retry. C# compilation checked; gameplay balance and visuals still require playtesting. Sound/music, a main menu and the itch.io build remain unfinished.
Known problems: the player figure and wisps over-brighten under bloom, the Dream still lacks painterly trees, white ruins and a distant giant creature, Nightmare ground reads pale, the wisteria arches are heavy on the GPU. Combat feel has not been tuned by hand yet. Feel values are public fields on `PlayerController`, `PlayerCombat`, `ThirdPersonCamera`, `WispEnemy`.

## Boss fight

The Sleeper has 900 health, three phases, 18 attacks across ground, air, sky and homing lanes, and a Sleepwalk teleport with a wake window. Dream has Resonance, Lucid Pulse and gold seeds. Nightmare has a damage multiplier and Dread. All tunables are in `Assets/Resources/BossAttackData.asset` (menu **Lucid > Build Boss Attack Data**). Full list: [BOSS_ATTACKS.md](BOSS_ATTACKS.md).

## Game design in one paragraph

The saved Level1 HUD installs `EclipseEncounter` at runtime; no scenery rebuild is needed for these mechanics. Approach the black sun near the end of the path (z=142) to engage. Dream fires rotating absorbable rings; Nightmare fires aimed fans. Below half health, attacks and world swaps accelerate. A 1.2-second warning names the next world; manually shifting into it during the warning grants +3 Power and clears orbs within 7 units, once per warning. At 10 Power, E in Nightmare releases a 42-unit pulse for 45 damage. Dream boss armor reduces damage to 15%. These are initial tuning values, not playtested balance. Retry reloads Level1, which is now registered in Build Settings.

Dream: slow, beautiful, safe. Enemy orbs are slow rings you absorb with a shield, which charges Power and heals. Nightmare: fast and loud. Enemy fire is quick and aimed, you shoot back, and the boss is exposed. The boss flips the world on a timer, so you absorb in Dream and strike in Nightmare. Original brief: [LUCID_BRIEF.md](LUCID_BRIEF.md). The puzzle idea was scrapped to focus on combat, movement feel and the boss.

## Team rules

- Unity: Asset Serialization = Force Text, Version Control = Visible Meta Files. Commit every `.meta`.
- Generated assets come from the builders. Change the builder, not the generated prefab, or your change is overwritten on the next build.
- One person per scene at a time. Pull before you start, push small and often.
- Add every outside asset or tool to [CREDITS.md](CREDITS.md) the moment you use it. The itch.io submission needs the list.
- Never commit the event deck PDF (it contains Wi-Fi passwords). It is in `.gitignore`.
