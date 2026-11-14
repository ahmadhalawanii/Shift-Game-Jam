using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

// Persistent uGUI elements; only values and short labels change during combat.
public class LucidCombatHUD : MonoBehaviour
{
    public static Vector3 DamageSource;
    LucidRing damageArc;
    Camera view;
    Canvas canvas;
    RectTransform root;
    PlayerCombat combat;
    DreamCombat dream;
    NightmareAbilities abilities;
    PlayerController pc;
    MemoryAvatar avatar;
    BalanceSheet sheet;
    Font font;
    Image health, chip, power, dread, bossFill, heart;
    Image[] weaponBacks = new Image[3], notches = new Image[2], charges = new Image[2];
    GameObject[] armourDashes = new GameObject[31];
    Text healthLabel, dreadLabel, ammo, weaponTitle, notice, bossName, swapLabel, popup;
    Text[] weaponLabels = new Text[3], abilityLabels = new Text[4];
    LucidRing ward, reload, swap, charge, incoming;
    LucidRing[] cooldowns = new LucidRing[4], tags = new LucidRing[3];
    GameObject bossGroup;
    float chipValue = 1f, lastHealth = 1f, chipAt, labelAt;
    bool wasPad, wasNight;
    int bindingVersion;
    Color cream = new Color(.94f,.88f,1f), ember = new Color(1f,.49f,.31f);
    void Start()
    {
        combat = GetComponent<PlayerCombat>(); dream = combat.Dream; abilities = GetComponent<NightmareAbilities>();
        pc = GetComponent<PlayerController>(); avatar = GetComponentInChildren<MemoryAvatar>(); sheet = BalanceSheet.Load();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        view = Camera.main;
        var go = new GameObject("Lucid Combat HUD", typeof(Canvas), typeof(CanvasScaler)); go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
        root = go.GetComponent<RectTransform>();
        var left = Group("Vital signs", new Vector2(0,0), new Vector2(36,38), new Vector2(300,160));
        Panel(left, new Vector2(0,0), new Vector2(300,155));
        heart = Box(left, new Vector2(13,85), new Vector2(16,16), new Color(1f,.3f,.42f)); heart.rectTransform.localRotation = Quaternion.Euler(0,0,45);
        healthLabel = Label(left,"HEALTH",new Vector2(42,112),new Vector2(240,25),16);
        Box(left,new Vector2(42,93),new Vector2(228,8),new Color(.12f,.07f,.13f));
        chip = Bar(left,new Vector2(42,93),228,8,new Color(1f,.78f,.85f)); health = Bar(left,new Vector2(42,93),228,8,new Color(.9f,.16f,.25f));
        ward = Ring(left,new Vector2(21,93),44,3,Color.cyan);
        Label(left,"POWER",new Vector2(18,57),new Vector2(75,18),11);
        power = Bar(left,new Vector2(95,62),175,4,new Color(.7f,.8f,1f));
        dreadLabel = Label(left,"DREAD",new Vector2(18,26),new Vector2(125,20),11);
        dread = Bar(left,new Vector2(145,32),125,4,ember);
        var right = Group("Weapons",new Vector2(1,0),new Vector2(-36,38),new Vector2(320,160));
        Panel(right,Vector2.zero,new Vector2(320,155));
        weaponTitle = Label(right,"",new Vector2(18,116),new Vector2(270,25),16);
        ammo = Label(right,"",new Vector2(18,84),new Vector2(250,30),24);
        reload = Ring(right,new Vector2(286,100),42,3,cream);
        for(int i=0;i<3;i++)
        {
            weaponBacks[i] = Box(right,new Vector2(16+i*100,14),new Vector2(90,48),new Color(.15f,.12f,.23f,.85f));
            Ring(weaponBacks[i].rectTransform,new Vector2(17,27),16+i*3,2,cream).Fill=.68f;
            weaponLabels[i] = Label(weaponBacks[i].rectTransform,"",new Vector2(32,5),new Vector2(58,36),10);
        }
        var bottom = Group("Abilities",new Vector2(.5f,0),new Vector2(0,38),new Vector2(400,100));
        for(int i=0;i<4;i++)
        {
            cooldowns[i] = Ring(bottom,new Vector2(50+i*100,58),44,3,cream);
            Ring(bottom,new Vector2(50+i*100,58),29,1,new Color(.65f,.75f,1f,.35f));
            abilityLabels[i] = Label(bottom,"",new Vector2(i*100,0),new Vector2(100,37),11); abilityLabels[i].alignment=TextAnchor.MiddleCenter;
        }
        for(int i=0;i<2;i++) charges[i] = Box(bottom,new Vector2(40+i*13,54),new Vector2(7,7),cream);
        bossGroup = Group("Encounter",new Vector2(.5f,1),new Vector2(0,-25),new Vector2(620,118)).gameObject;
        var top = (RectTransform)bossGroup.transform;
        swap = Ring(top,new Vector2(310,94),34,3,cream);
        swapLabel = Label(top,"",new Vector2(335,82),new Vector2(220,24),12);
        bossName = Label(top,"",new Vector2(0,45),new Vector2(620,25),16); bossName.alignment=TextAnchor.MiddleCenter;
        Box(top,new Vector2(0,31),new Vector2(620,7),new Color(.1f,.08f,.15f,.9f)); bossFill = Bar(top,new Vector2(0,31),620,7,cream);
        notches[0]=Box(top,new Vector2(620*sheet.phase2At,27),new Vector2(2,15),Color.white);
        notches[1]=Box(top,new Vector2(620*sheet.phase3At,27),new Vector2(2,15),Color.white);
        for(int i=0;i<3;i++) tags[i]=Ring(top,new Vector2(290+i*20,12),10,2,cream);
        // Masked dashed overlay makes the armoured Dream bar distinct without obscuring its fill.
        for(int i=0;i<31;i++) armourDashes[i] = Box(top,new Vector2(i*20+12,31),new Vector2(4,7),new Color(.1f,.08f,.15f,.8f)).gameObject;
        var center=Group("Aim feedback",new Vector2(.5f,.5f),Vector2.zero,new Vector2(100,100));
        charge=Ring(center,new Vector2(50,50),68,3,cream); incoming=Ring(center,new Vector2(50,50),82,2,DreamCombat.Pink);
        damageArc=Ring(center,new Vector2(50,50),650,12,new Color(1f,.12f,.2f,0f)); damageArc.Fill=.17f;
        popup=Label(center,"",new Vector2(-110,-35),new Vector2(320,30),18); popup.alignment=TextAnchor.MiddleCenter;
        var notices=Group("Notice",new Vector2(.5f,1),new Vector2(0,-158),new Vector2(1000,35));
        notice=Label(notices,"",Vector2.zero,new Vector2(1000,35),17); notice.alignment=TextAnchor.MiddleCenter;
        UpdateLabels();
    }
    RectTransform Group(string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(root,false);
        r.anchorMin=r.anchorMax=anchor; r.pivot=anchor; r.anchoredPosition=position; r.sizeDelta=size; return r;
    }
    void Place(RectTransform r,RectTransform parent,Vector2 position,Vector2 size)
    { r.SetParent(parent,false); r.anchorMin=r.anchorMax=r.pivot=Vector2.zero; r.anchoredPosition=position; r.sizeDelta=size; }
    Image Box(RectTransform parent,Vector2 position,Vector2 size,Color color)
    { var x=new GameObject("Line",typeof(Image)).GetComponent<Image>(); Place(x.rectTransform,parent,position,size); x.color=color; x.raycastTarget=false; return x; }
    Image Bar(RectTransform parent,Vector2 position,float width,float height,Color color) => Box(parent,position,new Vector2(width,height),color);
    void Panel(RectTransform p,Vector2 pos,Vector2 size)
    { Box(p,pos,size,new Color(.035f,.035f,.07f,.72f)); Box(p,pos+new Vector2(0,size.y),new Vector2(size.x,1),new Color(.65f,.72f,1f,.35f)); }
    Text Label(RectTransform parent,string text,Vector2 pos,Vector2 size,int fontSize)
    {
        var t=new GameObject("Label",typeof(Text),typeof(Outline)).GetComponent<Text>(); Place(t.rectTransform,parent,pos,size);
        t.font=font;t.fontSize=fontSize;t.text=text;t.color=cream;t.raycastTarget=false;
        t.GetComponent<Outline>().effectColor=new Color(0,0,0,.65f); return t;
    }
    LucidRing Ring(RectTransform parent,Vector2 center,float size,float width,Color tint)
    { var r=new GameObject("Ring",typeof(LucidRing)).GetComponent<LucidRing>(); Place(r.rectTransform,parent,center-Vector2.one*size/2,Vector2.one*size);r.thickness=width;r.color=tint;r.raycastTarget=false;return r; }
    void Fill(Image image,float width,float amount) { image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width*Mathf.Clamp01(amount)); }
    void UpdateLabels()
    {
        bool night=WorldState.CurrentOrDream==World.Nightmare; wasNight=night;wasPad=LucidInput.UsingGamepad;
        bindingVersion = LucidInput.BindingVersion;
        InputAction[] actions={LucidInput.Dash,LucidInput.Ability1,LucidInput.Ability2,night?LucidInput.Overdrive:LucidInput.Aim};
        string[] names=night?new[]{"DASH","BURST","SHADOWSTEP","OVERDRIVE"}:new[]{"DASH","PULSE","BLOOM","WARD"};
        for(int i=0;i<4;i++) abilityLabels[i].text=LucidInput.Label(actions[i])+"\n"+names[i];
        InputAction[] selects={LucidInput.Weapon1,LucidInput.Weapon2,LucidInput.Weapon3};
        string[] guns={"THORN","GLOAM","WRAITH"};
        for(int i=0;i<3;i++) weaponLabels[i].text=(wasPad?LucidInput.Label(LucidInput.NextWeapon):LucidInput.Label(selects[i]))+"\n"+guns[i];
    }
    void Update()
    {
        if(canvas==null)return;
        canvas.enabled=!DreamcatcherPresentation.CoversHud && !EclipseEncounter.Finished;
        if(!canvas.enabled)return;
        float hurt = Mathf.Clamp01(1f - (Time.time - pc.LastHitTime) / .6f);
        damageArc.color=new Color(1f,.12f,.2f,hurt*.28f);
        if(hurt>0f && view!=null)
        {
            Vector3 to=DamageSource-transform.position;to.y=0;
            float angle=Vector3.SignedAngle(view.transform.forward,to,Vector3.up);
            damageArc.rectTransform.localRotation=Quaternion.Euler(0,0,-angle+30f);
        }
        bool night=WorldState.CurrentOrDream==World.Nightmare;
        if(night!=wasNight || wasPad!=LucidInput.UsingGamepad || bindingVersion != LucidInput.BindingVersion) UpdateLabels();
        var w=combat.Weapons; var boss=EclipseEncounter.Instance!=null?EclipseEncounter.Instance.Boss:null;
        Color tint=Color.Lerp(cream,ember,WorldState.MixOrZero);
        float h=avatar!=null?avatar.health:1f;
        if(h<lastHealth){chipAt=Time.unscaledTime+.4f;chipValue=Mathf.Max(chipValue,lastHealth);} lastHealth=h;
        if(h>chipValue)chipValue=h;else if(Time.unscaledTime>chipAt)chipValue=Mathf.MoveTowards(chipValue,h,Time.unscaledDeltaTime*.5f);
        Fill(health,228,h);Fill(chip,228,chipValue);Fill(power,175,combat.Power/sheet.maxPower);Fill(dread,125,combat.Dread);
        heart.rectTransform.localScale=Vector3.one*(1f+.12f*Mathf.Sin(Time.unscaledTime*(h<.3f?15f:6f)));
        ward.Fill=dream.Ward/sheet.wardMax;ward.color=dream.Broken?Color.Lerp(Color.red,Color.white,Mathf.PingPong(Time.time*6,1)):new Color(.6f,.9f,1f);
        cooldowns[0].Fill=Mathf.Clamp01(pc.DashCharges/2f);
        cooldowns[1].Fill=night?abilities.BurstReady01:dream.PulseReady01;
        cooldowns[2].Fill=night?abilities.StepReady01:dream.BloomReady01;
        cooldowns[3].Fill=night?(abilities.DriveAvailable?1f:Mathf.Min(combat.Dread,abilities.DriveReady01)):dream.Ward/sheet.wardMax;
        for(int i=0;i<4;i++)cooldowns[i].color=tint*(cooldowns[i].Fill>=.999f?1f:.5f);
        for(int i=0;i<2;i++)charges[i].color=pc.DashCharges>=i+1?tint:new Color(.15f,.15f,.2f);
        for(int i=0;i<3;i++){weaponBacks[i].color=i==w.Index&&night?new Color(tint.r*.3f,tint.g*.3f,tint.b*.3f,.9f):new Color(.08f,.07f,.13f,.7f);weaponBacks[i].rectTransform.localScale=Vector3.one*(i==w.Index&&night?1.07f:1f);}
        reload.Fill=night?(w.Reloading?w.Reload01:w.Current.kind==WeaponKind.Chain?w.Heat01:0f):dream.Draw01;
        reload.color=tint;charge.Fill=dream.Draw01;
        incoming.Fill=EnemyBullet.PinkNearby(transform.position,8f)&&!night?1f:0f;
        popup.text=Time.unscaledTime-dream.PerfectAt<.8f?"PERFECT CATCH":"";
        bossGroup.SetActive(boss!=null&&boss.Engaged);
        if(boss!=null&&boss.Engaged)
        {
            Fill(bossFill,620,boss.health/boss.maxHealth);bossFill.color=tint;swap.Fill=boss.Swap01;swap.color=boss.SwapWarning?Color.Lerp(tint,Color.white,Mathf.PingPong(Time.time*5f,1)):tint;
            for(int i=0;i<3;i++)tags[i].color=boss.WeakPoints!=null&&boss.WeakPoints.Tagged(i)?DreamCombat.Pink:new Color(.3f,.3f,.4f);
            for(int i=0;i<armourDashes.Length;i++) if(armourDashes[i].activeSelf==night) armourDashes[i].SetActive(!night);
        }
        if(Time.unscaledTime<labelAt)return;labelAt=Time.unscaledTime+.1f;
        healthLabel.text="HEALTH  "+Mathf.RoundToInt(h*100);dreadLabel.text="DREAD  ×"+combat.DreadMultiplier.ToString("F2");
        weaponTitle.text=night?w.Current.name.ToUpper():"LUMEN BOW  /  "+(boss!=null&&boss.WeakPoints!=null?boss.WeakPoints.TagCount:0)+" TAGS";
        ammo.text=!night?"DRAW • TAG • SHIFT":w.Current.kind==WeaponKind.Chain?(w.Overheated?"COOLING":"HEAT "+Mathf.RoundToInt(w.Heat01*100)+"%"):w.Mag(w.Index)+" / ∞"+(w.Reloading?"  RELOADING":"");
        ammo.color=night&&w.Current.magazine>0&&w.Mag(w.Index)==0?Color.Lerp(ember,Color.white,Mathf.PingPong(Time.time*3,1)):tint;
        notice.text=EclipseEncounter.Instance!=null?EclipseEncounter.Instance.Notice:"";
        if(boss!=null){bossName.text="THE SLEEPER / PHASE "+boss.Phase+" / "+(night?"EXPOSED":"ARMOURED");swapLabel.text=LucidInput.Label(LucidInput.SwapStance)+"  "+boss.SwapSeconds.ToString("F1")+"s";}
    }
}
