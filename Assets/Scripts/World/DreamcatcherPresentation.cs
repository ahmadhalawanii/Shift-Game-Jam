using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(1000)]
public sealed class DreamcatcherPresentation : MonoBehaviour
{
    const string ScenePath = "Assets/Lucid/World/Scenes/LucidWorld.unity";
    const string VolumeKey = "Dreamcatcher.MasterVolume";
    const string LookKey = "Dreamcatcher.LookScale";
    const string ShakeKey = "Dreamcatcher.ReduceShake";
    const string FlashKey = "Dreamcatcher.ReduceFlash";
    public static bool ReduceFlash => PlayerPrefs.GetInt(FlashKey) != 0;
    enum Mode { Title, Opening, Playing, Paused, Fusion, Ending, Results, Defeat, Loading }
    static DreamcatcherPresentation current;
    static bool checkpointReload;
    static bool startAfterReload;
    InputActionRebindingExtensions.RebindingOperation rebinding;
    static float runTime;
    public static bool OwnsScreen => current != null;
    public static bool CoversHud => current != null && current.mode != Mode.Playing;

    Mode mode;
    PlayerController player;
    PlayerCombat combat;
    LevelFlow level;
    ThirdPersonCamera rig;
    Camera view;
    Transform subject;
    Vector3 rigPosition;
    Quaternion rigRotation;
    float rigFov;
    float sensitivity, verticalSensitivity, impulse, fovKick;
    bool restorePlayer, restoreCombat, restoreRig, locked;
    float elapsed;
    int lastShot = -1;
    Canvas canvas;
    RectTransform panel;
    UnityEngine.UI.Image fade;
    CanvasGroup panelGroup;
    Font font;
    EventSystem events;
    Texture2D menuShade;
    UnityEngine.UI.RawImage shade;
    bool inPanel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Register()
    {
        current = null;
        checkpointReload = false;
        startAfterReload = false;
        runTime = 0f;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        if (scene.path == ScenePath)
            new GameObject("Dreamcatcher Presentation").AddComponent<DreamcatcherPresentation>();
    }

    void Start()
    {
        level = FindFirstObjectByType<LevelFlow>();
        player = level != null ? level.player : null;
        rig = ThirdPersonCamera.Instance;
        view = rig != null ? rig.GetComponent<Camera>() : null;
        if (player == null || view == null) { Destroy(gameObject); return; }
        current = this;
        combat = player.GetComponent<PlayerCombat>();
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        sensitivity = rig.sensitivity;
        verticalSensitivity = rig.verticalSensitivity;
        impulse = rig.impulseStrength;
        fovKick = rig.fovKick;
        ApplyCameraPreferences();
        BuildCanvas();
        if (checkpointReload)
        {
            checkpointReload = false;
            Enter(Mode.Playing);
            OfferPointerCapture();
            return;
        }

        LockGameplay();
        Enter(Mode.Title);
        ShowTitle();
        if (startAfterReload)
        {
            startAfterReload = false;
            StartOpening();
        }
    }

    void Update()
    {
        if (rebinding != null) return;
        elapsed += Time.unscaledDeltaTime;
        var keyboard = Keyboard.current;
        bool escape = LucidInput.Pause.WasPressedThisFrame();
        bool skip = elapsed > 0.4f && keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
        switch (mode)
        {
            case Mode.Title:
                panelGroup.alpha = Mathf.Clamp01(elapsed / 0.55f);
                SetFade(1f - panelGroup.alpha);
                if (escape && inPanel) ShowTitle();
                break;
            case Mode.Opening:
                SetFade(1f - Mathf.Clamp01(elapsed / 0.5f));
                if (elapsed >= 7f || skip) Resume();
                break;
            case Mode.Playing:
                runTime += Time.unscaledDeltaTime;
                if (EclipseEncounter.Finished) ShowOutcome(false);
                else if (escape && Time.timeScale >= 1f) Pause();
                break;
            case Mode.Paused:
                if (escape && inPanel) ShowPause();
                else if (escape) Resume();
                break;
            case Mode.Fusion:
                if (skip || elapsed >= 5.4f) Resume();
                break;
            case Mode.Ending:
                if (skip || elapsed >= 11f) ShowOutcome(true);
                break;
        }
    }

