# The Sleeper: attack catalog

Every number below is a default. The live values are in `Assets/Resources/BossAttackData.asset` (one row per attack, matched by `id`). Edit them in the Inspector during Play mode. Menu **Lucid > Build Boss Attack Data** creates the asset and adds rows for new attacks. It never overwrites values you already tuned. If the asset is missing, the game uses these defaults.

Damage is out of 100 player health. The player's memory takes `damage / 100`. `difficulty` (0.7 to 1.5) multiplies speed and count and divides telegraph time. Telegraphs never go below 0.5 s, or 0.35 s for small phase-3 telegraphs.

Code: `Assets/Scripts/BossAttacks.cs`, one class per attack, each with `Telegraph()`, `Execute()` and `Cleanup()`. `AttackDirector.cs` chooses which attack runs next.

| Attack | id | Lane | Mode | Damage | Speed | Count | Telegraph s | Duration s | Cost | Look |
|---|---|---|---|---|---|---|---|---|---|---|
| Lullaby Spiral | `lullaby_spiral` | Air | Dream | 7.5 | 5.5 | 4 arms | 0.7 | 2.4 | 1 | Coral and cream orbs in rotating spiral arms that curve 25°/s and leave long trails. Absorbable. |
| Hex Garden | `hex_garden` | Ground | Dream | 14 | | 3 waves | 0.9 | 0.35 | 2 | A honeycomb of hex tiles blooms under the player. One stripe of tiles at a time lights pink to gold, then fires a pillar of light. Unlit tiles are safe. |
| Starfall | `starfall` | Sky | Dream | 12 | 20 | 6 | 0.8 | 3 (pool) | 1 | A gold floor ring with a pentagon inside marks each impact. A gold orb falls onto it and leaves a bloom pool that hurts (3 per 0.5 s). |
| Tether Seeds | `tether_seeds` | Homing | Dream | 7.5 | 3.6 | 4 | 0.8 | 4 | 1 | Large gold seeds tied to the boss by thin lines. They home in on the player. Absorbing one gives +1.5 Power and heals. A seed left alone bursts into a ring of 12 orbs. |
| Tendril Slam | `tendril_slam` | Ground | Dream | 10 | 9 | 2 rings | 0.8 | 0.5 | 1 | A coral ring with a spinning hexagon marks the slam point, then coral shockwave rings spread out. Jump or dash through them. |
| Bloom Ring | `bloom_ring` | Air | Dream | 7.5 | 6 | 24 | 0.7 | 0.7 | 1 | Expanding pink rings of 24 orbs, each with two gaps. Absorbable. |
| Dream Seed Halo | `seed_halo` | Air | Dream | 7.5 | 8.5 | 4 rows | 0.9 | 0.52 | 1 | The original cyan curtains. The opening moves one bead per row. |
| Twin Helix | `twin_helix` | Air | Dream | 7.5 | 8.5 | 34 | 0.9 | 0.11 | 1 | Two cream and cyan chains weaving on mirrored sine curves. |
| Night Terror Fan | `night_terror_fan` | Air | Nightmare | 12 | 13 | 5 to 9 | 0.6 | 0.45 | 1 | Two faint ember lines mark a 60° fan. Then come volleys of spiked ember thorns, alternately offset. |
| Prism Beams | `prism_beams` | Sky | Nightmare | 16 per tick | 32 °/s | 3 to 5 | 0.7 | 3.2 | 2 | Violet beam lines with white-hot cores spread from the core like clock hands and sweep the arena. Faint floor lines show them first. |
| Shatter Grid | `shatter_grid` | Ground | Nightmare | 15 | | 4 groups | 0.9 | 0.3 | 2 | The floor cracks into amber triangles. They erupt in groups as black and amber spikes. A cyan line marks the one safe lane, 3.4 m wide. |
| Hunter Orbs | `hunter_orbs` | Homing | Nightmare | 10 | 6.5 | 3 | 0.7 | 3 | 1 | Large dark orbs with ember rims hunt the player for 3 s, then split into 5 thorns. Shoot one to cancel it and get a Power refund. |
| Eclipse Meteors | `eclipse_meteors` | Sky | Nightmare | 14 | 30 | 5 | 0.6 | 3 (crater) | 1 | Ember rings with amber triangles mark impacts. Slanted meteors with long trails land on them and leave burning craters (4 per 0.5 s). Meteors can be shot down for a Power refund. |
| Whip Tendrils | `whip_tendrils` | Ground | Nightmare | 15 | 14 | 2 sweeps | 0.8 | 0.9 | 1 | A glowing red 120° arc on the floor, then low blood-red arc waves. Dash through them (i-frames) or move out of the arc. |
| Thorn Burst | `thorn_burst` | Air | Nightmare | 12 | 9 | 16 | 0.8 | 0.45 | 1 | Radial rings of 16 amber and ember thorns, each burst rotated half a slot. Three bursts in phase 3. |
| Tracking Beams | `tracking_beams` | Homing | Nightmare | 10 | | 2 | 1 | 0.25 | 1 | Thin red lines follow the player with a lag, lock, and fire as white-hot beams. A low pressure layer. |
| Mandala Bloom | `mandala_bloom` | Air | Both | 9 | 5 | 20 per ring | 0.9 | 0.55 | 2 | Concentric orb rings with four gaps that walk one slot per ring. From phase 2 they counter-rotate. Pink and cyan seeds in Dream, ember and violet thorns in Nightmare. |
| Petal Blade / Thorn Lance | `petal_blade` | Ground | Both | 13 | 10 | 1 | 1 | | 2 | A 60 m tilted strip sweeps across the arena through the player's lane. Coral petals in Dream, soot and amber in Nightmare. Dash through it. |
| Sleepwalk | `sleepwalk` | Ground | Both | 12 Dream / 18 Nightmare | | 1, or a 3+1 chain | sigil time per phase | 0.35 | 3 | See below. |

