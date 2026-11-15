using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// How to play: one window. A title, a few plain sentences, and under them a short looping clip of real
// gameplay (frames recorded from the game into Resources/Tutorial). Back, Next, or Skip the whole thing.
public sealed class LucidTutorial : MonoBehaviour
{
    public static LucidTutorial Active { get; private set; }

    public static void CloseActive()
    {
        if (Active == null) return;
        var t = Active; Active = null;
        // Deactivate first: Destroy is deferred, and the menu that opens next must not see Next as still selected.
        t.gameObject.SetActive(false);
        Destroy(t.gameObject);
    }

    public static LucidTutorial Open(Transform canvas, EventSystem events, Action onClose)
    {
        CloseActive();
        var go = new GameObject("How To Play", typeof(RectTransform));
        go.transform.SetParent(canvas, false);
        var r = (RectTransform)go.transform;
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        var t = go.AddComponent<LucidTutorial>();
        t.events = events; t.onClose = onClose;
        Active = t;
        t.Build();
        return t;
    }

    struct Page { public string title, clip; public Func<string> text; }

    const int Cols = 9, Rows = 8, Frames = 72;
    const float Fps = 12f;

    EventSystem events;
    Action onClose;
    Page[] pages;
    int index;
    float clock;
    Text title, body, nextLabel;
    RawImage clip;
    Texture2D clipTex;
    readonly List<LucidRing> dots = new List<LucidRing>();
    GameObject back;
    readonly Dictionary<string, Texture2D> clips = new Dictionary<string, Texture2D>();

    void Build()
    {
        pages = new[]
        {
            new Page { title = "Two worlds", clip = "shift", text = () =>
                $"Press {Key(LucidInput.SwapStance)} to switch between Dream and Nightmare. Dream keeps you alive, and Nightmare is where you do real damage. The Sleeper also switches the world by itself when its timer runs out." },
            new Page { title = "Dash", clip = "dash", text = () =>
                $"Press {Key(LucidInput.Dash)} to turn into smoke and dash. Attacks pass through you while you dash. You have two dashes, and each one comes back after a few seconds." },
            new Page { title = "Ward", clip = "ward", text = () =>
                $"In Dream, hold {Key(LucidInput.Aim)} to raise your Ward. Catch the pink seeds to heal. Raise it just before a seed reaches you and you heal twice as much." },
            new Page { title = "Bow", clip = "bow", text = () =>
                $"In Dream, hold {Key(LucidInput.Fire)} to draw your bow and let go to shoot. Your arrows mark the Sleeper's weak points." },
            new Page { title = "Guns", clip = "guns", text = () =>
                $"In Nightmare, {Key(LucidInput.Fire)} fires your gun. Shoot the weak points you marked in Dream for extra damage. Press {Key(LucidInput.Weapon1)}, {Key(LucidInput.Weapon2)} or {Key(LucidInput.Weapon3)} to change guns and {Key(LucidInput.Reload)} to reload." },
            new Page { title = "Abilities", clip = "abilities", text = () =>
                $"Press {Key(LucidInput.Ability1)}, {Key(LucidInput.Ability2)} and {Key(LucidInput.Overdrive)} to use your abilities. In Dream they protect and heal you. In Nightmare they hit the Sleeper hard." },
        };

        var self = (RectTransform)transform;
        var dim = LucidUI.Stretch(self, "Dim", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, LucidUI.Alpha(LucidUI.Night, .86f));
        dim.raycastTarget = true;

        var window = R(self, "Window", new Vector2(0, 0), new Vector2(720, 600));
        LucidUI.ShadowFor(window, .75f, 40f, 16f);
        var plate = window.gameObject.AddComponent<Image>();
        plate.color = LucidUI.Alpha(LucidUI.Panel, .97f); plate.raycastTarget = false;

        title = T(window, new Vector2(0, 254), new Vector2(660, 50), "", 34, LucidUI.Cream, TextAnchor.MiddleCenter, true);
        body = T(window, new Vector2(0, 182), new Vector2(620, 96), "", 19, LucidUI.Alpha(LucidUI.Cream, .9f), TextAnchor.MiddleCenter);
        body.lineSpacing = 1.1f;

        var frame = R(window, "Clip", new Vector2(0, -38), new Vector2(592, 333));
        LucidUI.ShadowFor(frame, .6f, 18f, 7f);
        var bg = frame.gameObject.AddComponent<Image>(); bg.color = LucidUI.Night; bg.raycastTarget = false;
        clip = new GameObject("Frames", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        clip.transform.SetParent(frame, false);
        var cr = clip.rectTransform; cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one; cr.offsetMin = cr.offsetMax = Vector2.zero;
        clip.raycastTarget = false;

        for (int i = 0; i < pages.Length; i++)
            dots.Add(Ring(window, new Vector2((i - (pages.Length - 1) / 2f) * 22f, -252), 10, 1.5f, LucidUI.Cream));

        Button(window, new Vector2(-240, -254), 170, "Skip tutorial", Close);
        back = Button(window, new Vector2(150, -254), 100, "Back", () => Go(index - 1)).gameObject;
        var next = Button(window, new Vector2(262, -254), 110, "Next", () => { if (index == pages.Length - 1) Close(); else Go(index + 1); });
        nextLabel = next.GetComponentInChildren<Text>();

        Go(0);
        if (events != null) events.SetSelectedGameObject(next.gameObject);
    }

    Button Button(RectTransform parent, Vector2 pos, float width, string label, Action click)
    {
        var r = R(parent, label, pos, new Vector2(width, 40));
        var hit = r.gameObject.AddComponent<Image>();
        hit.color = Color.clear;
        var text = LucidUI.Text(r, label, 20, LucidUI.Cream, TextAnchor.MiddleCenter);
        var tr = text.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
        // Selected: a thread is drawn under the word with a bead at its start, like the string of a dreamcatcher.
        var thread = LucidUI.Stretch(r, "Thread", new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(-width * .4f, 4f), new Vector2(width * .4f, 6f), LucidUI.Dream);
        thread.rectTransform.pivot = new Vector2(0f, .5f);
        thread.rectTransform.anchoredPosition = new Vector2(-width * .4f, 5f);
        var bead = Ring(r, new Vector2(-width * .4f, -16f), 8, 4, LucidUI.Dream);
        var button = r.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = hit;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => { LucidAudio.Play(LucidAudio.Cue.Capture); click(); });
        r.gameObject.AddComponent<LucidButtonFx>().Init(thread, bead, text);
        return button;
    }