    void LateUpdate()
    {
        if (mode == Mode.Title || mode == Mode.Opening)
        {
            var basePoint = player.transform.position;
            var chest = basePoint + Vector3.up * 1.12f;
            var forward = player.transform.forward;
            var right = player.transform.right;
            Vector3 orbit = chest + forward * 2.25f + right * (1.45f + 0.38f * Mathf.Sin(elapsed * Mathf.PI / 7f));

            if (mode == Mode.Title)
                Aim(basePoint - forward * 9f + right * (5f + 0.5f * Mathf.Sin(elapsed * 0.12f)) + Vector3.up * 6f,
                    basePoint + forward * 27f - right * 12f + Vector3.up * 12f, 58f);
            else if (elapsed < 2.1f)
            {
                ShotCue(0, "intro_thread");
                Aim(basePoint - forward * 8f + right * 4f + Vector3.up * 5f,
                    chest + forward * 18f, 54f);
            }
            else if (elapsed < 3.8f)
            {
                ShotCue(1, "intro_fracture_impact");
                Aim(basePoint - forward * 4.2f + right * 2.1f + Vector3.up * (2.6f + elapsed * 0.23f),
                    chest + forward * 13f, 57f);
            }
            else
            {
                ShotCue(2, "intro_awakening");
                Aim(orbit, chest, 44f);
                BlendToGameplay(Mathf.InverseLerp(5.5f, 7f, elapsed));
            }
        }
        else if ((mode == Mode.Fusion || mode == Mode.Ending) && subject != null)
        {
            bool ending = mode == Mode.Ending;
            var core = subject.position;
            var hero = player.transform.position + Vector3.up * 1.1f;
            var axis = Vector3.ProjectOnPlane(hero - core, Vector3.up).normalized;
            if (axis.sqrMagnitude < 0.5f) axis = Vector3.back;
            var side = Vector3.Cross(Vector3.up, axis);

            if (elapsed < 1.6f)
            {
                ShotCue(0, ending ? "ending_bind_impact" : "fusion_mask_impact");
                // Pulled back from 7 m: the Colossus head is deeper than the old blossom, so 7 m sat inside its face.
                Aim(core + axis * 15f + side * (3f + elapsed * 1.5f) + Vector3.up * 0.5f, core + Vector3.up * 0.5f, 47f);
            }
            else if (ending && elapsed >= 3.5f && elapsed < 6f)
            {
                ShotCue(2, "ending_loomheart");
                Aim(hero - axis * 2.9f + side * 1.8f, hero + (core - hero) * 0.08f, 44f);
            }
            else
            {
                ShotCue(elapsed >= 6f ? 3 : 1, ending ? "ending_resolve" : "fusion_world_impact");
                Aim(core + axis * (15f + elapsed * 0.5f) + side * 7f + Vector3.up * (2f + elapsed * 0.35f),
                    core * 0.8f + hero * 0.2f, 58f);
            }
            if (!ending) BlendToGameplay(Mathf.InverseLerp(4.2f, 5.4f, elapsed));
        }
    }

    void ShotCue(int shot, string cue)
    {
        if (lastShot == shot) return;
        lastShot = shot;
        LucidAudio.Hook(cue);
    }

    void Aim(Vector3 position, Vector3 target, float fov)
    {
        view.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
        view.fieldOfView = fov;
    }

    void BlendToGameplay(float blend)
    {
        blend = Mathf.SmoothStep(0f, 1f, blend);
        view.transform.SetPositionAndRotation(Vector3.Lerp(view.transform.position, rigPosition, blend),
            Quaternion.Slerp(view.transform.rotation, rigRotation, blend));
        view.fieldOfView = Mathf.Lerp(view.fieldOfView, rigFov, blend);
    }

