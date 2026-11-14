# Menu and cinematics QA

| Test | Result | Evidence |
| --- | --- | --- |
| Title boots and renders with the live hero | PASS | Play Mode Game view, `Library/DreamcatcherQA/title-revised.png` at 880 × 377 |
| Rapid double Play and natural opening handoff | PASS | Invoked the Play callback twice. The opening held player controls, then entered Playing with the player enabled. One AudioListener |
| Help, settings, credits | PASS (callback smoke test) | Opened and closed panels via their button listeners. Changing sensitivity changed the live camera value |
| Pause and resume | PASS | Injected Escape and invoked Resume. State sequence Playing → Paused → Playing |
| Fusion and ending skip | PASS (debug integration test) | Invoked the real phase-3 and victory methods. Injected Space. Fusion returned to Playing with phase 3. Ending reached Results |
| Replay resets zone and boss phase | PASS (state smoke test) | Play Again reloaded into zone 0 with the boss in phase 1. Opening handed back to gameplay |
| Full combat, natural boss cinematics, checkpoint retry and the complete player route | NOT TESTED | Debug integration exercised the underlying cinematic events directly |
| 1280 × 720 and 1920 × 1080 layouts | NOT TESTED | Editor capture used the active Game view size |
| Lethal lingering boss hazard | FIX IMPLEMENTED, RETEST PENDING | Editor log showed `ArgumentOutOfRangeException` in `BossHazards.Update` when defeat cleared the zone list during its damage callback. The zone write now precedes the callback. |
| WebGL build | INCOMPLETE | Last observed with compressed scene data emitted and Unity Bee linking the WebAssembly player. No launch page had been emitted. The build started before the final hazard fix. |
| Browser pointer lock, sound and complete run | NOT TESTED | Local HTTP and in-app browser requests to the loopback test server timed out |
