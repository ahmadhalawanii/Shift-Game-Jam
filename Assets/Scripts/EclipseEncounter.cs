using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Installed by the existing HUD so saved Level1 scenes need no regeneration.
public class EclipseEncounter : MonoBehaviour
{
    public static EclipseEncounter Instance { get; private set; }
    public static bool Finished => Instance != null && Instance.Result != null;
    public string Result { get; private set; }
    public EclipseBoss Boss { get; private set; }
    public string Notice { get; private set; }
    public static readonly Vector3 ArenaCenter = new Vector3(0, 0, 141f);
    public const float ArenaRadius = 27f;
    public bool BossFightStarted { get; private set; }
    static bool retryBoss;
    float noticeUntil;
    PlayerController player;

    void Awake()
    {
        Instance = this;
        gameObject.AddComponent<LucidAudio>();
    }
    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        var root = new GameObject("The Sleeper");
        root.transform.position = ArenaCenter + Vector3.up * 7f;
        Boss = root.AddComponent<EclipseBoss>();
        gameObject.AddComponent<EclipseArena>();
        if (retryBoss && player != null)
        {
            retryBoss = false;
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = ArenaCenter + Vector3.back * 18f + Vector3.up * 0.15f;
            player.checkpoint = player.transform.position;
            controller.enabled = true;
        }
        Tell("Follow the path to the Sleeper. Left-click to strike. Hold and release right-click to cast.", 8f);
    }
    void Update()
    {
        if (Time.time > noticeUntil) Notice = null;
        if (Finished && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            retryBoss = BossFightStarted;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
    }
    public void Tell(string message, float seconds = 2f) { Notice = message; noticeUntil = Time.time + seconds; }
    public void BeginBossFight()
    {
        if (BossFightStarted) return;
        BossFightStarted = true;
        EnemyBullet.Clear(Vector3.zero, float.PositiveInfinity);
        foreach (var enemy in Enemy.All.ToArray())
            if (enemy is WispEnemy) enemy.gameObject.SetActive(false);
        Tell("THE ECLIPSE — Q to focus the boss. Round orbs: absorb. Red waves: dash.", 5f);
    }
    public void End(bool victory)
    {
        if (Finished) return;
        if (victory) LucidAudio.Play(LucidAudio.Cue.Victory);
        Result = victory ? "THE BOUNDARY IS RESTORED" : "YOUR MEMORY FADED";
        EnemyBullet.Clear(Vector3.zero, float.PositiveInfinity);
        GroundWave.ClearAll();
        foreach (var e in Enemy.All.ToArray()) if (e != null) e.enabled = false;
        if (player != null) player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { retryBoss = false; Instance = null; }
    void OnDestroy()
    {
        if (Instance == this) { Instance = null; EnemyBullet.ReleaseSceneResources(); }
    }
}