    void LockGameplay()
    {
        if (locked) return;
        locked = true;
        rigPosition = view.transform.position;
        rigRotation = view.transform.rotation;
        rigFov = view.fieldOfView;
        restorePlayer = player.enabled;
        restoreCombat = combat != null && combat.enabled;
        restoreRig = rig.enabled;
        player.enabled = false;
        if (combat != null) combat.enabled = false;
        rig.enabled = false;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void Resume()
    {
        if (!locked) return;
        locked = false;
        view.transform.SetPositionAndRotation(rigPosition, rigRotation);
        view.fieldOfView = rigFov;
        rig.enabled = restoreRig;
        player.enabled = restorePlayer;
        if (combat != null) combat.enabled = restoreCombat;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        Enter(Mode.Playing);
        ClearPanel();
        SetFade(0f);
        OfferPointerCapture();
    }

    void OfferPointerCapture()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ClearPanel();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AddButton("CLICK TO CAPTURE MOUSE", () => { CapturePointer(); ClearPanel(); }, 44);
#else
        CapturePointer();
#endif
    }

    static void CapturePointer()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InstallForDirectLaunch()
    {
        // The sceneLoaded callback also handles reloads and subsequent scenes.
        if (SceneManager.GetActiveScene().path == ScenePath &&
            FindFirstObjectByType<DreamcatcherPresentation>() == null)
            new GameObject("Dreamcatcher Presentation").AddComponent<DreamcatcherPresentation>();
    }

    public static void PlayFusion(EclipseBoss boss)
    {
        if (current == null || current.mode != Mode.Playing) return;
        current.ShowCinematic(Mode.Fusion, boss.transform, "THE ECLIPSE WEAVER");
    }

    public static bool PlayEnding(EclipseBoss boss)
    {
        if (current == null || current.mode != Mode.Playing) return false;
        current.ShowCinematic(Mode.Ending, boss.transform, "THE VEIL WAS NEVER MEANT TO DIVIDE.");
        return true;
    }

    void ShowCinematic(Mode next, Transform focus, string caption)
    {
        LockGameplay();
        subject = focus;
        Enter(next);
        ShowSkip(caption);
    }

    void Enter(Mode next)
    {
        mode = next;
        if (shade != null) shade.gameObject.SetActive(next == Mode.Title || next == Mode.Paused || next == Mode.Results || next == Mode.Defeat);
        elapsed = 0f;
        lastShot = -1;
    }

    void StartOpening()
    {
        if (mode != Mode.Title) return;
        Enter(Mode.Opening);
        ShowSkip(null);
        SetFade(1f);
        LucidAudio.Play(LucidAudio.Cue.Capture);
    }

    void ShowSkip(string caption)
    {
        ClearPanel();
        if (!string.IsNullOrEmpty(caption)) AddText(caption, 23, 54, accent);
        AddText("SPACE  /  SKIP", 18, 36, ivory);
        panel.anchorMin = panel.anchorMax = new Vector2(1f, 0f);
        panel.pivot = new Vector2(1f, 0f);
        panel.anchoredPosition = new Vector2(-38f, 30f);
    }

    void Pause()
    {
        LockGameplay();
        AudioListener.pause = true;
        Enter(Mode.Paused);
        ShowPause();
    }

    void Load(bool checkpoint)
    {
        if (mode == Mode.Loading) return;
        checkpointReload = checkpoint;
        if (checkpoint) EclipseEncounter.PrepareCheckpointRetry();
        else
        {
            LevelFlow.ResetCheckpoint();
            runTime = 0f;
        }
        Enter(Mode.Loading);
        ClearPanel();
        SetFade(1f);
        AddText("WEAVING THE DREAM…", 24, 64, ivory);
        AudioListener.pause = false;
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
    }

    static readonly Color ivory = new Color(0.97f, 0.93f, 0.86f);
    static readonly Color accent = new Color(0.57f, 0.92f, 0.89f);

    void ShowTitle()
    {
        ClearPanel();
        AddText("Lucid", 104, 112, LucidUI.Cream, 0f, true);
        var tagline = AddText("between two worlds", 24, 44, accent, 0f, true);
        tagline.fontStyle = FontStyle.Italic;
        AddRule(accent, 0f);
        AddButton("Play", StartOpening, 52, true);
        AddButton("How to play", ShowHelp);
        AddButton("Controls", () => ShowControls(ShowTitle));
        AddButton("Settings", () => ShowSettings(ShowTitle));
        AddButton("Credits", () => ShowCredits(ShowTitle));
        AddText("Made for the SHIFT Game Jam 2026", 14, 52, LucidUI.Alpha(LucidUI.Cream, .55f));
    }

