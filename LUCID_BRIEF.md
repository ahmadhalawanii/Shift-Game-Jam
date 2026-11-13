> **Status note (Fri 13 Nov night):** this is the original design brief. The game has since changed: the 2.5D layered look and sprite plan are replaced by a full 3D cinematic look, the player is a fading "memory" figure of light (not a faerie), enemies are ranged wisps, the puzzle idea was scrapped, and the boss is the Eclipse (the black sun). See `README.md` for the current state and what is built. Event rules, scoring and the pitch below are still valid.

# LUCID: project brief (SHIFT Game Jam, Nov 2026)

Everything decided so far in the brainstorm session. Use this as the source of truth for the build.

**LATEST MAJOR CHANGE (Fri 13 Nov, 19:32): the game is now fully 3D** (it was 2.5D). See section 3 for what that changes. Concept images in the art folder are still 2D/2.5D concept frames and show the look, not the final 3D camera.

## 1. The event

- SHIFT Game Jam, Abu Dhabi Gaming x Unity, NABDH MBZ, 13 to 15 Nov 2026.
- Theme: **Between Two Worlds**. The reveal video also showed these motifs: Life/Void, Organic/Digital, Ocean/Desert, Light/Darkness, Parallel Realities, sentient plants, "Yet connected", "One step away", "All worlds collide".
- Engine: Unity. Solo or teams up to 8. All work starts after the reveal. Declared assets and tools are allowed (list them in credits).
- Deadline: **Sunday 15 Nov, 14:45**. Submit a build or playable link on the SHIFT Game Jam itch.io page before leaving the venue. Needs title, team name, description and controls, credits/tools/assets, optional screenshot. Late entries are not judged.
- Judging session: Sunday 15:00 to 18:00. Demo is 3 minutes per team: 1 min intro (team and concept), 1 min live demo, 1 min judges' scoring.
- Everyone fills in the post-jam survey before leaving.
- Schedule: Fri prototyping 16:15 to 19:00, then free work. Sat 10:00 to 19:00 dev. Sun 10:00 to 13:00 dev, 13:00 to 14:00 polish, 14:45 submission.

### Scoring rubric (100 points)
- Gameplay 30 (Fun 10, Controls 10, Challenge 5, Replay 5)
- Theme relevance 20 (fits and builds on the theme)
- Creativity 15 (original concept 10, fresh mechanics 5)
- Art and audio 15 (visual design 10, sound and music 5)
- Technical execution 10 (stability 5, polish and completeness 5)
- Presentation 10 (clear live demo 5, team pitch 5)

Prizes: AED 60,000 in vouchers (30k / 20k / 10k) plus Unity consulting.

## 2. The game

**Title: Lucid.** A Saros-inspired, fast, fun bullet-ballet boss fight. The whole world and the boss constantly swap between two modes, Dream and Nightmare, and the player must adapt and swap with it.

Rejected along the way: puzzle games (not wanted), the earlier 1v1 hidden-timer duel (netcode risk), neon sci-fi look (disliked).

### Premise
The Sleeper is a giant whose mind is split in two. In the Dream it is calm and beautiful. In the Nightmare it is shattered, fast and hungry. The player is the Lucid, the only one who can walk both worlds, and must bring the Sleeper down before the Nightmare swallows the Dream.

### The boss: the Sleeper
A floating blossom with long curving tendrils (not a spider). Faceted flat shapes. Dream form: coral and pink, closed sleeping eyes. Nightmare form: same body turned charcoal with ember-orange lines and one open golden eye. One mesh, two materials.

### Dream mode: flow and recovery
- Feel: slow, floaty, graceful. Bullets are slow, curving, beautiful.
- Player holds a shield and absorbs bullets. Absorbing charges Power and slowly heals. A perfect absorb gives a brief slow-motion moment.
- Gives clarity: attack patterns are telegraphed and weak points glow, so the player learns the fight here.
- Limit: the boss is armored (about 15 percent damage), so you can't win here.
- Dream drains the Dread meter.

