# Lucid

SHIFT Game Jam, 13 to 15 Nov 2026. Theme: **Between Two Worlds**. Unity 6 (6000.0.62f1), URP.

You are a fading memory fighting through a dream that keeps flipping into a nightmare. The world swaps between **Dream** and **Nightmare**, and the way you fight swaps with it. Level 1 ends in a boss fight against the Eclipse.

**Deadline: Sunday 15 Nov, 14:45.** Submit on the SHIFT Game Jam itch.io page. Late entries are not judged.

## Open and run

1. Install Unity **6000.0.62f1** with Mac or Windows build support, URP is already set up.
2. `git lfs install`, then clone. Textures and models come through Git LFS.
3. Open the project. First import takes a few minutes.
4. Open `Assets/Lucid/World/Scenes/LucidWorld.unity` and press Play. Click the Game view first (the mouse is locked, Esc releases it). This is the scene in Build Settings.
5. To change the level, edit the builders in `Assets/Editor/World/` and run menu **Lucid > Build Lucid World**. It regenerates the kit (meshes, materials, prefabs) and the scene in about 20 seconds. **Lucid > Build Lucid World (scene only)** skips the kit.

The old Level1 and its Poly Haven assets were removed (see git history). The combat prefabs in `Assets/Resources/Combat/` and the player figure come from **Lucid > Build Combat And Player Props**.

## Controls

| Input | Dream | Nightmare |
|---|---|---|
| WASD | move | move |
| Mouse | look (in the boss fight: small nudge only, see below) | look and aim (shards bend toward the nearest enemy) |
| Left mouse (hold) | strike (the boss is armoured, except in the wake window) | fire a stream of shards; shoot Hunter Orbs and meteors for Power |
| Right mouse (hold) | raise the shield and absorb seeds (Power, Resonance); release to cast a burst | aim over the shoulder (C swaps shoulder) |
| Left Shift | smoke dash: 2 charges, invulnerable while smoke; one dash in the air | same |
| Space | jump; hold while falling to hover for 0.6 s | jump (lower, no hover) |
| Jump along a glowing wall | wall-run for up to 1.8 s, Space to leap off | same |
| Q | shift world (during the 1.2 s swap warning: +3 Power). The swap is a wave that starts at you | same |
| E | Lucid Pulse | Burst |
| R after defeat/victory | retry | retry |
| `[` and `]` | test only: lower and raise health | |

Once the boss fight starts, hover and wall-run are off and jump and dash behave as they did before, so the fight plays the same.

Level keys:

| Key | Action | Builds |
|---|---|---|
| G | jump to the next zone | all (this is the 60 second demo route) |
| B | jump to the boss entrance | all |
| F1, F2 | force Dream, force Nightmare | editor and development |
| F3, F4 | send a wave from the player, a slow wave from the boss arena | editor and development |

Boss debug keys (editor and development builds only; pressing one before the fight drops you into the arena):

| Key | Action |
|---|---|
| F5, F6, F7 | jump to boss phase |
| N | force a world swap |
| T | force a Sleepwalk teleport |
| V | cycle through every attack one by one |
| K | kill the boss |
| H | toggle hitbox drawing (turn Gizmos on in the Game view) |
| L | toggle the boss lock-on camera (Gizmos show the weighted look point; the HUD shows distance, FOV and nudge) |

### Boss fight camera

Entering the arena (`BossArenaTrigger`, created at runtime) locks the camera on automatically, with a 0.6 s blend, and fires `BossArenaTrigger.OnFightStart`. The camera sits close behind the player on the boss-player line, so A/D strafe around the boss and W/S move toward and away. The player always faces the boss. The mouse only nudges the view up to 10 degrees and springs back. There is no button to release the lock; it releases with a 0.8 s blend when the boss or the player dies. Tunables: `Assets/Resources/CameraLockSettings.asset` (menu **Lucid > Build Camera Lock Settings**; code defaults are used if it is missing).

Health: the HEALTH bar (bottom left) drops at once on a hit and a pale chip drains after 0.4 s. Below 40% health the screen corners turn red (off again above 45%).

Combat redesign in progress: see [COMBAT_NOTES.md](COMBAT_NOTES.md) for the current controls, tuning values and cuts.

## Where things are