    void ShowHelp() => ShowTutorial(ShowTitle);

    // The field guide sits over the menu; Esc (handled above through inPanel) or DONE returns to it.
    void ShowTutorial(Action back)
    {
        ClearPanel();
        inPanel = true;
        LucidTutorial.Open(canvas.transform, events, back);
    }

    // Spacer between the title and the options.
    void AddRule(Color color, float width)
    {
        var row = new GameObject("Gap", typeof(RectTransform), typeof(UnityEngine.UI.LayoutElement));
        row.transform.SetParent(panel, false);
        row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 18f;
    }

    // Menu strings are written in capitals in the code; on screen they read in normal case.
    static string Nice(string s)
    {
        if (string.IsNullOrEmpty(s) || s.ToUpperInvariant() != s) return s;
        var t = System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(s.ToLowerInvariant());
        return t.Replace("Lmb", "LMB").Replace("Rmb", "RMB").Replace(" To ", " to ").Replace(" Of ", " of ").Replace(" The ", " the ");
    }
    void ShowControls(Action back)
    {
        ClearPanel(true);
        AddText("CONTROLS", 44, 60, ivory);
        AddText(LucidInput.Label(LucidInput.Move)+"  Move / "+LucidInput.Label(LucidInput.Look)+"  Look\n"+
            LucidInput.Label(LucidInput.Fire)+"  Draw bow / fire\n"+
            LucidInput.Label(LucidInput.Aim)+"  Ward / aim    •    "+LucidInput.Label(LucidInput.SwapShoulder)+"  Shoulder\n"+
            LucidInput.Label(LucidInput.Dash)+"  Dash    •    "+LucidInput.Label(LucidInput.Jump)+"  Jump\n"+
            LucidInput.Label(LucidInput.SwapStance)+"  Shift    •    "+LucidInput.Label(LucidInput.Reload)+"  Reload\n"+
            LucidInput.Label(LucidInput.Ability1)+"  Pulse / Burst    •    "+LucidInput.Label(LucidInput.Ability2)+"  Bloom / Shadowstep\n"+
            LucidInput.Label(LucidInput.Overdrive)+"  Overdrive    •    "+LucidInput.Label(LucidInput.NextWeapon)+"  Next weapon\n"+
            "PINK seeds heal. Gold and cyan drain Ward. Red: dodge.", 18, 240, ivory);
        AddButton("INVERT Y   "+(LucidInput.invertY?"ON":"OFF"),()=>{LucidInput.invertY=!LucidInput.invertY;PlayerPrefs.SetInt("Lucid.InvertY",LucidInput.invertY?1:0);ShowControls(back);});
        AddButton("AIM / WARD   "+(LucidInput.aimToggle?"TOGGLE":"HOLD"),()=>{LucidInput.aimToggle=!LucidInput.aimToggle;PlayerPrefs.SetInt("Lucid.AimToggle",LucidInput.aimToggle?1:0);ShowControls(back);});
        AddButton("CHANGE BINDINGS",()=>ShowBindings(back,0));
        AddButton("BACK", back);
    }
    void ShowBindings(Action back,int page)
    {
        ClearPanel(true); AddText("CHANGE BINDINGS",36,55,ivory);
        var actions = new System.Collections.Generic.List<InputAction>();
        var bindings = new System.Collections.Generic.List<int>();
        string group = LucidInput.UsingGamepad ? LucidInput.Pad : LucidInput.Kbm;
        foreach (var a in LucidInput.Actions.actions)
            for (int j = 0; j < a.bindings.Count; j++)
                if (!a.bindings[j].isComposite && !string.IsNullOrEmpty(a.bindings[j].groups) && a.bindings[j].groups.Contains(group))
                { actions.Add(a); bindings.Add(j); }
        int pages = Mathf.Max(1, Mathf.CeilToInt(actions.Count / 4f));
        page %= pages;
        for(int i=page*4;i<Mathf.Min(actions.Count,page*4+4);i++)
        {
            var action=actions[i];
            int binding = bindings[i];
            string part = action.bindings[binding].isPartOfComposite ? " / " + action.bindings[binding].name : "";
            AddButton(action.name.ToUpper()+part+"   "+action.GetBindingDisplayString(binding),()=>
            {
                ClearPanel(true);AddText("PRESS A BUTTON\nEsc cancels",26,100,ivory);action.Disable();
                rebinding=action.PerformInteractiveRebinding(binding).WithControlsExcluding("<Mouse>/position").WithCancelingThrough("<Keyboard>/escape")
                    .OnComplete(op=>{op.Dispose();rebinding=null;action.Enable();LucidInput.SaveBindings();ShowBindings(back,page);})
                    .OnCancel(op=>{op.Dispose();rebinding=null;action.Enable();ShowBindings(back,page);});
                if (action != LucidInput.Look) rebinding.WithControlsExcluding("<Mouse>/delta");
                rebinding.Start();
            });
        }
        AddButton("NEXT PAGE  "+(page+1)+" / "+pages,()=>ShowBindings(back,(page+1)%pages));
        AddButton("BACK",()=>ShowControls(back));
    }

