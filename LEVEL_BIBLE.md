# Lucid: level bible

World and level redesign for the SHIFT Game Jam build. Written Saturday 14 Nov, 18:00. Deadline Sunday 15 Nov, 14:45.

The brief is `FABLE_WORLD_REDESIGN_PROMPT.md`. Reference images are in `Assets/Lucid/Reference/` (reference only, never shipped).

## 1. Research takeaways (Ori and the Blind Forest, Ori and the Will of the Wisps)

1. **Everything walkable is lit.** Platforms and routes are the brightest, clearest shapes in the frame; scenery sits in lower contrast behind them. The player is guided without signs.
2. **Light means safety, dark means danger.** One colour language runs through the whole game: living areas are saturated green, gold, blue and magenta; decayed areas are near-monochrome grey-brown and sickly purple.
3. **Painterly, not plastic.** Lighting stays soft because it is painted, not computed from sharp normal maps. In 3D this means soft ramps, gentle normals and painted colour variation instead of PBR detail.
4. **Rim light and light rays everywhere.** The sequel added rim lights, deep shadows and shafts to every asset so forms pop out of the background.
5. **Stacked depth.** Scenes are built from several painted layers, back to front. Near and far layers sit in depth-of-field blur, so the eye rests on the play layer.
6. **Silhouettes do the storytelling.** Foreground and background are often plain dark shapes against a glowing backdrop. A big shape with two bright eyes is enough.
7. **Tiny glowing hero, huge world.** The protagonist is a moving light source. Scale contrast is the emotional device.
8. **A warm note inside the cold.** Even the creepiest area has small warm accents (orange fungus in grey mould) so it stays inviting to look at.
9. **Never still.** Spores, rays, swaying plants and small creatures are tuned so that no screen is inert at rest. Many small details add up to a believable place.
10. **Colour tells you where you are.** Each area owns a palette, and the palette shifts with the story beat.
11. **Blockout first.** The level's bones are tested as plain shapes and only dressed once they play well. Gameplay wins over nature: walls are unnaturally straight where the player needs them.
12. **Teach safe, then test.** A mechanic is introduced where failing costs nothing, then layered. Escalations of one idea are interleaved with other ideas, not dumped in a row.
13. **All roads lead back.** Side paths rejoin the main path. Secrets are hinted by a small visual lip, never obscure.

We take the principles only. No Ori character, creature, tree shape or screenshot composition is copied.

## 2. Final palettes

| Role | Dream | Nightmare |
|---|---|---|
| Sky top / mid | `#1B1B5E` / `#6A3FA0` | `#050B1A` / `#0C1B33` |
| Horizon and mist | `#C45AA5`, `#E88CC8` | `#2B4C73` |
| Key light | `#BFE6FF` (blue-white, from the spirit tree) | `#7FB6D9` (pale moon, from behind silhouettes) |
| Ground and bark, lit | `#5B4FA8` | `#16263F` |
| Ground and bark, shade | `#2A1F5C` | `#050B1A` |
| Rim light | `#E88CC8` | `#7FB6D9` |
| Glow flora | `#5FE3F0` (cyan), `#FFD98A` (gold) | `#4A1F6B` (dim violet) |
| Warm accents | `#FF9A4D` embers | `#FF5A36`, `#FFB347` (enemies and hazards only) |
| Shift seam | `#FFF2D0` core, `#E88CC8` edge | `#FFF2D0` core, `#FF5A36` edge |

Rules: backgrounds stay low in contrast and saturation. Red-orange is reserved for threats. The player and key items stay luminous in both worlds.

## 3. Layout

The arena centre is fixed in code at `(0, 0, 141)`, so the level climbs up to it. The path runs south to north and rises 34 m. The spirit tree stands behind the arena at `z = 215` and is 110 m tall, so it is visible from the first step.

