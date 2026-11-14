using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Play-mode route tester for the Lucid world. It drives the real PlayerController through simulated
// keyboard input, so a gap that the bot clears is a gap a player can clear with the same simple plan.
// Script: commands separated by ';'
//   zone N                    jump to a zone spawn
//   tp X Y Z                  teleport
//   world D|N                 set the world and wait for the wave to finish
//   wait S                    stand still
//   go X Z                    walk to a point
//   jump X Z [hover] [dash] [swap] [early]   jump toward a point; swap/dash fire near the top of the arc
//   wall [q=Z] [j=Z] [aim=X,Z]   ride a wall run; press Q at z >= q, jump off at z >= j, then steer to aim
//   fly X Z [hover] [dash]    already airborne (after a bounce blossom): steer to a point and land
// The log is kept in SessionState under "botlog".
public static class WorldBot
{
    public static bool Busy { get; private set; }
    public static string Log { get { return SessionState.GetString("botlog", ""); } }

    static List<string[]> steps;
    static int index;
    static bool init;
    static float started;
    static Vector3 from;
    static bool airborne, didSwap, didDash, didJump, wasOnWall;
    static float yaw;
    static StringBuilder log;
    static readonly HashSet<Key> holds = new HashSet<Key>();
    static readonly Dictionary<Key, int> taps = new Dictionary<Key, int>();
    static string lastSent = "";
    static FieldInfo yawField, pitchField;

