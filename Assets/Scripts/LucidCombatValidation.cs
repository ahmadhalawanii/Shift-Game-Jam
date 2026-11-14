#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Editor-only integration probe. Explicitly started; never runs in the shipped game.
public class LucidCombatValidation : MonoBehaviour
{
    public static string Result = "Not run";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    System.Text.StringBuilder report = new System.Text.StringBuilder();
    bool oldToggle, oldInvert;
    void Record(string name, bool passed) { report.AppendLine((passed ? "PASS " : "FAIL ") + name); }
    static void Set(object o,string field,object value) { o.GetType().GetField(field,Private).SetValue(o,value); }
    static void Key(Key key) { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(key)); }
    static void KeysUp() { InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState()); }
    static void Mouse(bool fire=false,bool ward=false) { InputSystem.QueueStateEvent(UnityEngine.InputSystem.Mouse.current,new MouseState().WithButton(MouseButton.Left,fire).WithButton(MouseButton.Right,ward)); }
    public static void Run() { Result="Running";new GameObject("Combat validation (editor only)").AddComponent<LucidCombatValidation>(); }
    IEnumerator Start()
    {
        oldToggle=LucidInput.aimToggle;oldInvert=LucidInput.invertY;LucidInput.aimToggle=false;LucidInput.invertY=false;
        Application.runInBackground=true;
        var presentation=FindFirstObjectByType<DreamcatcherPresentation>();
        if(presentation!=null) typeof(DreamcatcherPresentation).GetMethod("Resume",Private).Invoke(presentation,null);
        var pc=FindFirstObjectByType<PlayerController>();var c=pc.GetComponent<PlayerCombat>();var d=c.Dream;
        var avatar=pc.GetComponentInChildren<MemoryAvatar>();var boss=EclipseEncounter.Instance.Boss;var sheet=BalanceSheet.Load();
        pc.Teleport(EclipseEncounter.ArenaCenter+Vector3.back*18f,0);
        yield return new WaitForSeconds(.2f);
        boss.Director.StopAll();EnemyBullet.ClearBoss();GroundWave.ClearAll();BossHazards.ClearAll();
        var beat=typeof(EclipseBoss).GetField("beat",Private);beat.SetValue(boss,System.Enum.Parse(beat.FieldType,"Intro"));Set(boss,"remaining",999f);Set(boss,"seedsLeft",0);
        WorldState.Instance.Set(World.Dream);avatar.health=.5f;
        yield return new WaitForSeconds(.8f);
        Mouse(ward:true);yield return new WaitForSeconds(.04f);
        EnemyBullet.SpawnBoss(pc.transform.position+Vector3.up+Vector3.forward*.5f,Vector3.zero,0,.01f,World.Dream).HeartSeed();
        yield return new WaitForSeconds(.05f);
        Record("Perfect Catch heals 8 HP",Mathf.Abs(avatar.health-.58f)<.005f);
        float health=avatar.health,ward=d.Ward;
        EnemyBullet.SpawnBoss(pc.transform.position+Vector3.up+Vector3.forward*.5f,Vector3.zero,0,.06f,World.Dream);
        yield return new WaitForSeconds(.05f);
        Record("Cyan drains Ward without healing",d.Ward<ward&&Mathf.Abs(avatar.health-health)<.005f);
        for(int i=0;i<5;i++)d.Block(20f);
        Record("Ward breaks at zero",d.Broken&&!d.WardUp);
        Mouse();yield return new WaitForSeconds(2.65f);
        Record("Ward recovers after break",!d.Broken&&d.Ward>0f);
        var pink=EnemyBullet.SpawnBoss(pc.transform.position+Vector3.up+Vector3.right*4,Vector3.zero,0,.01f,World.Dream).HeartSeed();
        var danger=EnemyBullet.SpawnBoss(pc.transform.position+Vector3.up+Vector3.left*4,Vector3.zero,0,.06f,World.Dream);
        Key(UnityEngine.InputSystem.Key.E);yield return new WaitForSeconds(.05f);KeysUp();
        Record("Pulse clears danger and preserves pink",pink.gameObject.activeSelf&&!danger.gameObject.activeSelf);
        Key(UnityEngine.InputSystem.Key.LeftCtrl);yield return new WaitForSeconds(.05f);KeysUp();
        var bloomSeed=EnemyBullet.SpawnBoss(d.BloomPosition+Vector3.right*.3f,Vector3.zero,0,.01f,World.Dream).HeartSeed();
        health=avatar.health;yield return new WaitForSeconds(.5f);
        Record("Bloom draws pink and heals nearby player",!bloomSeed.gameObject.activeSelf&&avatar.health>health);
        EnemyBullet.ClearBoss();
        var cam=Camera.main;var rig=cam.GetComponent<ThirdPersonCamera>();rig.enabled=false;boss.WeakPoints.enabled=false;
        Vector3 origin=pc.transform.position+Vector3.up*1.2f,target=boss.WeakPoints.Position(0);target.y=origin.y;
        cam.transform.position=origin;cam.transform.rotation=Quaternion.LookRotation(target-origin);
        Mouse(fire:true);yield return new WaitForSeconds(.7f);Mouse();yield return new WaitForSeconds(.06f);
        Record("Charged Lumen Bow tags sigil",boss.WeakPoints.Tagged(0));
        WorldState.Instance.Set(World.Nightmare);float power=c.Power;
        typeof(PlayerWeapons).GetMethod("Deal",Private).Invoke(c.Weapons,new object[]{boss,boss.WeakPoints.Position(0),14f,sheet.weapons[0]});
        Record("Nightmare cash-in consumes tag and grants Power",!boss.WeakPoints.Tagged(0)&&c.Power>power);
        boss.WeakPoints.enabled=true;rig.enabled=true;
        c.AddPower(100);Key(UnityEngine.InputSystem.Key.E);yield return new WaitForSeconds(.05f);KeysUp();
        Record("Burst spends 40 Power and staggers",Mathf.Abs(c.Power-60f)<.01f&&boss.Stunned);
        Vector3 before=pc.transform.position;
        Key(UnityEngine.InputSystem.Key.LeftCtrl);yield return new WaitForSeconds(.1f);KeysUp();
        Record("Shadowstep moves and empowers",Vector3.Distance(before,pc.transform.position)>1f&&c.Weapons.empowered>1f);
        c.SetDread(1f);Key(UnityEngine.InputSystem.Key.F);yield return new WaitForSeconds(.05f);KeysUp();
        Record("Overdrive activates and locks phase reuse",c.Weapons.Overdrive&&!pc.GetComponent<NightmareAbilities>().DriveAvailable);
        DpsDummy.Spawn(pc.transform,sheet);
        Set(boss,"<Phase>k__BackingField",3);
        boss.health = 1f;
        boss.TakeDamage(14f, boss.transform.position);
        Record("Regular damage defeats the final phase", EclipseEncounter.Finished && boss.health == 0f && WorldState.CurrentOrDream == World.Dream);
        Result=report.ToString();Debug.Log("LUCID_COMBAT_VALIDATION\n"+Result);Destroy(gameObject);
    }
    void OnDestroy() { KeysUp();Mouse();LucidInput.aimToggle=oldToggle;LucidInput.invertY=oldInvert; }
}
#endif
