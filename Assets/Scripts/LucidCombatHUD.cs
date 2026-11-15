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
    Image health, chip, power, dread, bossFill, wardBar;
    Image[] weaponBacks = new Image[3], notches = new Image[2], charges = new Image[2];
    GameObject[] armourDashes = new GameObject[31];
    Text healthLabel, healthValue, dreadLabel, ammo, weaponTitle, notice, bossName, swapLabel, popup;
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
        font = LucidUI.Font;
        view = Camera.main;
        var go = new GameObject("Lucid Combat HUD", typeof(Canvas), typeof(CanvasScaler)); go.transform.SetParent(transform, false);
        canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 15;
        var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
        root = go.GetComponent<RectTransform>();
        var left = Group("Vital signs", new Vector2(0,0), new Vector2(36,38), new Vector2(300,160));
        Panel(left, new Vector2(0,0), new Vector2(300,160));
        // Health first and biggest: the word, the number, and a thick bar with quarter marks.
        healthLabel = Label(left,"Health",new Vector2(18,122),new Vector2(150,30),20);
        healthValue = Label(left,"100",new Vector2(150,118),new Vector2(134,40),34); healthValue.alignment=TextAnchor.MiddleRight; healthValue.fontStyle=FontStyle.Bold;
        Box(left,new Vector2(16,86),new Vector2(268,26),new Color(.01f,.005f,.02f,.95f));
        Box(left,new Vector2(18,88),new Vector2(264,22),new Color(.16f,.08f,.12f));
        chip = Bar(left,new Vector2(18,88),264,22,LucidUI.Chip); health = Bar(left,new Vector2(18,88),264,22,LucidUI.HealthRed);
        for(int q=1;q<4;q++) Box(left,new Vector2(18+264*q/4f-1,88),new Vector2(2,22),new Color(0,0,0,.35f));
        ward = Ring(left,new Vector2(-200,-200),2,1,Color.clear); ward.enabled=false;
        Label(left,"Ward",new Vector2(18,54),new Vector2(70,22),15);
        Box(left,new Vector2(80,60),new Vector2(204,7),new Color(.1f,.08f,.16f)); wardBar = Bar(left,new Vector2(80,60),204,7,LucidUI.Dream);
        Label(left,"Power",new Vector2(18,30),new Vector2(70,22),15);
        Box(left,new Vector2(80,36),new Vector2(204,7),new Color(.1f,.08f,.16f)); power = Bar(left,new Vector2(80,36),204,7,cream);
        dreadLabel = Label(left,"Dread",new Vector2(18,6),new Vector2(130,22),15);
        Box(left,new Vector2(150,12),new Vector2(134,7),new Color(.1f,.08f,.16f)); dread = Bar(left,new Vector2(150,12),134,7,ember);
        var right = Group("Weapons",new Vector2(1,0),new Vector2(-36,38),new Vector2(320,160));
        Panel(right,Vector2.zero,new Vector2(320,155));
        weaponTitle = Label(right,"",new Vector2(18,116),new Vector2(270,25),16);
        ammo = Label(right,"",new Vector2(18,84),new Vector2(250,30),24);
        reload = Ring(right,new Vector2(286,100),42,3,cream);
        for(int i=0;i<3;i++)
        {
            weaponBacks[i] = Box(right,new Vector2(16+i*100,14),new Vector2(90,48),new Color(.15f,.12f,.23f,.85f));
            LucidUI.ShadowFor(weaponBacks[i].rectTransform,.5f,12f,5f);
            Ring(weaponBacks[i].rectTransform,new Vector2(17,27),16+i*3,2,cream).Fill=.68f;
            weaponLabels[i] = Label(weaponBacks[i].rectTransform,"",new Vector2(32,5),new Vector2(58,36),10);
        }
        var bottom = Group("Abilities",new Vector2(.5f,0),new Vector2(0,38),new Vector2(400,100));
        for(int i=0;i<4;i++)
        {
            // Dark disc with a soft drop shadow so each ability reads as a widget over any background.
            var disc = Ring(bottom,new Vector2(50+i*100,58),50,25,new Color(.02f,.03f,.06f,.72f));
            LucidUI.ShadowFor(disc.rectTransform,.55f,16f,6f);
            cooldowns[i] = Ring(bottom,new Vector2(50+i*100,58),44,3,cream);
            Ring(bottom,new Vector2(50+i*100,58),29,1,new Color(.65f,.75f,1f,.35f));
            abilityLabels[i] = Label(bottom,"",new Vector2(i*100,0),new Vector2(100,37),11); abilityLabels[i].alignment=TextAnchor.MiddleCenter;
        }
        for(int i=0;i<2;i++) charges[i] = Box(bottom,new Vector2(40+i*13,54),new Vector2(7,7),cream);
        bossGroup = Group("Encounter",new Vector2(.5f,1),new Vector2(0,-25),new Vector2(620,118)).gameObject;
        var top = (RectTransform)bossGroup.transform;
        var bossPlate = Box(top,new Vector2(-24,4),new Vector2(668,70),new Color(.02f,.03f,.06f,.55f));
        LucidUI.ShadowFor(bossPlate.rectTransform,.45f,26f,8f);
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
    {
        var plate=Box(p,pos,size,new Color(.025f,.03f,.06f,.74f));
        LucidUI.ShadowFor(plate.rectTransform,.6f,26f,9f);
    }
    Text Label(RectTransform parent,string text,Vector2 pos,Vector2 size,int fontSize)
    {
        var t=new GameObject("Label",typeof(Text)).GetComponent<Text>(); Place(t.rectTransform,parent,pos,size);
        t.font=font;t.fontSize=fontSize;t.text=text;t.color=cream;t.raycastTarget=false;
        LucidUI.Style(t); return t;
    }
    LucidRing Ring(RectTransform parent,Vector2 center,float size,float width,Color tint)
    { var r=new GameObject("Ring",typeof(LucidRing)).GetComponent<LucidRing>(); Place(r.rectTransform,parent,center-Vector2.one*size/2,Vector2.one*size);r.thickness=width;r.color=tint;r.raycastTarget=false;return r; }
    void Fill(Image image,float width,float amount) { image.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width*Mathf.Clamp01(amount)); }
    void UpdateLabels()
    {
        bool night=WorldState.CurrentOrDream==World.Nightmare; wasNight=night;wasPad=LucidInput.UsingGamepad;
        bindingVersion = LucidInput.BindingVersion;
        InputAction[] actions={LucidInput.Dash,LucidInput.Ability1,LucidInput.Ability2,night?LucidInput.Overdrive:LucidInput.Aim};
        string[] names=night?new[]{"Dash","Burst","Shadowstep","Overdrive"}:new[]{"Dash","Pulse","Bloom","Ward"};
        for(int i=0;i<4;i++) abilityLabels[i].text=LucidInput.Label(actions[i])+"\n"+names[i];
        InputAction[] selects={LucidInput.Weapon1,LucidInput.Weapon2,LucidInput.Weapon3};
        string[] guns={"Thorn","Gloam","Wraith"};
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
        Fill(health,264,h);Fill(chip,264,chipValue);Fill(power,204,combat.Power/sheet.maxPower);Fill(dread,134,combat.Dread);
        // Animated health bar: it swells, flashes white and rattles on a hit, and beats faster the lower it gets.
        float punch=Mathf.Clamp01((chipAt-Time.unscaledTime)/.4f);
        float beat=h<.4f?Mathf.Pow(Mathf.Abs(Mathf.Sin(Time.unscaledTime*Mathf.Lerp(9f,4f,h/.4f))),6f):0f;
        health.rectTransform.localScale=new Vector3(1f,1f+punch*1.6f+beat*.5f,1f);chip.rectTransform.localScale=health.rectTransform.localScale;
        health.color=Color.Lerp(Color.Lerp(new Color(.9f,.16f,.25f),new Color(1f,.45f,.3f),beat),Color.white,punch);
        healthLabel.rectTransform.localScale=Vector3.one*(1f+punch*.35f+beat*.08f);
        healthLabel.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(Time.unscaledTime*70f)*6f*punch);
        Fill(wardBar,204,dream.Ward/sheet.wardMax);
        wardBar.color=dream.Broken?Color.Lerp(LucidUI.HealthRed,Color.white,Mathf.PingPong(Time.time*6,1)):LucidUI.Dream;
        // Low health: the number turns red and pulses so it is noticed without looking away from the fight.
        healthValue.color=h<.3f?Color.Lerp(LucidUI.HealthRed,LucidUI.Chip,Mathf.PingPong(Time.unscaledTime*3f,1f)):cream;
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
        healthValue.text=Mathf.RoundToInt(h*100).ToString();dreadLabel.text="Dread  x"+combat.DreadMultiplier.ToString("F2");
        int tagCount=boss!=null&&boss.WeakPoints!=null?boss.WeakPoints.TagCount:0;
        weaponTitle.text=night?w.Current.name:"Lumen Bow, "+tagCount+(tagCount==1?" mark":" marks");
        ammo.text=!night?"Hold to draw":w.Current.kind==WeaponKind.Chain?(w.Overheated?"Cooling down":"Heat "+Mathf.RoundToInt(w.Heat01*100)+"%"):w.Reloading?"Reloading":w.Mag(w.Index)+" left";
        ammo.color=night&&w.Current.magazine>0&&w.Mag(w.Index)==0?Color.Lerp(ember,Color.white,Mathf.PingPong(Time.time*3,1)):tint;
        notice.text=EclipseEncounter.Instance!=null?EclipseEncounter.Instance.Notice:"";
        if(boss!=null){bossName.text="The Sleeper, phase "+boss.Phase+(night?"":" (armoured)");swapLabel.text=LucidInput.Label(LucidInput.SwapStance)+"  "+boss.SwapSeconds.ToString("F1")+"s";}
    }
}