### Nightmare mode: aggression and risk
- Feel: fast, harsh, loud. Screen warps, music turns heavy, bullets are fast and sharp.
- Player drops the shield, fights with guns and a double dash. The boss is exposed, damage is multiplied, and stored Power can be spent on a burst.
- Pressure: patterns are distorted, shadow clones appear, and the Dread meter fills while you stay. At max, the player takes heavy damage.
- Limit: strong but fragile, so you can't stay long.

### How they complete each other
- Dream charges Power and heals. Nightmare turns Power into damage.
- Dream teaches the patterns. Nightmare tests them under pressure.
- Dream drains Dread. Nightmare fills it.
- Dream is preparation, Nightmare is the strike. Neither mode wins alone.

### Why the boss pulls you into Nightmare
- Its swap timer runs out and the world tears open.
- Night Terror: a telegraphed grab that drags you in early if you don't dodge.
- When you are weak and low on health, it forces you in to finish you.
- In later phases it pulls you in more often.

### Why the player chooses Nightmare
- Power is full and you want to burst the boss.
- The boss is exposed and you want to deal damage.
- A weak point has opened and you want to hit it before it closes.
- Perfect shifts (swapping on the beat) give bonus Power and a shockwave.

### Why the player retreats to Dream
- Dread is nearly full.
- Low health, need to recover.
- Need to recharge Power.
- Want to read the next attack pattern.

### Run flow
1. Opening: wake in the Dream. Voice: "Stay calm. Learn its rhythm." Absorb, charge, observe.
2. Phase 1: slow swaps, learn the loop of charge, swap, burst.
3. Phase 2: the Sleeper stirs. Faster swaps, Night Terror grab appears. Voice: "It's pulling you under."
4. Phase 3: Dream and Nightmare overlap. Calm and sharp bullets come together, both music layers play at once, arena glitches and tears.
5. Ending: the Sleeper falls, both worlds settle into one soft glowing scene. Voice: "You were never choosing between them. You needed both." On death, the worlds snap back and the fight restarts.

### Core mechanics spec
- Two stances for the player, matching the two modes: Dream stance (shield, absorb, cannot shoot) and Nightmare stance (guns, no shield). Swap key: Shift or right mouse.
- Boss swaps world on a timer: 9 seconds at full health down to about 3.5 seconds near death.
- Telegraph before each swap (about 1.2 seconds). The boss pauses firing during the telegraph.
- Perfect shift: swap stance during the telegraph or within about 0.35 seconds after the flip. Once per flip. Gives bonus Power and a shockwave that clears nearby bullets.
- Power: filled by absorbing Dream bullets. When full, burst (E key) deals big damage and clears bullets. Burst damage is still affected by armor, so it is best used in Nightmare.
- Dash (Space) gives brief invulnerability and passes through bullets.
- Dread meter: rises in Nightmare, falls in Dream. At max, heavy damage. (Designed, not yet implemented anywhere.)
- Controls: WASD move, mouse aim, hold left mouse to shoot (Nightmare stance), Shift or right mouse swap, Space dash, E burst.
- Tuning must be easy: keep swap interval, telegraph time, bullet speeds and armor as public variables. Playtest swap fairness first.

## 3. Camera and technical approach (UPDATED: full 3D)