```
                         N
              [ SPIRIT TREE  z=215 ]          goal landmark, always visible
              (  skull canopy in N )
                 .-------------.
                /   SLEEPER'S   \
               |   ARENA  r=27   |   y=0     z=141   (plane-locked)
                \               /
                 '------+------'
                        |  entrance z=114
   6 SLEEPER'S APPROACH |  y -4 -> 0     z 44..112    well at z=75, vista at z=95
                        |
                 .------+------.
   5 ROOTCLIMB  (  hollow tree  )  y -26 -> -4   centre z=30   spiral, bounce, wall-run
                 '------+------'
                        |  thorn gate z=8
                 .------+------.
   4 GLOAM ARENA (   combat    )   y=-26    centre z=-8  r=15
                 '------+------'
                        |
   3 WEEPING WALL  =====|#          y -30 -> -26  z -78..-30   cliff on the east side
                        |#          three wall-runs, swap hits mid-run
                        |
   2 HOLLOW BRIDGE  o O o O o       y -34 -> -30  z -132..-84  phase platforms over the abyss
                        |
                 .------+------.
   1 THRESHOLD   (    glade     )   y=-34    centre z=-150  r=15   start, tutorial
                 '-------------'
                         S
```

| Zone | New idea | Calm | Challenge | Reward | Checkpoint |
|---|---|---|---|---|---|
| 1 Threshold Glade | Move, absorb seeds in Dream, first swap, shoot | Open glade, glow sprite leads | Wave turns the glade to Nightmare; thorns block the gate; shoot the corrupted bud | Gate opens, world returns | Gate |
| 2 Hollow Bridge | Phase platforms, shift (Q) as a traversal tool | First two platforms are wide and close | Alternating Dream and Nightmare platforms over the abyss | Seed cache on a side leaf | Far ledge |
| 3 Weeping Wall | Wall-run | One short safe run over ground | Two longer runs; a scripted swap hits mid-run and thorns erupt on the wall | Hidden nook above the second ledge | Top ledge |
| 4 Gloam Arena | The full loop at small scale | Healing pods in Dream | Two waves of 3 to 6 enemies | Thorn gate opens | Gate |
| 5 Rootclimb | Combine everything, vertical | Light shafts, big roots | Spiral of platforms, bounce blossoms, wall-run, corruption rising from below | View of the arena from the top | Top |
| 6 Sleeper's Approach | None (breath before the boss) | Healing Well | None | Reveal of the arena and the sleeping boss | Arena entrance |

First-time route: 4 to 5 minutes. Demo route: `G` jumps to the next zone and `B` to the boss entrance, so the boss is reachable in seconds.

### As built

The diagram and table above are the plan. The built level keeps the order and the ideas but the coordinates moved during route testing. These are the values in `WorldLevelBuilder`:

| # | Zone (name in game) | Spawn | What is there |
|---|---|---|---|
| 0 | Threshold Glade | `(0, -33.9, -158)` | Start island, corrupt bud that opens the first thorn gate |
| 1 | The Hollow Bridge | `(0, -33.9, -129)` | Petal platforms (Dream) and thorn walkways (Nightmare) over the mist |
| 2 | The Weeping Wall | `(0, -30.7, -62)` | Safe wall-run at `z = -44`, landing island at `z = -30.5`, Dream panel at `z = -19` overlapping the Nightmare panel at `z = -10.5` by 5.5 m, arrival island at `z = 3` |
| 3 | The Gloam Hollow | `(0, -26.9, 7.5)` | Combat ring, centre `(0, -27, 19)`, radius 9.5, three waves, exit thorn gate |
| 4 | Rootclimb | `(0, -25.5, 38.6)` | 13 steps on a 9.2 m ring around the root pillar at `z = 48`, bounce blossoms, rising gloom from `y = -31` |
| 5 | The Sleeper's Approach | `(0, -3.1, 68)` | Healing Well island at `z = 72`, two root decks, the big root arch |
| 6 | The Sleeper | `(0, 0.15, 108.5)` | Arena entrance. Arena centre `(0, 0, 141)`, radius 27. Spirit tree at `z = 217`, skull above `z = 188` |

Changes from the plan: the combat zone has three short waves instead of two, the Weeping Wall has one safe run and one two-panel run with a swap in the middle instead of three runs, the moving ring in the Rootclimb and the hidden nooks were cut.