| Path | What |
|---|---|
| `Assets/Scripts/` | Runtime code. `WorldState` is the Dream/Nightmare switch, `WorldMaterialSwap` reacts to it, `WorldLook` blends sky, fog and light. |
| `Assets/Scripts/PlayerController.cs` | Flow movement, dash chain, recall (flow back when you fall) and damage. |
| `Assets/Scripts/PlayerCombat.cs` | Shoot in Nightmare, shield in Dream. `Projectile`, `Enemy`, `WispEnemy`, `EnemyBullet` are the combat pieces. |
| `Assets/Scripts/LucidInput.cs` | The single input layer. All gameplay input and bindings (keyboard, mouse, gamepad) live here. |
| `Assets/Scripts/MemoryAvatar.cs` | The player visual, the Dreamwalker: a hooded figure animated in the shader (`Assets/Shaders/Dreamwalker.shader`). `SmokeDash` is the dash effect. |
| `Assets/Editor/` | Builder tools. They create prefabs, materials and the level from code. See the table below. |
| `Assets/Editor/World/` | The new world. `WorldMeshes` and `WorldKit` build the procedural kit, `WorldLevelBuilder` lays out the six zones and the arena, `WorldShots` renders review screenshots, `WorldBot` drives the player along a route in Play mode. |
| `Assets/Scripts/World/` | Runtime for the new world. `WorldShift` runs the swap wave and the shader globals, `LucidWorldLook` blends fog, light and post, `LevelFlow` holds zones, checkpoints, fall recovery and the level keys, plus phase platforms, wall-run surfaces, bounce blossoms, the three enemies and the ambience synth. |
| `Assets/Lucid/World/` | Generated shaders, materials, meshes, prefabs, volume profiles and the scene. Do not edit by hand. |
| `LEVEL_BIBLE.md` | Design of the new world: research takeaways, palettes, layout as built, wave design, kit list. |
| `Assets/Resources/Combat/` | Combat prefabs loaded at runtime by name through `CombatAssets`. |
| `docs/FreeAssetsReport.md` | Research on free assets to use next. |
| `LUCID_BRIEF.md` | Original design brief and event rules. Parts of it are out of date, see "Current state" below. |

### Builders (menu Lucid > Build Combat And Player Props)

`PlayerCombatPropsBuilder` runs `CombatBuilder` (shard, enemy orb, wisp, shield, particle burst) and `MemoryBuilder` (the player figure). The world is built by `Assets/Editor/World/`.

## Current state (Sat night): the new world

`LucidWorld.unity` replaces the old level in the build. One connected level of six zones climbs to the boss arena: Threshold Glade, The Hollow Bridge, The Weeping Wall, The Gloam Hollow, Rootclimb, The Sleeper's Approach. Everything in it is procedural (no third-party assets). A world swap is a wave that rolls out from the player or the boss; platforms, hazards and lights change when the front reaches them. New enemies: Glimmer Moth, Bramble Crawler, Gloam Watcher (only vulnerable in Nightmare). Dying before the boss returns you to the last checkpoint; falling costs 5% health.

Checked in Play mode with the route bot: every zone can be crossed, checkpoints advance, the three combat waves open the exit gate, the Healing Well heals, the boss fight starts in the new arena and R retries it.

Known problems: draw calls are 280 to 500 depending on the view (the target was 150), though the frame takes about 5 ms in the editor. The boss still kills a player who stands still very quickly. The old HUD debug line is still shown. Not playtested by a person yet, only by the bot. Sound is a synth ambience only.

## Earlier state (Fri night, old level)

Working: Dream and Nightmare look, world swap, flow movement, dash, recall, Nightmare shooting, Dream shield and absorb, wisp enemies, animated memory figure.
Gameplay implementation added: the Eclipse encounter, Power burst, timed-shift rewards, defeat/victory and R to retry. C# compilation checked; gameplay balance and visuals still require playtesting. Sound/music, a main menu and the itch.io build remain unfinished.
Known problems: the player figure and wisps over-brighten under bloom, the Dream still lacks painterly trees, white ruins and a distant giant creature, Nightmare ground reads pale, the wisteria arches are heavy on the GPU. Combat feel has not been tuned by hand yet. Feel values are public fields on `PlayerController`, `PlayerCombat`, `ThirdPersonCamera`, `WispEnemy`, plus `CameraLockSettings` for the boss camera.

## Boss fight

The Sleeper has 900 health, three phases, 18 attacks across ground, air, sky and homing lanes, and a Sleepwalk teleport with a wake window. Dream has Resonance, Lucid Pulse and gold seeds. Nightmare has a damage multiplier and Dread. All tunables are in `Assets/Resources/BossAttackData.asset` (menu **Lucid > Build Boss Attack Data**). Full list: [BOSS_ATTACKS.md](BOSS_ATTACKS.md).

## Game design in one paragraph

The HUD installs `EclipseEncounter` at runtime; no scenery rebuild is needed for these mechanics. Approach the black sun near the end of the path (z=142) to engage. Dream fires rotating absorbable rings; Nightmare fires aimed fans. Below half health, attacks and world swaps accelerate. A 1.2-second warning names the next world; manually shifting into it during the warning grants +3 Power and clears orbs within 7 units, once per warning. At 10 Power, E in Nightmare releases a 42-unit pulse for 45 damage. Dream boss armor reduces damage to 15%. These are initial tuning values, not playtested balance. Retry reloads the current scene.

Dream: slow, beautiful, safe. Enemy orbs are slow rings you absorb with a shield, which charges Power and heals. Nightmare: fast and loud. Enemy fire is quick and aimed, you shoot back, and the boss is exposed. The boss flips the world on a timer, so you absorb in Dream and strike in Nightmare. Original brief: [LUCID_BRIEF.md](LUCID_BRIEF.md). The puzzle idea was scrapped to focus on combat, movement feel and the boss.

## Team rules

- Unity: Asset Serialization = Force Text, Version Control = Visible Meta Files. Commit every `.meta`.
- Generated assets come from the builders. Change the builder, not the generated prefab, or your change is overwritten on the next build.
- One person per scene at a time. Pull before you start, push small and often.
- Add every outside asset or tool to [CREDITS.md](CREDITS.md) the moment you use it. The itch.io submission needs the list.
- Never commit the event deck PDF (it contains Wi-Fi passwords). It is in `.gitignore`.
