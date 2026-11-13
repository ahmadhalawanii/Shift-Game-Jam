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
    float noticeUntil;
    PlayerController player;

    void Awake() { Instance = this; }
    void Start()
    {
        player = FindFirstObjectByType<PlayerController>();
        var root = new GameObject("Eclipse Boss");
        root.transform.position = new Vector3(0, 5f, 142f);
        Boss = root.AddComponent<EclipseBoss>();
        Tell("Follow the path to the Eclipse. Absorb in Dream; spend Power with E in Nightmare.", 8f);
    }
    void Update()
    {
        if (Time.time > noticeUntil) Notice = null;
        if (Finished && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }
    }
    public void Tell(string message, float seconds = 2f) { Notice = message; noticeUntil = Time.time + seconds; }
    public void End(bool victory)
    {
        if (Finished) return;
        Result = victory ? "THE ECLIPSE HAS FALLEN" : "YOUR MEMORY FADED";
        EnemyBullet.Clear(Vector3.zero, float.PositiveInfinity);
        foreach (var e in Enemy.All.ToArray()) if (e != null) e.enabled = false;
        if (player != null) player.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    void OnDestroy() { if (Instance == this) Instance = null; }
}