- Decision: the game is 3D. Recommended form: **third-person with lock-on to the boss, in a circular arena**. The player strafes around the boss, and the camera follows behind and above (about 35 to 50 degrees pitch) with the boss kept in frame by lock-on.
- Gameplay stays on a horizontal plane. Movement and bullets sit at a fixed height, there is no vertical dodging, and aim is auto-locked to the boss. This keeps the logic simple and bullets readable, while everything the player sees is real 3D.
- Player is a hovering faerie, so no walk cycle is needed. Use idle bob, bank tilt when moving, and wing flutter.
- Boss is a floating blossom. Faceted low-poly body and petals (flat shading gives the faceted look for free). Tendrils are procedural (a chain of segments or a spline with sine motion), so no skeletal animation is needed. Nightmare form uses a second material with ember lines and an open eye.
- Bullets: emissive spheres with trails and bloom. Dream bullets are smooth round orbs, Nightmare bullets are spiked orbs. Blob shadows or decals under bullets help depth reading.
- Rendering: Unity URP, toon or flat-shaded materials with outlines, bloom, fog, depth of field, and a color-grading volume per world.
- Dream/Nightmare swap: global shader float `_WorldBlend`, volume weight crossfade, skybox and fog swap, boss material swap, music crossfade between two looping layers, hit-stop, camera shake (Cinemachine impulse).
- Environment: circular arena floor with an ornate pattern texture, pillars from simple meshes, clouds as soft sprite cards or layered particle systems, blurred foreground clouds near the camera.
- Tools: URP, Cinemachine (camera, lock-on, shake), Shader Graph (toon, swap), post-processing volumes.
- Controls: WASD move, mouse look (camera) or lock-on, left mouse shoot (Nightmare stance), Shift or right mouse swap stance, Space dash, E burst.
- Risks and mitigations:
  - Camera tuning and collision. Mitigation: lock-on, a simple open arena with no walls, and tune the camera in the first hour.
  - Bullet readability in 3D. Mitigation: plane-locked bullets, bright emissive orbs with outlines, dim environment, distinct shapes per world.
  - Scope. Mitigation: no skeletal animation, procedural tendrils, store assets for props, cut list below.
- Assets: use low-poly props from the Asset Store, Kenney, Quaternius or CC0 Sketchfab models for pillars, rocks and crystals. Declare everything in the itch.io credits.
- Note: a Unity C# scaffold was drafted earlier (Life/Void naming, planar top-down). It is not delivered and needs a rewrite for the 3D camera and Dream/Nightmare naming. Available on request.

## 4. Art direction (chosen)

**Layered fairytale look, translated into 3D (flat-shaded, faceted, outlined)**, inspired by the hand-painted folk-art fairytale feel of Faerie Afterlight (surreal, crisp contrasting colors, ornate patterns) and the layered-cloud, faceted-boss, glowing-accent style of the user's reference screenshot. Take the style only. Do not copy any game's scenes, boss designs or characters.

### Style rules
- Flat color shapes with soft rim-lighting on cloud edges (light edge on the top left of each puff).
- Puffy cloud clusters framing the sides and bottom, on separate depth layers.
- Tall, dark, low-contrast pillars in the background, with some blurred ones at the screen edges in the foreground.
- Foreground ground is navy with a scalloped bumpy edge.
- Boss: faceted angular petals and body, thin curving tendrils with glowing stripes.
- Arena floor: ornate ring and mandala pattern in a folk-art style.
- Small floating drop-shaped sparkles for atmosphere.
- Same world, two states: the Nightmare is the Dream corrupted (sleeping face becomes an open eye, soft orbs become thorn-orbs, pink clouds become dark smoke with ember rim light).

### Palette
Dream:
- Sky: indigo `#2B2F8F` to orchid `#7A4FA8`
- Clouds: `#C9619F`, `#E487B8`, `#9C4C9E`, rim `#FFB3D6`
- Boss: coral `#F4876B`, `#FFB199`, `#E0657A`, shadow `#B4415F`
- Accents: cyan `#4FD1E8`, cream `#FFE7C2`
- Ground: navy `#1B2060`
Nightmare:
- Sky: `#070921` to `#241346`
- Clouds: `#2B2A62`, `#1D1B4A`, rim `#C8601F`
- Boss: charcoal `#1B1B3D`, `#2D2A5C`, `#14142E`
- Accents: ember `#FF7A2E`, amber `#FFB347`, gold `#FFD166`
- Ground: `#07081C`