    void ShowSettings(Action back)
    {
        ClearPanel(true);
        AddText("SETTINGS", 44, 65, ivory);
        AddSlider("MASTER VOLUME", VolumeKey, 0f, 1f, value => AudioListener.volume = value);
        AddSlider("LOOK SENSITIVITY", LookKey, 0.5f, 2f, _ => ApplyCameraPreferences());
        AddToggle("REDUCED SHAKE", ShakeKey, back);
        AddToggle("REDUCED FLASH", FlashKey, back);
        AddButton("BACK", back);
    }

    void ApplyCameraPreferences()
    {
        float scale = PlayerPrefs.GetFloat(LookKey, 1f);
        bool reduced = PlayerPrefs.GetInt(ShakeKey) != 0;
        rig.sensitivity = sensitivity * scale;
        rig.verticalSensitivity = verticalSensitivity * scale;
        rig.impulseStrength = reduced ? 0f : impulse;
        rig.fovKick = reduced ? 0f : fovKick;
    }

    void AddToggle(string title, string key, Action back)
    {
        bool value = PlayerPrefs.GetInt(key) != 0;
        AddButton(title + (value ? "   ON" : "   OFF"), () =>
        {
            PlayerPrefs.SetInt(key, value ? 0 : 1);
            PlayerPrefs.Save();
            ApplyCameraPreferences();
            ShowSettings(back);
        });
    }

