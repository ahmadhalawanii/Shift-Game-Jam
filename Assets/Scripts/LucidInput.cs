using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// The one place gameplay reads input from. Keyboard, mouse and gamepad share the same actions.
// To change a binding, edit the table in Build(). Debug keys (F-keys, G, B and the boss keys) are not here.
public static class LucidInput
{
    public const string Kbm = "Keyboard&Mouse", Pad = "Gamepad";
    public const float Buffer = 0.12f;          // a press this old still counts (dash, reload, abilities)

    // Options (the controls screen will edit these).
    public static float sensitivity = 0.12f;    // degrees per mouse count
    public static float stickLookSpeed = 170f;  // degrees per second at full stick
    public static bool invertY;
    public static bool aimToggle;               // aim and Ward: toggle instead of hold

    public static bool UsingGamepad { get; private set; }

    public static InputAction Move, Look, Fire, Aim, Dash, Jump, SwapStance, Reload, Ability1, Ability2, Overdrive,
        Weapon1, Weapon2, Weapon3, NextWeapon, PrevWeapon, WeaponWheel, SwapShoulder, Recenter, Pause;

    static InputActionMap map;
    public static InputActionMap Actions => map;
    public static int BindingVersion { get; private set; }
    static readonly Dictionary<InputAction, float> pressedAt = new Dictionary<InputAction, float>();
    static bool aimLatch;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Build()
    {
        if (map != null) map.Dispose();
        pressedAt.Clear();
        aimLatch = false;
        invertY = PlayerPrefs.GetInt("Lucid.InvertY") != 0;
        aimToggle = PlayerPrefs.GetInt("Lucid.AimToggle") != 0;
        UsingGamepad = false;
        map = new InputActionMap("Lucid");

        Move = map.AddAction("Move", InputActionType.Value);
        Move.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w", Kbm).With("Down", "<Keyboard>/s", Kbm)
            .With("Left", "<Keyboard>/a", Kbm).With("Right", "<Keyboard>/d", Kbm);
        Move.AddBinding("<Gamepad>/leftStick", groups: Pad);
        Look = map.AddAction("Look", InputActionType.Value);
        Look.AddBinding("<Mouse>/delta", groups: Kbm);
        Look.AddBinding("<Gamepad>/rightStick", groups: Pad);

        Fire = Button("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");
        Aim = Button("Aim / Ward", "<Mouse>/rightButton", "<Gamepad>/leftTrigger");
        Dash = Button("Dash", "<Keyboard>/space", "<Gamepad>/buttonSouth");
        // Jump is not in the combat table (Space is the dash). It is needed for the traversal zones.
        Jump = Button("Jump", "<Keyboard>/leftCtrl", "<Gamepad>/dpad/down");
        SwapStance = Button("Swap stance", "<Keyboard>/leftShift", "<Gamepad>/leftShoulder");
        Reload = Button("Reload", "<Keyboard>/r", "<Gamepad>/buttonWest");
        Ability1 = Button("Ability 1", "<Keyboard>/e", "<Gamepad>/buttonEast");
        Ability2 = Button("Ability 2", "<Keyboard>/q", "<Gamepad>/buttonNorth");
        Overdrive = Button("Overdrive", "<Keyboard>/f", "<Gamepad>/dpad/up");
        Weapon1 = Button("Weapon 1", "<Keyboard>/1", null);
        Weapon2 = Button("Weapon 2", "<Keyboard>/2", null);
        Weapon3 = Button("Weapon 3", "<Keyboard>/3", null);
        NextWeapon = Button("Next weapon", "<Mouse>/backButton", "<Gamepad>/rightShoulder");
        NextWeapon.AddBinding("<Gamepad>/dpad/right", groups: Pad);
        PrevWeapon = Button("Previous weapon", null, "<Gamepad>/dpad/left");
        WeaponWheel = map.AddAction("Weapon wheel", InputActionType.Value, "<Mouse>/scroll/y", groups: Kbm);
        SwapShoulder = Button("Swap shoulder", "<Keyboard>/c", "<Gamepad>/rightStickPress");
        Recenter = Button("Recenter", null, "<Gamepad>/leftStickPress");
        Pause = Button("Pause", "<Keyboard>/escape", "<Gamepad>/start");

        map.actionTriggered += OnTriggered;
        string overrides = PlayerPrefs.GetString("Lucid.BindingOverrides", "");
        if (!string.IsNullOrEmpty(overrides)) map.LoadBindingOverridesFromJson(overrides);
        map.Enable();
    }
    public static void SaveBindings() { BindingVersion++; PlayerPrefs.SetString("Lucid.BindingOverrides", map.SaveBindingOverridesAsJson()); PlayerPrefs.Save(); }

    static InputAction Button(string name, string keyboard, string gamepad)
    {
        var a = map.AddAction(name, InputActionType.Button);
        if (keyboard != null) a.AddBinding(keyboard, groups: Kbm);
        if (gamepad != null) a.AddBinding(gamepad, groups: Pad);
        return a;
    }

    static void OnTriggered(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        UsingGamepad = ctx.control != null && ctx.control.device is Gamepad;
        if (ctx.action.type != InputActionType.Button) return;
        pressedAt[ctx.action] = Time.unscaledTime;
        if (ctx.action == Aim && aimToggle) aimLatch = !aimLatch;
    }

    // True if the button went down within the buffer window and nothing has used the press yet.
    public static bool Pressed(InputAction a, float buffer = Buffer)
    {
        float t;
        return pressedAt.TryGetValue(a, out t) && Time.unscaledTime - t <= buffer;
    }

    public static void Consume(InputAction a) { pressedAt[a] = -99f; }

    public static bool AimHeld { get { return aimToggle ? aimLatch : Aim.IsPressed(); } }

    public static Vector2 MoveVector { get { return Vector2.ClampMagnitude(Move.ReadValue<Vector2>(), 1f); } }

    // Look input for this frame, in degrees.
    public static Vector2 LookDelta
    {
        get
        {
            Vector2 d = Look.ReadValue<Vector2>();
            var control = Look.activeControl;
            d = control != null && control.device is Gamepad ? d * stickLookSpeed * Time.unscaledDeltaTime : d * sensitivity;
            if (invertY) d.y = -d.y;
            return d;
        }
    }

    // The binding as text for the device in use ("Space", "RMB", "A"), for HUD hints.
    public static string Label(InputAction a)
    {
        return a.GetBindingDisplayString(InputBinding.MaskByGroup(UsingGamepad ? Pad : Kbm));
    }
}