### Characters and UI
- Player: a small winged faerie with a round head and hair bun, petal-pink wings and a shield bubble in the Dream, ember-orange wings and amber orb shots in the Nightmare.
- HUD: petal-shaped health pips and rounded-pill Power pips, in the Dream and Nightmare palettes.

### Art checklist
Needs an artist or store assets: boss mesh or sprite (one body, two materials), faerie player, 2 to 3 pillar and cloud sprite sets, arena floor texture, crystals, HUD pieces.
Can be done with shaders and effects: world swap dissolve, fog, bloom, depth-of-field, color grading, blob shadows, sparkle particles, music crossfade.

## 5. Pitch script (3 minutes, updated for Lucid)

Intro (about 1 minute):
Hi everyone, we're [team name], and our game is Lucid. The theme is Between Two Worlds, so we built a boss fight inside a mind that is split in two, a Dream and a Nightmare. In the Dream the boss is calm and armored, and its bullets are slow and beautiful. You hold up your shield and absorb them, and every bullet you absorb charges your power and heals you. Then the world tears, and you're pulled into the Nightmare. The boss is exposed, the bullets turn fast and sharp, and now you drop the shield, fight back, and unleash everything you stored. You can't win in just one world. The Dream is where you prepare and the Nightmare is where you strike, and the closer the boss gets to dying, the faster the worlds flip.

Demo narration (about 1 minute):
So here we are in the Dream. See the soft bullets? I'm holding my shield and soaking them up, and the power bar is filling. Now watch the warning, the screen is about to tear. If I swap right on the beat, I get a perfect shift, a shockwave that clears the bullets around me and gives me bonus power. And there it goes, we're in the Nightmare. The boss is wide open, so I shoot, dash through the bullets, and when my power is full I hit burst. Big damage. But I can feel the dread building, so I swap back to the Dream to recover. Absorb, burst, adapt. That's Lucid.

## 6. Plan for roughly 36 working hours (3D version)

Start Fri evening, deadline Sun 14:45. The 3D change adds risk, so tonight's goal is a playable greybox.
- Hours 0 to 8: greybox arena, 3D player controller (hover, strafe, dash), third-person camera with lock-on, shooting, shield/absorb, stance swap, swap timer. Test the camera and bullet readability on placeholder shapes.
- Hours 8 to 20: boss with Dream and Nightmare bullet patterns, Power and burst loop, perfect shift, phase scaling, basic HUD. Boss placeholder first, final model later.
- Hours 20 to 30: art pass (faceted boss and tendrils, faerie, arena floor, pillars, clouds), toon/outline shaders, swap effect, post-processing, music crossfade, sound effects.
- Hours 30 to 36: tuning of swap timing, bug fixes, itch.io build, pitch rehearsal.
- Cut order if behind: Dread meter, upgrades, shadow clones, a second arena, extra bullet patterns, in that order. Never cut swap polish or audio.
- Upload an early build to itch.io on Saturday night so a late problem can't cost the submission.

## 7. Open questions
- Team size and roles (who does art, audio, programming)?
- Art style is chosen as the layered 2.5D fairytale look. Confirm if any change is wanted.
- Is Dread meter in scope, or cut for time? (Now first on the cut list.)
- 3D assets: does anyone on the team model in 3D, or is it all store assets?
- Unity version and render pipeline (assuming URP)?
- Final game title is Lucid. Boss name "the Sleeper" is a placeholder.

## 8. Concept images (in the art folder)
- `layered25.png`: the chosen look as a 2.5D concept, Dream on top and Nightmare below (the final game is now 3D).
- `layered_style.png`: flat side-view version of the same style.
- `folk_fairytale_v2.png`: folk-art fairytale option with the faerie player.
- `style_options.png`: the four earlier style options for comparison.
