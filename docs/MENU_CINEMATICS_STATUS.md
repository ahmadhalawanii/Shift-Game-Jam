# Dreamcatcher presentation

The active branch for this work is `ibrahim-dreamcatcher`, based on `756b892` with existing local edits. Unity 6000.0.62f1 responded over the live editor bridge. The baseline Console query returned no errors. The build starts in `Assets/Lucid/World/Scenes/LucidWorld.unity`, the connected six-zone world. Installed packages include URP 17.0.4, Input System 1.20.1, and uGUI 2.0.0. The package lock has no Timeline or Cinemachine.

The presentation is installed at runtime for LucidWorld. It uses the live procedural MemoryAvatar, WorldAmbience and LucidAudio, LevelFlow checkpoints, the existing third-person camera, and EclipseBoss events. The scene builder and build scene list require no changes. Space skips a cinematic and Esc pauses gameplay. Web offers a mouse capture button after the opening.

| Gate | State |
| --- | --- |
| A · Audit | Live Editor and scene verified |
| B · Title | Editor-tested title render, menu dispatch and Play handoff |
| C · Opening | In-engine camera sequence and natural completion Editor-tested. Authored Timeline and hero pose animation remain outstanding |
| D · Boss | Phase-3 refocus and skip tested by invoking the actual phase method in Play Mode. The existing fight has no three-anchor fusion objective |
| E · Ending and Results | End and skip tested by invoking the actual victory method in Play Mode. Replay reset to zone 0 and phase 1 |
| F · Pause and settings | Pause/resume and camera sensitivity exercised in Editor. Existing checkpoint behavior retained. Master volume and reduced shake/flash controls implemented |
| G · Art and audio | First pass using the actual game assets, woven ring and synthesized cues. Additional shot choreography and sound mix remain outstanding |
| H · Web | Unity started a WebGL build to `Builds/Dreamcatcher-Web`. Browser verification remains outstanding |

The real victory condition is binding the Sleeper in Dream with sufficient Power after the core breaks. The presentation uses that event and preserves the three existing combat phases.

Presentation code and UI were produced with Codex assistance. Existing art and audio attributions are in `CREDITS.md`. Team credits there still need names.