    void AddSlider(string title, string key, float min, float max, Action<float> changed)
    {
        float initial = PlayerPrefs.GetFloat(key, 1f);
        var label = AddText($"{title}   {initial:P0}", 21, 32, ivory);
        var row = new GameObject(title, typeof(RectTransform), typeof(UnityEngine.UI.Slider), typeof(UnityEngine.UI.LayoutElement));
        row.transform.SetParent(panel, false);
        row.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 32f;
        var track = new GameObject("Track", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
        track.transform.SetParent(row.transform, false);
        track.rectTransform.anchorMin = Vector2.zero;
        track.rectTransform.anchorMax = Vector2.one;
        track.rectTransform.offsetMin = new Vector2(0f, 7f);
        track.rectTransform.offsetMax = new Vector2(0f, -7f);
        track.color = new Color(0.25f, 0.19f, 0.37f);
        var fill = new GameObject("Fill", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
        fill.transform.SetParent(track.transform, false);
        fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
        fill.color = accent;
        var slider = row.GetComponent<UnityEngine.UI.Slider>();
        slider.targetGraphic = track;
        slider.fillRect = fill.rectTransform;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = initial;
        slider.onValueChanged.AddListener(value =>
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            changed(value);
            label.text = $"{title}   {value:P0}";
        });
        if (events.currentSelectedGameObject == null || !events.currentSelectedGameObject.activeInHierarchy)
            events.SetSelectedGameObject(row);
    }

    void ShowCredits(Action back)
    {
        ClearPanel(true);
        AddText("CREDITS", 44, 65, ivory);
        AddText("SHIFT Game Jam 2026\nMade with Unity 6\n\nLucid world art and synthesized audio\nLucid presentation assisted by Codex\n\nCristianQiu · URP Volumetric Light (MIT)\nPoly Haven · environmental assets (CC0)", 22, 235, ivory);
        AddButton("BACK", back);
    }

    void ShowPause()
    {
        ClearPanel(true);
        AddText("Paused", 46, 72, LucidUI.Cream, 0f, true);
        AddButton("RESUME", Resume, 52, true);
        AddButton("RESTART CHECKPOINT", () => Load(true));
        AddButton("HOW TO PLAY", () => ShowTutorial(ShowPause));
        AddButton("SETTINGS", () => ShowSettings(ShowPause));
        AddButton("CONTROLS", () => ShowControls(ShowPause));
        AddButton("MAIN MENU", () => Load(false));
        inPanel = false;
    }

    void ShowOutcome(bool victory)
    {
        LockGameplay();
        Enter(victory ? Mode.Results : Mode.Defeat);
        ClearPanel(true);
        AddText(victory ? "THE VEIL IS RESTORED" : "THE DREAM FADES", 43, 65, ivory);
        if (victory)
        {

            var duration = TimeSpan.FromSeconds(runTime);
            AddText($"RUN TIME   {((int)duration.TotalMinutes):00}:{duration.Seconds:00}\nWORLD BALANCE   RESTORED", 23, 92, accent);
        }
        else AddButton("RETRY CHECKPOINT", () => Load(true), 60, true);
        AddButton(victory ? "PLAY AGAIN" : "RESTART RUN", RestartRun);
        AddButton("MAIN MENU", () => Load(false));
        AddButton("VIEW CREDITS", () => ShowCredits(() => ShowOutcome(victory)));
    }

    void RestartRun()
    {
        startAfterReload = true;
        Load(false);
    }

    void BuildCanvas()
    {
        font = LucidUI.Font;
        var ui = new GameObject("Dreamcatcher Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        ui.transform.SetParent(transform, false);
        canvas = ui.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        var scaler = ui.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 0.5f;

        menuShade = new Texture2D(128, 1, TextureFormat.RGBA32, false);
        menuShade.wrapMode = TextureWrapMode.Clamp;
        for (int x = 0; x < 128; x++)
        {
            float opacity = 0.83f * (1f - Mathf.SmoothStep(0f, 1f, x / 127f));
            menuShade.SetPixel(x, 0, new Color(0.018f, 0.035f, 0.075f, opacity));
        }
        menuShade.Apply();
        shade = new GameObject("Menu atmosphere", typeof(UnityEngine.UI.RawImage)).GetComponent<UnityEngine.UI.RawImage>();
        shade.transform.SetParent(ui.transform, false);
        shade.texture = menuShade;
        shade.raycastTarget = false;
        shade.rectTransform.anchorMin = Vector2.zero;
        shade.rectTransform.anchorMax = new Vector2(0.68f, 1f);
        shade.rectTransform.offsetMin = shade.rectTransform.offsetMax = Vector2.zero;

        fade = new GameObject("Fade", typeof(UnityEngine.UI.Image)).GetComponent<UnityEngine.UI.Image>();
        fade.transform.SetParent(ui.transform, false);
        fade.rectTransform.anchorMax = Vector2.one;
        fade.rectTransform.offsetMin = fade.rectTransform.offsetMax = Vector2.zero;
        fade.raycastTarget = false;
        SetFade(0f);

        events = FindFirstObjectByType<EventSystem>();
        if (events == null)
        {
            var input = new GameObject("Dreamcatcher EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            input.transform.SetParent(transform, false);
            events = input.GetComponent<EventSystem>();
        }
    }

    void ClearPanel(bool backdrop = false)
    {
        LucidTutorial.CloseActive();
        if (panel != null)
        {
            panel.gameObject.SetActive(false);
            Destroy(panel.gameObject);
        }
        inPanel = backdrop;
        var root = new GameObject("Menu", typeof(RectTransform), typeof(CanvasGroup), typeof(UnityEngine.UI.VerticalLayoutGroup),
            typeof(UnityEngine.UI.ContentSizeFitter), typeof(UnityEngine.UI.Image));
        panel = (RectTransform)root.transform;
        panel.SetParent(canvas.transform, false);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(82f, 0f);
        panel.sizeDelta = new Vector2(516f, 0f);
        var layout = root.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.padding = new RectOffset(backdrop ? 30 : 22, 22, backdrop ? 26 : 15, backdrop ? 26 : 15);
        layout.childForceExpandHeight = false;
        root.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        var background = root.GetComponent<UnityEngine.UI.Image>();
        background.color = Color.clear;
        background.raycastTarget = false;
        if (backdrop)
        {
            // Plate with a soft drop shadow, hairline frame and corner ticks.
            // A plain plate in the HUD's own panel colour, lifted off the scene by a soft shadow.
            var plate = LucidUI.Stretch(panel, "Plate", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, LucidUI.Alpha(LucidUI.Panel, 0.92f));
            LucidUI.ShadowFor(plate.rectTransform, 0.7f, 30f, 12f);
        }
        panelGroup = root.GetComponent<CanvasGroup>();
    }

    UnityEngine.UI.Text AddText(string value, int size, int height, Color color, float tracking = 0f, bool display = false)
    {
        var go = new GameObject("Label", typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.LayoutElement));
        go.transform.SetParent(panel, false);
        go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
        var label = go.GetComponent<UnityEngine.UI.Text>();
        // Headings use the title face; everything else the reading face. No letter spacing anywhere.
        label.font = display || size >= 36 ? LucidUI.Display : font;
        label.fontSize = size;
        label.fontStyle = FontStyle.Normal;
        LucidUI.Style(label);
        label.color = color == ivory ? LucidUI.Cream : color;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;
        label.text = size >= 36 ? Nice(value) : value;
        return label;
    }

    void AddButton(string title, Action action, int height = 44, bool primary = false)
    {
        var go = new GameObject(title, typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.LayoutElement));
        go.transform.SetParent(panel, false);
        go.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = new Color(1f, 1f, 1f, 0f);
        var button = go.GetComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        button.transition = UnityEngine.UI.Selectable.Transition.None;
        // Selected: a thread runs in from the screen edge and ends in a bead before the word,
        // like the hanging string of a dreamcatcher (animated by LucidButtonFx).
        var thread = LucidUI.Stretch(go.transform, "Thread", new Vector2(0f, .5f), new Vector2(0f, .5f), new Vector2(-104f, -1f), new Vector2(4f, 1f), accent);
        thread.rectTransform.pivot = new Vector2(0f, .5f);
        thread.rectTransform.anchoredPosition = new Vector2(-104f, 0f);
        var bead = new GameObject("Bead", typeof(RectTransform), typeof(LucidRing), typeof(UnityEngine.UI.LayoutElement));
        bead.GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
        var beadRect = (RectTransform)bead.transform;
        beadRect.SetParent(go.transform, false);
        beadRect.anchorMin = beadRect.anchorMax = new Vector2(0f, .5f);
        beadRect.anchoredPosition = new Vector2(8f, 0f); beadRect.sizeDelta = new Vector2(10f, 10f);
        var beadRing = bead.GetComponent<LucidRing>(); beadRing.thickness = 5f; beadRing.raycastTarget = false;
        var label = AddText(Nice(title), primary ? 30 : 24, height, LucidUI.Cream);
        label.rectTransform.SetParent(go.transform, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(24f, 0f);
        label.rectTransform.offsetMax = new Vector2(-10f, 0f);
        go.AddComponent<LucidButtonFx>().Init(thread, beadRing, label);
        button.onClick.AddListener(() => { LucidAudio.Play(LucidAudio.Cue.Capture); action(); });
        if (events.currentSelectedGameObject == null || !events.currentSelectedGameObject.activeInHierarchy)
            events.SetSelectedGameObject(go);
    }

    void SetFade(float alpha) => fade.color = new Color(0.025f, 0.018f, 0.055f,
        ReduceFlash && mode == Mode.Opening ? Mathf.Min(0.35f, alpha) : alpha);

    void OnDestroy()
    {
        if (rebinding != null) { rebinding.action.Enable(); rebinding.Dispose(); rebinding = null; }
        if (menuShade != null) Destroy(menuShade);
        if (current != this) return;
        current = null;
        Time.timeScale = 1f;
        AudioListener.pause = false;
    }
}