## 4. Transition design

A swap is a wave, never a fade. One component, `WorldShift`, owns these shader globals:

| Global | Meaning |
|---|---|
| `_WorldBlend` | 0 Dream, 1 Nightmare, animated (sky and post use it) |
| `_ShiftOrigin` | World position where the wave started |
| `_ShiftRadius` | Current radius of the wave front |
| `_ShiftEdgeWidth` | Thickness of the seam |
| `_ShiftFrom`, `_ShiftTo` | State outside and inside the front |
| `_ShiftSeam` | Seam brightness, fades out when the wave ends |

Every environment shader computes its own state from the distance to the origin, so the change rolls across surfaces. The same formula runs on the CPU (`WorldShift.BlendAt`) so colliders, hazards and lights switch when the front reaches them.

- **Origins:** the boss when it forces the swap, the player when they press Q, a scripted point in the level zones. Phase changes use a slower, wider wave.
- **Timing:** 0.5 s anticipation during the boss's existing warning, 1.2 s for the front to cross the arena, then it accelerates to the horizon and settles.
- **Seam:** a bright line on every surface, a refractive shell at the front, a ring of sparks, a chromatic-aberration burst and a light shake.
- **Per object, driven by the wave:** blossoms fold into thorn buds, roots and thorns grow out of the ground, glow flora dim, lanterns change colour, fog and sky roll over, Dream-only and Nightmare-only pieces dissolve with a glowing edge, phase platforms shimmer for 0.5 s before they stop being solid.
- **Player proximity:** in Dream, glow flora brighten near the player. In Nightmare, thorns lean toward the player and only the player's light shows colour.

Stance and world are one switch in the existing code (Q flips the world). The redesign keeps that; it does not add a separate stance.

## 5. Asset kit

All procedural, generated by `Assets/Editor/World/`. Every piece has a Dream and a Nightmare look from the shader globals.

1. Island platform (flat top, rocky underside)
2. Petal platform (Dream phase platform)
3. Thorn walkway (Nightmare phase platform)
4. Bark ledge
5. Wall-run wall with glowing handholds
6. Tree trunk and silhouette tree
7. Root (arches, bridges, arena wall)
8. Thorn gate
9. Healing Well
10. Bounce blossom
11. Lantern
12. Fog card and mist plane
13. Glow flora (mushrooms, strands, grass, seed pods)
14. Giant skull and beast silhouette (Nightmare backdrop)
15. Spirit tree (goal landmark)

Shaders (hand-written URP HLSL; Shader Graph files cannot be authored reliably from code): `Lucid/World/Environment`, `Foliage`, `Glow`, `FogCard`, `Sky`, `Seam`.

## 6. Schedule (20 hours left)

| When | Work |
|---|---|
| Sat 18:00 to 20:00 | Wave system, environment shaders, sky, post-processing |
| Sat 20:00 to 22:00 | Mesh toolkit and asset kit |
| Sat 22:00 to 00:00 | Boss arena, traversal (wall-run, air dash, hover, phase platforms) |
| Sun 00:00 to 03:00 | Zones 1 to 3, enemies |
| Sun 03:00 to 06:00 | Zones 4 to 6, per-object responses, audio |
| Sun 06:00 to 10:00 | Readability, performance, polish |
| Sun 10:00 to 13:00 | Team playtest and tuning |
| Sun 13:00 to 14:45 | Build, itch.io page, submit |

## 7. Debug keys

The brief asks for `1`, `2`, `T` and `H`, but the boss fight already uses those (`1/2/3` phase, `T` teleport, `H` hitboxes) and must not break. The world keys are therefore:

| Key | Action | Builds |
|---|---|---|
| `G` | Jump to the next zone | All (needed for the 60 s demo) |
| `B` | Teleport to the boss entrance | All |
| `F1` / `F2` | Force Dream / Nightmare | Editor and development |
| `F3` / `F4` | Trigger a wave from the player / from the boss | Editor and development |
