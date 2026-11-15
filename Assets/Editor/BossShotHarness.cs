using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Dev tool: file-triggered boss screenshots. Write a script to Temp/bossshot.req and the editor
// recompiles, enters Play mode, drops the player in the arena, runs the script and writes PNGs to
// Temp/LucidShots plus a log to Temp/bossshot.done. Nothing here runs in a build.
// Commands (one per line):
//   wait S | world D|N | phase N | attack ID | tp X Y Z | time S | god 0|1
//   shot NAME                              gameplay camera
//   shotc NAME px py pz tx ty tz fov       camera and target in boss-local space
//   shotw NAME px py pz tx ty tz fov       camera and target relative to the arena centre
[InitializeOnLoad]
public static class BossShotHarness
{
    const string Req = "Temp/bossshot.req", Done = "Temp/bossshot.done";
    const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    static List<string[]> steps;
    static int index;
    static float waitUntil, startedAt;
    static float Now { get { return Time.realtimeSinceStartup; } }
    static bool ready, god = true;
    static StringBuilder log;
    static double nextPoll;

    static BossShotHarness() { EditorApplication.update += Tick; }

    static int Stage { get { return SessionState.GetInt("bs_stage", 0); } set { SessionState.SetInt("bs_stage", value); } }

    static void Finish(string message)
    {
        StopSim(); noSwap = false; vidDir = null; vidClock = false; Time.captureFramerate = 0;
        if (atlas != null) { UnityEngine.Object.DestroyImmediate(atlas); atlas = null; }
        File.WriteAllText(Done, message + "\n" + (log != null ? log.ToString() : ""));
        Stage = 0; steps = null;
        if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
    }