    void Go(int target)
    {
        index = Mathf.Clamp(target, 0, pages.Length - 1);
        var p = pages[index];
        title.text = p.title;
        body.text = p.text();
        clipTex = LoadClip(p.clip);
        clip.texture = clipTex;
        clip.enabled = clipTex != null;
        clock = 0f;
        for (int i = 0; i < dots.Count; i++)
        {
            dots[i].thickness = i == index ? 5f : 1.5f;
            dots[i].color = i == index ? LucidUI.Cream : LucidUI.Alpha(LucidUI.Cream, .5f);
            dots[i].SetVerticesDirty();
        }
        back.SetActive(index > 0);
        nextLabel.text = index == pages.Length - 1 ? "Let's play" : "Next";
        Update();
    }

    Texture2D LoadClip(string name)
    {
        if (!clips.TryGetValue(name, out var tex)) clips[name] = tex = Resources.Load<Texture2D>("Tutorial/" + name);
        return tex;
    }

    void Close()
    {
        var done = onClose;
        Active = null;
        gameObject.SetActive(false);
        Destroy(gameObject);
        done?.Invoke();
    }

    void OnDestroy() { if (Active == this) Active = null; }

    void Update()
    {
        if (pages == null) return;
        var kb = Keyboard.current; var pad = Gamepad.current;
        bool right = kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
            || pad != null && (pad.rightShoulder.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame);
        bool left = kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
            || pad != null && (pad.leftShoulder.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame);
        if (right && index < pages.Length - 1) { LucidAudio.Play(LucidAudio.Cue.Capture); Go(index + 1); return; }
        if (left && index > 0) { LucidAudio.Play(LucidAudio.Cue.Capture); Go(index - 1); return; }

        if (clipTex == null) return;
        clock += Time.unscaledDeltaTime;
        int f = (int)(clock * Fps) % Frames;
        clip.uvRect = new Rect((f % Cols) / (float)Cols, 1f - (f / Cols + 1) / (float)Rows, 1f / Cols, 1f / Rows);
    }

    // "the right mouse button" reads better in a sentence than "RMB".
    static string Key(InputAction a)
    {
        string s = a != null ? LucidInput.Label(a) : "";
        switch (s)
        {
            case "LMB": return "the left mouse button";
            case "RMB": return "the right mouse button";
            case "": return "the key";
        }
        return s;
    }

    static RectTransform R(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        r.SetParent(parent, false);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return r;
    }

    static LucidRing Ring(Transform parent, Vector2 pos, float diameter, float thickness, Color color)
    {
        var r = R(parent, "Ring", pos, Vector2.one * diameter);
        var ring = r.gameObject.AddComponent<LucidRing>();
        ring.thickness = thickness; ring.color = color; ring.raycastTarget = false;
        return ring;
    }

    static Text T(Transform parent, Vector2 pos, Vector2 size, string value, int fontSize, Color color, TextAnchor align, bool display = false)
    {
        var t = LucidUI.Text(parent, value, fontSize, color, align, 0f, display);
        var r = t.rectTransform;
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
        r.anchoredPosition = pos; r.sizeDelta = size;
        return t;
    }
}