    public static string Run(string script)
    {
        if (!Application.isPlaying) return "Not in Play mode";
        Stop();
        steps = new List<string[]>();
        foreach (var raw in script.Split(';'))
        {
            var parts = raw.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0) steps.Add(parts);
        }
        index = 0; init = false; log = new StringBuilder();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
        yawField = typeof(ThirdPersonCamera).GetField("yaw", flags);
        pitchField = typeof(ThirdPersonCamera).GetField("pitch", flags);
        yaw = LevelFlow.Player != null ? LevelFlow.Player.transform.eulerAngles.y : 0f;
        Busy = true;
        SessionState.SetString("botlog", "running");
        EditorApplication.update += Tick;
        return "started " + steps.Count + " steps";
    }

    public static void Stop()
    {
        EditorApplication.update -= Tick;
        holds.Clear(); taps.Clear();
        if (Application.isPlaying) Send();
        Busy = false;
    }

    static float F(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

    static void Tap(Key k) { taps[k] = Time.frameCount + 2; }

    static void Send()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        var keys = new List<Key>(holds);
        foreach (var t in taps) if (Time.frameCount <= t.Value && !keys.Contains(t.Key)) keys.Add(t.Key);
        keys.Sort();
        string sig = string.Join(",", keys.ConvertAll(k => k.ToString()).ToArray());
        if (sig == lastSent) return;
        lastSent = sig;
        InputSystem.QueueStateEvent(kb, new KeyboardState(keys.ToArray()));
    }

    static void Face(float x, float z, Vector3 p)
    {
        Vector3 d = new Vector3(x - p.x, 0f, z - p.z);
        if (d.sqrMagnitude > 0.01f) yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
    }

    static void Done(string result)
    {
        var pc = LevelFlow.Player;
        log.AppendLine(string.Join(" ", steps[index]) + " -> " + result + " at " + pc.transform.position.ToString("F1") + " t=" + (Time.time - started).ToString("F2"));
        index++; init = false;
        SessionState.SetString("botlog", log.ToString() + (index < steps.Count ? "(running)" : "(done)"));
    }

    static void Fail(string result)
    {
        Done("FAILED " + result);
        log.AppendLine("stopped");
        SessionState.SetString("botlog", log.ToString() + "(stopped)");
        Stop();
    }

    static void Tick()
    {
        if (!Application.isPlaying) { Stop(); return; }
        var pc = LevelFlow.Player;
        if (pc == null) { Stop(); return; }
        if (index >= steps.Count) { Stop(); return; }
        var cam = ThirdPersonCamera.Instance;
        var s = steps[index];
        Vector3 p = pc.transform.position;
        if (!init)
        {
            init = true; started = Time.time; from = p;
            airborne = didSwap = didDash = didJump = wasOnWall = false;
            holds.Clear();
        }
        float t = Time.time - started;
        bool has(string flag) { for (int i = 1; i < s.Length; i++) if (s[i] == flag) return true; return false; }
        string arg(string name) { for (int i = 1; i < s.Length; i++) if (s[i].StartsWith(name + "=")) return s[i].Substring(name.Length + 1); return null; }

        switch (s[0])
        {
            case "zone":
                LevelFlow.Instance.MoveTo(int.Parse(s[1]));
                yaw = 0f;
                Done("ok");
                break;
            case "tp":
                pc.Teleport(new Vector3(F(s[1]), F(s[2]), F(s[3])), 0f);
                Done("ok");
                break;
            case "world":
                {
                    World want = s[1] == "N" ? World.Nightmare : World.Dream;
                    if (t < 0.01f && WorldState.Instance.Current != want) WorldState.Instance.Set(want);
                    if (t > 0.1f && !WorldShift.Instance.Running) Done("ok");
                    break;
                }
            case "wait":
                if (t >= F(s[1])) Done("ok");
                break;
            case "go":
                {
                    float x = F(s[1]), z = F(s[2]);
                    Face(x, z, p);
                    holds.Add(Key.W);
                    float d = new Vector2(x - p.x, z - p.z).magnitude;
                    if (d < 0.4f) Done("ok");
                    else if (p.y < from.y - 8f) Fail("fell");
                    else if (t > 8f) Fail("timeout, " + d.ToString("F1") + " m short");
                    break;
                }
            case "jump":
                {
                    float x = F(s[1]), z = F(s[2]);
                    Face(x, z, p);
                    float d = new Vector2(x - p.x, z - p.z).magnitude;
                    if (d > 0.35f) holds.Add(Key.W); else holds.Remove(Key.W);
                    if (!didJump) { didJump = true; if (has("hover")) holds.Add(Key.Space); else Tap(Key.Space); }
                    if (has("hover")) holds.Add(Key.Space);
                    if (!pc.Grounded && t > 0.08f) airborne = true;
                    float top = has("early") ? 6f : 2f;
                    if (airborne && has("swap") && !didSwap && pc.VerticalSpeed < top) { didSwap = true; Tap(Key.Q); }
                    if (airborne && has("dash") && !didDash && pc.VerticalSpeed < 1f) { didDash = true; Tap(Key.LeftShift); }
                    if (airborne && pc.WallRunning) Done("on wall");
                    else if (airborne && pc.VerticalSpeed > 12f) Done("bounced");
                    else if (airborne && pc.Grounded && t > 0.25f) Done("landed " + d.ToString("F1") + " m from target");
                    else if (p.y < from.y - 9f) Fail("fell");
                    else if (t > 6f) Fail("timeout");
                    break;
                }
            case "fly":
                {
                    // Already in the air (after a bounce): steer to a point and land.
                    float x = F(s[1]), z = F(s[2]);
                    Face(x, z, p);
                    float d = new Vector2(x - p.x, z - p.z).magnitude;
                    if (d > 0.35f) holds.Add(Key.W); else holds.Remove(Key.W);
                    if (has("hover") && pc.VerticalSpeed < 0f) holds.Add(Key.Space); else holds.Remove(Key.Space);
                    if (has("dash") && !didDash && pc.VerticalSpeed < 1f) { didDash = true; Tap(Key.LeftShift); }
                    if (pc.Grounded && t > 0.2f) Done("landed " + d.ToString("F1") + " m from target");
                    else if (p.y < from.y - 9f) Fail("fell");
                    else if (t > 6f) Fail("timeout");
                    break;
                }
            case "wall":
                {
                    string q = arg("q"), j = arg("j"), aim = arg("aim");
                    if (pc.WallRunning)
                    {
                        wasOnWall = true;
                        holds.Add(Key.W);
                        if (q != null && !didSwap && p.z >= F(q)) { didSwap = true; Tap(Key.Q); }
                        if (j != null && !didJump && p.z >= F(j)) { didJump = true; Tap(Key.Space); }
                    }
                    else if (wasOnWall)
                    {
                        if (aim != null) { var a = aim.Split(','); Face(F(a[0]), F(a[1]), p); holds.Add(Key.W); }
                        if (pc.Grounded) Done("landed");
                    }
                    else if (t > 0.6f) Fail("no wall run");
                    if (init && p.y < from.y - 9f) Fail("fell");
                    else if (init && t > 8f) Fail("timeout");
                    break;
                }
            default:
                Done("unknown command");
                break;
        }

        if (cam != null && yawField != null)
        {
            yawField.SetValue(cam, yaw);
            pitchField.SetValue(cam, 12f);
        }
        Send();
    }
}