    static void Tick()
    {
        int stage = Stage;
        if (stage == 0)
        {
            if (EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + 0.5;
            if (!File.Exists(Req)) return;
            SessionState.SetString("bs_script", File.ReadAllText(Req));
            File.Delete(Req);
            if (File.Exists(Done)) File.Delete(Done);
            SessionState.SetFloat("bs_t", (float)EditorApplication.timeSinceStartup);
            Stage = 1;
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            AssetDatabase.Refresh();
            return;
        }
        if (stage == 1)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlaying) return;
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat("bs_t", 0f) < 2.0) return;
            if (EditorUtility.scriptCompilationFailed) { Finish("COMPILE_FAILED"); return; }
            if (SessionState.GetString("bs_script", "").Trim() == "compile") { Finish("COMPILED"); return; }
            Stage = 2;
            EditorApplication.isPlaying = true;
            return;
        }
        if (stage == 2)
        {
            if (!EditorApplication.isPlaying || !Application.isPlaying) return;
            steps = new List<string[]>();
            foreach (var raw in SessionState.GetString("bs_script", "").Split('\n'))
            {
                var parts = raw.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && !parts[0].StartsWith("#")) steps.Add(parts);
            }
            index = 0; ready = false; god = true; waitUntil = 0f; log = new StringBuilder(); startedAt = Now;
            Application.runInBackground = true;
            Stage = 3;
            return;
        }
        if (stage == 3)
        {
            if (!Application.isPlaying || steps == null) { Finish("ABORTED (left play mode)"); return; }
            if (Now - startedAt > 400f) { Finish("TIMEOUT at step " + index); return; }
            try { InputTick(); RecTick(); VidTick(); HoldSwap(); Run(); }
            catch (Exception e) { Finish("EXCEPTION " + e); }
        }
    }

    // ---- gameplay clip recorder: Camera.main into a frame atlas, saved as a JPG under Resources/Tutorial.
    const int RW = 400, RH = 225;
    static Texture2D atlas; static RenderTexture recRT; static string recName;
    static int clipFrame, recFrames, recCols; static float recNext, recStep;

    static void StartRec(string name, float fps, float seconds)
    {
        recName = name; recStep = 1f / fps; recFrames = Mathf.RoundToInt(fps * seconds); recCols = 9; clipFrame = 0; recNext = Now;
        int rows = Mathf.CeilToInt(recFrames / (float)recCols);
        atlas = new Texture2D(recCols * RW, rows * RH, TextureFormat.RGB24, false);
        var clear = new Color32[atlas.width * atlas.height]; atlas.SetPixels32(clear);
        if (recRT == null) recRT = new RenderTexture(RW, RH, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
    }

    static void RecTick()
    {
        if (atlas == null || Now < recNext) return;
        recNext += recStep;
        var cam = Camera.main; if (cam == null) return;
        var keep = cam.targetTexture; cam.targetTexture = recRT; cam.Render(); cam.targetTexture = keep;
        var act = RenderTexture.active; RenderTexture.active = recRT;
        int rows = atlas.height / RH, col = clipFrame % recCols, row = clipFrame / recCols;
        atlas.ReadPixels(new Rect(0, 0, RW, RH), col * RW, (rows - 1 - row) * RH);
        RenderTexture.active = act;
        if (++clipFrame < recFrames) return;
        atlas.Apply();
        Directory.CreateDirectory("Assets/Resources/Tutorial");
        File.WriteAllBytes("Assets/Resources/Tutorial/" + recName + ".jpg", atlas.EncodeToJPG(90));
        log.AppendLine("clip " + recName + " " + recFrames + " frames " + atlas.width + "x" + atlas.height);
        UnityEngine.Object.DestroyImmediate(atlas); atlas = null;
    }

    // ---- simulated input, queued on the real keyboard and mouse every tick while active.
    static readonly Dictionary<Key, float> held = new Dictionary<Key, float>();
    static float leftUntil, rightUntil; static bool sim;
    static InputSettings.EditorInputBehaviorInPlayMode keepBehavior;
    static InputSettings.BackgroundBehavior keepBackground;

    static void StartSim()
    {
        if (sim) return; sim = true;
        keepBehavior = InputSystem.settings.editorInputBehaviorInPlayMode; keepBackground = InputSystem.settings.backgroundBehavior;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
    }

    static void StopSim()
    {
        if (!sim) return; sim = false; held.Clear(); leftUntil = rightUntil = 0f;
        InputSystem.settings.editorInputBehaviorInPlayMode = keepBehavior; InputSystem.settings.backgroundBehavior = keepBackground;
    }

    static void InputTick()
    {
        if (!sim) return;
        var keys = new List<Key>();
        foreach (var k in held) if (k.Value > Clock) keys.Add(k.Key);
        if (Keyboard.current != null) InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys.ToArray()));
        if (Mouse.current != null)
            InputSystem.QueueStateEvent(Mouse.current, new MouseState().WithButton(MouseButton.Left, Clock < leftUntil).WithButton(MouseButton.Right, Clock < rightUntil));
    }

    // ---- video recorder: fixed 30 fps game time, one JPG per frame into a folder (encode with ffmpeg).
    static string vidDir; static int vidFrame, vidTotal, vidLast; static bool vidClock;
    static RenderTexture vidRT; static Texture2D vidTex;
    // noswap: keeps pushing the boss's forced world swap into the future so a clip stays in one world.
    static bool noSwap;
    static void HoldSwap()
    {
        if (!noSwap) return;
        var b = UnityEngine.Object.FindFirstObjectByType<EclipseBoss>();
        if (b != null) typeof(EclipseBoss).GetField("swapAt", Any).SetValue(b, Time.time + 30f);
    }
    static float Clock { get { return vidClock ? Time.time : Now; } }

    static void VidTick()
    {
        if (vidDir == null || Time.frameCount == vidLast) return;
        vidLast = Time.frameCount;
        var cam = Camera.main; if (cam == null) return;
        const int w = 960, h = 540;
        if (vidRT == null) vidRT = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        if (vidTex == null) vidTex = new Texture2D(w, h, TextureFormat.RGB24, false);
        var keep = cam.targetTexture; cam.targetTexture = vidRT; cam.Render(); cam.targetTexture = keep;
        var act = RenderTexture.active; RenderTexture.active = vidRT;
        vidTex.ReadPixels(new Rect(0, 0, w, h), 0, 0); vidTex.Apply();
        RenderTexture.active = act;
        File.WriteAllBytes(vidDir + "/f_" + vidFrame.ToString("D4") + ".jpg", vidTex.EncodeToJPG(92));
        if (++vidFrame < vidTotal) return;
        log.AppendLine("video " + vidDir + " " + vidFrame + " frames");
        vidDir = null;
    }

    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    // ---------- Recording with a simple fight bot ----------

    static bool recording;
    static int recFrame, recTotal, recLastFrame;
    static float recStart;
    static string recFolder, lastKeys = "", lastMouse = "";
    static RenderTexture recTarget;
    static Texture2D recTexture;

    static void RecordFrame(EclipseBoss boss, PlayerController player)
    {
        if (Time.frameCount == recLastFrame) return;
        recLastFrame = Time.frameCount;
        float t = Time.time - recStart;
        BotInput(t, boss, player, true);

        var cam = Camera.main;
        if (cam != null)
        {
            const int width = 1280, height = 720;
            if (recTarget == null) recTarget = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            if (recTexture == null) recTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var keepTarget = cam.targetTexture;
            cam.targetTexture = recTarget;
            cam.Render();
            var keepActive = RenderTexture.active;
            RenderTexture.active = recTarget;
            recTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            recTexture.Apply();
            RenderTexture.active = keepActive;
            cam.targetTexture = keepTarget;
            File.WriteAllBytes(recFolder + "/frame_" + recFrame.ToString("D5") + ".jpg", recTexture.EncodeToJPG(90));
        }
        recFrame++;
        if (recFrame < recTotal) return;
        recording = false;
        Time.captureFramerate = 0;
        BotInput(t, boss, player, false);
        log.AppendLine("recorded " + recFrame + " frames to " + recFolder);
    }

    // Strafes around the boss, keeps a medium distance, dashes, jumps, attacks and raises the ward in Dream.
    static void BotInput(float t, EclipseBoss boss, PlayerController player, bool active)
    {
        var keys = new List<UnityEngine.InputSystem.Key>();
        bool left = false, right = false;
        if (active)
        {
            bool clockwise = Mathf.FloorToInt(t / 2.7f) % 2 == 0;
            keys.Add(clockwise ? UnityEngine.InputSystem.Key.D : UnityEngine.InputSystem.Key.A);
            if (boss != null)
            {
                Vector3 d = boss.transform.position - player.transform.position; d.y = 0f;
                if (d.magnitude > 21f) keys.Add(UnityEngine.InputSystem.Key.W);
                else if (d.magnitude < 12f) keys.Add(UnityEngine.InputSystem.Key.S);
            }
            if (Mathf.Repeat(t, 2.3f) < 0.1f) keys.Add(UnityEngine.InputSystem.Key.LeftShift);
            if (Mathf.Repeat(t + 1.1f, 3.7f) < 0.1f) keys.Add(UnityEngine.InputSystem.Key.Space);
            if (Mathf.Repeat(t + 3f, 7f) < 0.1f) keys.Add(UnityEngine.InputSystem.Key.E);
            bool dream = WorldState.Instance == null || WorldState.Instance.Current == World.Dream;
            if (dream)
            {
                right = Mathf.Repeat(t, 5f) > 3.2f && Mathf.Repeat(t, 5f) < 4.6f;
                left = !right && Mathf.Repeat(t, 1.5f) < 0.9f;
            }
            else left = Mathf.Repeat(t, 3f) > 0.2f;
        }
        keys.Sort();
        string keySig = string.Join(",", keys.ConvertAll(k => k.ToString()).ToArray());
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null && keySig != lastKeys)
        {
            lastKeys = keySig;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(keys.ToArray()));
        }
        string mouseSig = (left ? "L" : "") + (right ? "R" : "");
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse != null && mouseSig != lastMouse)
        {
            lastMouse = mouseSig;
            var state = new UnityEngine.InputSystem.LowLevel.MouseState();
            state = state.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, left)
                .WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, right);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, state);
        }
    }

    static void Run()
    {
        var boss = UnityEngine.Object.FindFirstObjectByType<EclipseBoss>();
        var player = LevelFlow.Player != null ? LevelFlow.Player : UnityEngine.Object.FindFirstObjectByType<PlayerController>();
        if (player == null || (boss == null && !ready)) return;
        if (boss == null && index < steps.Count && steps[index][0] != "wait" && steps[index][0] != "shot" && steps[index][0] != "shotw") { log.AppendLine("boss gone, skipped " + steps[index][0]); index++; return; }
        if (!ready)
        {
            // Leave the title or opening, then stand in the arena so the fight engages by itself.
            var pres = typeof(DreamcatcherPresentation).GetField("current", Any).GetValue(null);
            if (pres != null)
            {
                var mode = pres.GetType().GetField("mode", Any).GetValue(pres).ToString();
                if (mode != "Playing") { pres.GetType().GetMethod("Resume", Any).Invoke(pres, null); return; }
            }
            Teleport(player, EclipseEncounter.ArenaCenter + Vector3.back * 18f + Vector3.up * 0.15f);
            ready = true; waitUntil = Now + 3.5f;
            return;
        }
        if (god)
        {
            var avatar = player.GetComponentInChildren<MemoryAvatar>();
            if (avatar != null) avatar.health = 1f;
        }
        if (recording) { RecordFrame(boss, player); return; }
        if (Clock < waitUntil) return;
        if (index >= steps.Count) { Finish("OK"); return; }
        var s = steps[index++];
        switch (s[0])
        {
            case "wait": waitUntil = Clock + F(s[1]); break;
            case "record":
                // record SECONDS FOLDER: a bot plays the fight while every game frame is saved as a JPG (30 fps).
                recording = true; recFrame = 0; recTotal = Mathf.RoundToInt(F(s[1]) * 30f); recFolder = s[2];
                recLastFrame = -1; recStart = Time.time; startedAt = Now + 900f;
                Directory.CreateDirectory(recFolder);
                Time.captureFramerate = 30;
                UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
                UnityEngine.InputSystem.InputSystem.settings.editorInputBehaviorInPlayMode =
                    UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                break;
            case "time": Time.timeScale = F(s[1]); break;
            case "god": god = s[1] != "0"; break;
            case "world":
                WorldState.Instance.Set(s[1] == "N" ? World.Nightmare : World.Dream);
                break;
            case "phase":
                typeof(EclipseBoss).GetMethod("DebugPhase", Any).Invoke(boss, new object[] { int.Parse(s[1]) });
                break;
            case "attack":
            {
                var director = boss.Director;
                director.StopAll();
                var attack = director.Catalog.Find(a => a.Id == s[1]);
                if (attack == null) { log.AppendLine("no attack " + s[1]); break; }
                if (attack.Mode != AttackMode.Both)
                    WorldState.Instance.Set(attack.Mode == AttackMode.Dream ? World.Dream : World.Nightmare);
                typeof(AttackDirector).GetMethod("Launch", Any).Invoke(director, new object[] { attack, 0.2f });
                log.AppendLine("attack " + attack.Name);
                break;
            }
            case "tp":
                Teleport(player, EclipseEncounter.ArenaCenter + new Vector3(F(s[1]), F(s[2]), F(s[3])));
                break;
            case "shot":
                log.AppendLine(Capture(s[1], null, Vector3.zero, Vector3.zero, 0f));
                break;
            case "shotc":
                log.AppendLine(Capture(s[1], boss.transform, new Vector3(F(s[2]), F(s[3]), F(s[4])),
                    new Vector3(F(s[5]), F(s[6]), F(s[7])), F(s[8])));
                break;
            case "shotw":
            {
                Vector3 c = EclipseEncounter.ArenaCenter;
                log.AppendLine(CaptureWorld(s[1], c + new Vector3(F(s[2]), F(s[3]), F(s[4])),
                    c + new Vector3(F(s[5]), F(s[6]), F(s[7])), F(s[8])));
                break;
            }
            case "hurt":
                god = false; player.Damage(F(s[1]));
                break;
            case "gshot":
                // Game view capture: includes the HUD and screen effects.
                Directory.CreateDirectory("Temp/LucidShots");
                ScreenCapture.CaptureScreenshot("Temp/LucidShots/" + s[1] + ".png");
                log.AppendLine("gshot " + s[1]);
                break;
            case "kill":
                typeof(EclipseBoss).GetMethod("Die", Any).Invoke(boss, null);
                break;
            case "ui":
                // Game view including overlay canvases (Camera.Render skips those).
                Directory.CreateDirectory("Temp/LucidShots");
                ScreenCapture.CaptureScreenshot("Temp/LucidShots/" + s[1] + ".png");
                log.AppendLine("ui Temp/LucidShots/" + s[1] + ".png");
                break;
            case "pres":
            {
                var pres = typeof(DreamcatcherPresentation).GetField("current", Any).GetValue(null);
                if (pres == null) { log.AppendLine("no presentation"); break; }
                pres.GetType().GetMethod(s[1], Any, null, Type.EmptyTypes, null).Invoke(pres, null);
                log.AppendLine("pres " + s[1]);
                break;
            }
            case "rec": StartRec(s[1], F(s[2]), F(s[3])); break;
            case "noswap": noSwap = s[1] != "0"; break;
            case "vidmode": vidClock = true; Time.captureFramerate = 30; waitUntil = Clock; break;
            case "vid":
                vidDir = "C:/Users/AlHelwany/Downloads/Lucid-Combat-Videos/frames/" + s[1];
                if (Directory.Exists(vidDir)) Directory.Delete(vidDir, true);
                Directory.CreateDirectory(vidDir); vidFrame = 0; vidTotal = Mathf.RoundToInt(F(s[2]) * 30f); vidLast = -1;
                break;
            case "key": StartSim(); held[(Key)Enum.Parse(typeof(Key), s[1], true)] = Clock + F(s[2]); break;
            case "mouse": StartSim(); if (s[1] == "L") leftUntil = Clock + F(s[2]); else rightUntil = Clock + F(s[2]); break;
            case "hp":
            {
                god = false;
                var av = player.GetComponentInChildren<MemoryAvatar>();
                if (av != null) av.health = F(s[1]);
                break;
            }
            case "power":
            {
                var pcmb = player.GetComponent<PlayerCombat>();
                pcmb.AddPower(999f); pcmb.SetDread(1f);
                break;
            }
            case "presa":
            {
                // presa METHOD BACK [int]: a menu page that takes a "back" callback (and optionally a page number).
                var pres = typeof(DreamcatcherPresentation).GetField("current", Any).GetValue(null);
                if (pres == null) { log.AppendLine("no presentation"); break; }
                var backMethod = pres.GetType().GetMethod(s[2], Any, null, Type.EmptyTypes, null);
                Action back = () => backMethod.Invoke(pres, null);
                foreach (var m in pres.GetType().GetMethods(Any))
                {
                    if (m.Name != s[1]) continue;
                    var ps = m.GetParameters();
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(Action)) { m.Invoke(pres, new object[] { back }); break; }
                    if (ps.Length == 2 && ps[0].ParameterType == typeof(Action)) { m.Invoke(pres, new object[] { back, s.Length > 3 ? int.Parse(s[3]) : 0 }); break; }
                    if (ps.Length == 1 && ps[0].ParameterType == typeof(bool)) { m.Invoke(pres, new object[] { s[2] == "1" }); break; }
                }
                log.AppendLine("presa " + s[1]);
                break;
            }
            case "tut":
            {
                var tut = LucidTutorial.Active;
                if (tut == null) { log.AppendLine("no tutorial"); break; }
                typeof(LucidTutorial).GetMethod("Go", Any).Invoke(tut, new object[] { int.Parse(s[1]) });
                log.AppendLine("tut " + s[1]);
                break;
            }
            case "fonts":
                foreach (var f in Font.GetOSInstalledFontNames()) if (f.Contains("Constantia") || f.Contains("Candara") || f.Contains("Georgia") || f.Contains("Sitka") || f.Contains("Corbel")) log.AppendLine("font " + f);
                log.AppendLine("ui font " + LucidUI.Font.name + " / display " + LucidUI.Display.name);
                break;
            case "list":
                foreach (var a in boss.Director.Catalog) log.AppendLine(a.Id + " | " + a.Name + " | " + a.Mode);
                break;
            default: log.AppendLine("unknown " + s[0]); break;
        }
    }

    static void Teleport(PlayerController player, Vector3 position)
    {
        var controller = player.GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        player.transform.position = position;
        if (controller != null) controller.enabled = true;
    }

    static string Capture(string name, Transform space, Vector3 localPos, Vector3 localTarget, float fov)
    {
        if (space == null) return CaptureWorld(name, Vector3.zero, Vector3.zero, 0f);
        // Unscaled boss space, so numbers read as metres.
        Vector3 p = space.position + space.rotation * localPos, t = space.position + space.rotation * localTarget;
        return CaptureWorld(name, p, t, fov);
    }

    static string CaptureWorld(string name, Vector3 position, Vector3 target, float fov)
    {
        var cam = Camera.main;
        if (cam == null) return "No main camera";
        const int width = 1600, height = 900;
        Vector3 keepPos = cam.transform.position;
        Quaternion keepRot = cam.transform.rotation;
        float keepFov = cam.fieldOfView;
        var keepTarget = cam.targetTexture;
        if (fov > 0f)
        {
            cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
            cam.fieldOfView = fov;
        }
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        cam.targetTexture = rt;
        cam.Render();
        cam.Render();
        var keepActive = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();
        RenderTexture.active = keepActive;
        cam.targetTexture = keepTarget;
        RenderTexture.ReleaseTemporary(rt);
        cam.transform.SetPositionAndRotation(keepPos, keepRot);
        cam.fieldOfView = keepFov;
        Directory.CreateDirectory("Temp/LucidShots");
        string path = "Temp/LucidShots/" + name + ".png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        return "shot " + path;
    }
}