## Sleepwalk (teleport)

The boss bursts into petals or embers and a streak shoots toward a floor sigil (a ring with two counter-rotating triangles) beside or behind the player. The boss lands with a slam (radius `slamRadius` 4.5 m), a shockwave ring and a burst of `sleepwalkBurstCount` orbs. Camera shake, 0.06 s hit-stop.

- Sigil time per phase: 0.9 s, 0.75 s, 0.6 s (`BossPhaseData.sigilTime`).
- In Nightmare, phase 2 and 3 use a chain: three sigils in a triangle around the player, then a bigger slam in the center.
- If the slam misses, the boss is stunned for `wakeWindow` (1.2 s). During the wake window it takes full damage in both worlds. The boss bar turns cyan and reads WAKE WINDOW.
- Frequency per phase: 18 to 25 s, 14 to 20 s, 10 to 14 s.

## Director and phases

| | Phase 1 | Phase 2 | Phase 3 |
|---|---|---|---|
| Health | 900 to 600 | 600 to 300 | 300 to 0 |
| World swap interval (1.2 s warning) | 9 s | 6 s | 3.5 s |
| Budget per cycle | 2 | 3 | 4 |
| Layering | one attack | plus an overlay from another lane after 0.9 s (cost 1) | two lanes at once |
| Gap after a cycle | 0.7 to 1.1 s | 0.55 to 0.9 s | 0.45 to 0.75 s |
| Telegraph scale | 1.0 | 0.9 | 0.8 |

The director never repeats the last attack and never pairs two attacks from the same lane. It starts nothing while Sleepwalk runs. In phase 3 the arena wall contracts from 20 m to 13 m over 20 s. In the finale (core broken, binding ready) it fires only absorbable Dream Air attacks.

## Dream and Nightmare engagement

- **Resonance:** each absorbed seed counts toward the combo. Every `absorbsPerResonance` (4) absorbs raises it one level, x1 to x5. The shield shifts from cyan to gold and each level plays a rising tone. Taking a hit resets it. At x5, press E in Dream for a **Lucid Pulse**: 60 damage through the Dream armour, and it clears orbs within 12 m.
- **Gold seeds:** each Dream seed has a 12% chance to be gold. A gold seed gives +1 Power and heals 5% on absorb.
- **Perfect shift:** shift into the announced world during the 1.2 s warning to get +3 Power and clear orbs within 7 m.
- **Nightmare multiplier:** damage rises to x2 over 10 s in Nightmare without being hit.
- **Dread:** fills over 14 s in Nightmare and drains over 5 s in Dream. Above 55% the screen shakes and the edges turn violet. At 100% you are thrown back to Dream and take 8 damage.

## Balance note

Boss health went from 300 to 900. Player weapon damage is unchanged; `playerDamageScale` is 1, so the boss takes three times as many hits as before. The Nightmare multiplier (up to x2), the wake window and Lucid Pulse make up some of that. If playtests run past 5 minutes, raise `playerDamageScale` (1.5 means an old-equivalent 600 hp).

## Audio hooks

`LucidAudio.Hook(id)` plays `Resources/Audio/<id>` if that clip exists. Otherwise it plays a synthesized placeholder tone, different for each id. Ids: `atk_<id>_telegraph`, `atk_<id>_fire`, `atk_<id>_impact` for every attack above, plus `boss_teleport_out`, `boss_teleport_in`, `boss_swap`, `boss_stagger`.

## Third-party assets

None. All boss geometry, VFX and placeholder sounds are generated in code at runtime.
