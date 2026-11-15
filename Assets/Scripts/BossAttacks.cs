using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AttackLane { Ground, Air, Sky, Homing }
public enum AttackMode { Dream, Nightmare, Both }

// One self-contained boss attack: Telegraph, then Execute, then Cleanup. Damage is out of 100 player health.
// Numbers come from BossAttackData (row with the same id), falling back to the defaults passed to the constructor.
public abstract class BossAttack
{
    public readonly string Id, Name;
    public readonly AttackLane Lane;
    public readonly AttackMode Mode;
    public readonly AttackTuning Defaults;
    public bool Running { get; private set; }
    public int Cost => T.cost;
    public bool Enabled => T.enabled;
    protected AttackTuning T;
    protected EclipseBoss boss;
    readonly string hookTelegraph, hookFire, hookImpact;
    protected readonly List<LineRenderer> shapes = new List<LineRenderer>();

    protected BossAttack(string id, string name, AttackLane lane, AttackMode mode,
        float damage, float speed, int count, float telegraph, float duration, int cost)
    {
        Id = id; Name = name; Lane = lane; Mode = mode;
        Defaults = new AttackTuning { id = id, damage = damage, speed = speed, count = count, telegraph = telegraph, duration = duration, cost = cost };
        T = Defaults;
        hookTelegraph = "atk_" + id + "_telegraph";
        hookFire = "atk_" + id + "_fire";
        hookImpact = "atk_" + id + "_impact";
    }

    public void Bind(BossAttackData data) { T = data.Find(Id) ?? Defaults; }
    public bool Fits(World w) => Mode == AttackMode.Both || (Mode == AttackMode.Dream) == (w == World.Dream);

    public IEnumerator Run(EclipseBoss owner, float delay)
    {
        boss = owner;
        Running = true;
        if (delay > 0f) yield return new WaitForSeconds(delay);
        float tele = Tele(T.telegraph);
        boss.BeginTell(Name, tele);
        LucidAudio.Hook(hookTelegraph);
        yield return Telegraph(tele);
        LucidAudio.Hook(hookFire);
        yield return Execute();
        Abort();
    }

    public void Abort()
    {
        if (!Running) return;
        Running = false;
        Cleanup();
    }

    protected virtual IEnumerator Telegraph(float seconds) { yield return new WaitForSeconds(seconds); }
    protected abstract IEnumerator Execute();
    protected virtual void Cleanup()
    {
        for (int i = 0; i < shapes.Count; i++) BossHazards.Release(shapes[i]);
        shapes.Clear();
    }

    protected void Impact() { LucidAudio.Hook(hookImpact); }
    protected LineRenderer Keep(LineRenderer line) { shapes.Add(line); return line; }
    protected float Tele(float seconds, bool small = false) => boss.Tele(seconds, small);
    protected float Speed => T.speed * boss.Difficulty;
    protected int Count => Mathf.Max(1, Mathf.RoundToInt(T.count * boss.Difficulty));
    protected World ModeWorld => Mode == AttackMode.Both ? WorldState.CurrentOrDream : Mode == AttackMode.Dream ? World.Dream : World.Nightmare;
    protected bool DreamLook => ModeWorld == World.Dream;
    protected Vector3 Player => boss.PlayerPosition;
    protected Vector3 Emitter => boss.Emitter;
    protected float AimYaw(Vector3 from) { Vector3 d = Player - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
    protected static Vector3 Dir(float yaw) => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    protected EnemyBullet Orb(Vector3 origin, float yaw, float speed, World world, Color color, float thorn, float scale) =>
        boss.Orb(origin, Dir(yaw), speed, T.damage, world, color, thorn, scale);

    protected static IEnumerator Ramp(float seconds, System.Action<float> step)
    {
        for (float t = 0f; t < seconds; t += Time.deltaTime) { step(t / seconds); yield return null; }
        step(1f);
    }

    // Historical colour names remain for attack layouts; dangerous Dream telegraphs are gold/cyan.
    public static readonly Color Coral = new Color(1f, .72f, .18f), Cream = new Color(1f, .84f, .3f),
        Cyan = new Color(0.3f, 0.9f, 1f), Gold = new Color(1f, 0.78f, 0.28f), Pink = new Color(.3f, .9f, 1f),
        Ember = new Color(1f, 0.33f, 0.05f), Amber = new Color(1f, 0.62f, 0.1f), Violet = new Color(0.66f, 0.25f, 1f),
        Blood = new Color(1f, 0.08f, 0.1f), WhiteHot = new Color(1f, 0.95f, 0.85f), Soot = new Color(0.35f, 0.08f, 0.12f);

    public static List<BossAttack> Catalog() => new List<BossAttack>
    {
        new LullabySpiral(), new HexGarden(), new Starfall(), new TetherSeeds(), new TendrilSlam(), new BloomRing(),
        new SeedHalo(), new TwinHelix(),
        new NightTerrorFan(), new PrismBeams(), new ShatterGrid(), new HunterOrbs(), new EclipseMeteors(),
        new WhipTendrils(), new ThornBurst(), new TrackingBeams(),
        new MandalaBloom(), new PetalBlade(), new Sleepwalk(),
        new TalonDive(), new FeatherGale(), new MoonlitMolt(),
    };
}

// ---------------- Dream ----------------

sealed class LullabySpiral : BossAttack
{
    public LullabySpiral() : base("lullaby_spiral", "LULLABY SPIRAL", AttackLane.Air, AttackMode.Dream, 7.5f, 5.5f, 4, 0.7f, 2.4f, 1) { }
    protected override IEnumerator Execute()
    {
        int arms = Mathf.Clamp(Count, 3, 5);
        float start = AimYaw(Emitter), spin = Random.value < 0.5f ? 40f : -40f;
        for (float t = 0f; t < T.duration; t += 0.14f)
        {
            for (int a = 0; a < arms; a++)
            {
                var b = Orb(Emitter, start + a * 360f / arms + t * spin, Speed, World.Dream, a % 2 == 0 ? Coral : Cream, 0f, 1.1f);
                if (b != null) b.Curve(spin > 0f ? 25f : -25f).Trail(0.4f, 0.6f);
            }
            yield return new WaitForSeconds(0.14f);
        }
    }
}

sealed class HexGarden : BossAttack
{
    const float Size = 2.3f;
    readonly List<Vector3> centers = new List<Vector3>();
    readonly List<int> waves = new List<int>();
    public HexGarden() : base("hex_garden", "HEX GARDEN", AttackLane.Ground, AttackMode.Dream, 14f, 0f, 3, 0.9f, 0.35f, 2) { }

    protected override IEnumerator Telegraph(float seconds)
    {
        centers.Clear(); waves.Clear();
        Vector3 origin = boss.ClampToArena(Player, 0f);
        int offset = Random.Range(0, 3);
        for (int q = -2; q <= 2; q++)
            for (int r = -2; r <= 2; r++)
            {
                if (Mathf.Abs(q + r) > 2) continue;
                // Pointy hex axial layout.
                Vector3 c = origin + new Vector3(Size * Mathf.Sqrt(3f) * (q + r * 0.5f), 0f, Size * 1.5f * r);
                if (!boss.InsideArena(c, 1f)) continue;
                centers.Add(c);
                waves.Add(((q - r) % 3 + 3 + offset) % 3);
                Keep(BossHazards.Polygon(c, Size * 0.96f, 6, 0f, 0.06f, new Color(Cream.r, Cream.g, Cream.b, 0.25f)));
            }
        yield return Ramp(Mathf.Min(0.35f, seconds), k => { for (int i = 0; i < shapes.Count; i++) BossHazards.Tint(shapes[i], Color.Lerp(Color.clear, new Color(1f, 0.92f, 0.78f, 0.3f), k), 0.06f); });
    }

    protected override IEnumerator Execute()
    {
        int rounds = Mathf.Clamp(Count, 1, 3);
        for (int w = 0; w < rounds; w++)
        {
            float tele = Tele(T.telegraph);
            yield return Ramp(tele, k =>
            {
                for (int i = 0; i < centers.Count; i++)
                    if (waves[i] == w) BossHazards.Tint(shapes[i], Color.Lerp(Pink, Gold, k) * (0.6f + k), 0.08f + 0.22f * k);
            });
            Impact();
            for (int i = 0; i < centers.Count; i++)
            {
                if (waves[i] != w) continue;
                Keep(BossHazards.Line(centers[i], centers[i] + Vector3.up * 7f, 1.6f, new Color(1f, 0.85f, 0.6f, 0.9f), false));
                Fx.Burst(centers[i] + Vector3.up, Gold, 10);
                BossHazards.HitCircle(centers[i], Size * 0.85f, T.damage, false);
            }
            yield return new WaitForSeconds(T.duration);
            for (int i = shapes.Count - 1; i >= centers.Count; i--) { BossHazards.Release(shapes[i]); shapes.RemoveAt(i); }
            for (int i = 0; i < centers.Count; i++)
                if (waves[i] == w) BossHazards.Tint(shapes[i], new Color(1f, 0.92f, 0.78f, 0.2f), 0.05f);
        }
    }
}

sealed class Starfall : BossAttack, IBulletListener
{
    readonly LineRenderer[] rings = new LineRenderer[16];
    readonly Vector3[] spots = new Vector3[16];
    int landed, fired;
    public Starfall() : base("starfall", "STARFALL", AttackLane.Sky, AttackMode.Dream, 12f, 20f, 6, 0.8f, 3f, 1) { }
    protected override IEnumerator Telegraph(float seconds) { yield return new WaitForSeconds(Mathf.Min(0.3f, seconds)); }
    protected override IEnumerator Execute()
    {
        fired = Mathf.Clamp(Count, 1, rings.Length); landed = 0;
        float tele = Tele(T.telegraph);
        for (int i = 0; i < fired; i++)
        {
            Vector3 spot = i == 0 ? Player : Player + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * Random.Range(2.5f, 7f);
            spot = boss.ClampToArena(spot, 1.5f);
            spots[i] = spot;
            rings[i] = Keep(BossHazards.Polygon(spot, 1.9f, 40, 0f, 0.14f, Gold));
            Keep(BossHazards.Polygon(spot, 0.6f, 5, 0f, 0.08f, Cream));
            float speed = Speed;
            var b = boss.Orb(spot + Vector3.up * speed * tele, Vector3.down, speed, 0f, World.Nightmare, Gold, 0f, 1.5f);
            if (b != null) b.Faller().Listen(this, i).Trail(0.35f, 0.7f);
            yield return new WaitForSeconds(0.22f);
        }
        float until = Time.time + tele + 1f;
        while (landed < fired && Time.time < until) yield return null;
    }
    public void OnBulletEnd(EnemyBullet b, BulletEnd how)
    {
        if (!Running) return;
        landed++;
        int i = b.Tag;
        Impact();
        Fx.Burst(spots[i], Gold, 24);
        BossHazards.HitCircle(spots[i], 1.9f, T.damage, false);
        BossHazards.AddZone(spots[i], 1.7f, T.duration, 3f, new Color(1f, 0.8f, 0.45f, 0.6f));
        BossHazards.Release(rings[i]);
    }
}

sealed class TetherSeeds : BossAttack, IBulletListener
{
    readonly EnemyBullet[] seeds = new EnemyBullet[6];
    readonly int[] gens = new int[6];
    readonly LineRenderer[] tethers = new LineRenderer[6];
    public TetherSeeds() : base("tether_seeds", "TETHER SEEDS", AttackLane.Homing, AttackMode.Dream, 7.5f, 3.6f, 4, 0.8f, 4f, 1) { }
    protected override IEnumerator Execute()
    {
        int n = Mathf.Clamp(Count, 1, seeds.Length);
        float yaw = AimYaw(Emitter);
        for (int i = 0; i < n; i++)
        {
            var b = boss.Orb(Emitter, Dir(yaw - 60f + 120f * i / Mathf.Max(1, n - 1)), Speed, T.damage, World.Dream, Gold, 0f, 1.5f, false);
            seeds[i] = b;
            if (b == null) continue;
            gens[i] = b.Generation;
            b.Home(T.duration, 70f).Life(T.duration).Reward(1.5f, 0.03f).Listen(this).Trail(0.5f, 0.5f);
            tethers[i] = Keep(BossHazards.Line(Emitter, b.transform.position, 0.06f, new Color(1f, 0.85f, 0.5f, 0.7f), false));
        }
        float until = Time.time + T.duration + 0.2f;
        while (Time.time < until)
        {
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                if (tethers[i] == null) continue;
                bool alive = seeds[i] != null && seeds[i].Alive(gens[i]);
                tethers[i].enabled = alive;
                if (alive) { any = true; BossHazards.SetLine(tethers[i], Emitter + Vector3.up * 2f, seeds[i].transform.position, false); }
            }
            if (!any) break;
            yield return null;
        }
    }
    public void OnBulletEnd(EnemyBullet b, BulletEnd how)
    {
        if (how != BulletEnd.Expired) return;
        Impact();
        Vector3 p = b.transform.position;
        for (int i = 0; i < 12; i++) boss.Orb(p, Dir(i * 30f), 6f, T.damage, World.Dream, Coral, 0f, 1f);
    }
}

sealed class TendrilSlam : BossAttack
{
    Vector3 point;
    public TendrilSlam() : base("tendril_slam", "TENDRIL SLAM", AttackLane.Ground, AttackMode.Dream, 10f, 9f, 2, 0.8f, 0.5f, 1) { }
    protected override IEnumerator Telegraph(float seconds)
    {
        Vector3 to = Player - boss.Home; to.y = 0f;
        point = boss.ClampToArena(boss.Home + to.normalized * Mathf.Min(to.magnitude, 11f), 2f);
        var ring = Keep(BossHazards.Polygon(point, 2.6f, 48, 0f, 0.1f, Coral));
        var core = Keep(BossHazards.Polygon(point, 1f, 6, 30f, 0.1f, Cream));
        yield return Ramp(seconds, k => { BossHazards.Tint(ring, Coral * (0.5f + k), 0.1f + 0.3f * k); BossHazards.SetPolygon(core, point, 0.4f + 2f * k, 6, 30f + k * 90f); });
    }
    protected override IEnumerator Execute()
    {
        Impact();
        ThirdPersonCamera.Shake(0.3f);
        Fx.Burst(point + Vector3.up * 0.5f, Coral, 40);
        BossHazards.HitCircle(point, 2.6f, T.damage, false);
        int rings = boss.Phase >= 2 ? Mathf.Clamp(Count, 1, 3) : 1;
        for (int i = 0; i < rings; i++)
        {
            GroundWave.Spawn(point, boss.PlayerTransform, Speed, 0f, 180f, T.damage / 100f, Coral);
            yield return new WaitForSeconds(T.duration);
        }
    }
}

sealed class BloomRing : BossAttack
{
    public BloomRing() : base("bloom_ring", "BLOOM RING", AttackLane.Air, AttackMode.Dream, 7.5f, 6f, 24, 0.7f, 0.7f, 1) { }
    protected override IEnumerator Execute()
    {
        int rings = boss.Phase == 1 ? 2 : 3;
        for (int r = 0; r < rings; r++)
        {
            int n = Mathf.Max(12, Count);
            int gapA = Random.Range(0, n), gapB = (gapA + n / 2 + Random.Range(-2, 3)) % n;
            for (int i = 0; i < n; i++)
            {
                int da = Mathf.Abs(i - gapA), db = Mathf.Abs(i - gapB);
                if (Mathf.Min(da, n - da) <= 1 || Mathf.Min(db, n - db) <= 1) continue;
                Orb(Emitter, i * 360f / n + r * 7f, Speed, World.Dream, Pink, 0f, 1.2f);
            }
            yield return new WaitForSeconds(T.duration);
        }
    }
}

// The original Sleeper curtains, kept from the first encounter.
sealed class SeedHalo : BossAttack
{
    public SeedHalo() : base("seed_halo", "DREAM SEED HALO", AttackLane.Air, AttackMode.Dream, 7.5f, 8.5f, 4, 0.9f, 0.52f, 1) { }
    protected override IEnumerator Execute()
    {
        float yaw = AimYaw(Emitter);
        int rows = boss.Phase == 1 ? Count : Count + 2;
        for (int row = 0; row < rows; row++)
        {
            for (int bead = 0; bead < 29; bead++)
                if (!EclipseBoss.IsHaloGap(bead, row)) Orb(Emitter, yaw + EclipseBoss.HaloAngle(bead), Speed, World.Dream, Cyan, 0f, 1.2f);
            yield return new WaitForSeconds(T.duration);
        }
    }
}

sealed class TwinHelix : BossAttack
{
    public TwinHelix() : base("twin_helix", "TWIN HELIX", AttackLane.Air, AttackMode.Dream, 7.5f, 8.5f, 34, 0.9f, 0.11f, 1) { }
    protected override IEnumerator Execute()
    {
        float yaw = AimYaw(Emitter);
        Vector3 side = Dir(yaw + 90f) * 3.5f;
        for (int i = 0; i < Count; i++)
        {
            Orb(Emitter - side, yaw + EclipseBoss.HelixAngle(i, false), Speed, World.Dream, Cream, 0f, 1.1f);
            Orb(Emitter + side, yaw + EclipseBoss.HelixAngle(i, true), Speed, World.Dream, Cyan, 0f, 1.1f);
            yield return new WaitForSeconds(T.duration);
        }
    }
}

// ---------------- Nightmare ----------------

sealed class NightTerrorFan : BossAttack
{
    float yaw;
    public NightTerrorFan() : base("night_terror_fan", "NIGHT TERROR FAN", AttackLane.Air, AttackMode.Nightmare, 12f, 13f, 7, 0.6f, 0.45f, 1) { }
    protected override IEnumerator Telegraph(float seconds)
    {
        yaw = AimYaw(Emitter);
        Vector3 o = Emitter;
        Keep(BossHazards.Line(o, o + Dir(yaw - 30f) * 22f, 0.08f, new Color(1f, 0.3f, 0.1f, 0.5f)));
        Keep(BossHazards.Line(o, o + Dir(yaw + 30f) * 22f, 0.08f, new Color(1f, 0.3f, 0.1f, 0.5f)));
        yield return new WaitForSeconds(seconds);
    }
    protected override IEnumerator Execute()
    {
        int n = Mathf.Clamp(Count, 5, 9);
        int volleys = boss.Phase == 1 ? 2 : 3;
        for (int v = 0; v < volleys; v++)
        {
            float aim = Mathf.LerpAngle(yaw, AimYaw(Emitter), 0.5f);
            for (int i = 0; i < n; i++)
                Orb(Emitter, aim - 30f + 60f * i / (n - 1) + (v % 2) * 30f / (n - 1), Speed, World.Nightmare, Ember, 1f, 0.95f);
            yield return new WaitForSeconds(T.duration);
        }
    }
}

sealed class PrismBeams : BossAttack
{
    readonly LineRenderer[] beams = new LineRenderer[5];
    readonly LineRenderer[] cores = new LineRenderer[5];
    float baseAngle, direction;
    int n;
    public PrismBeams() : base("prism_beams", "PRISM BEAMS", AttackLane.Sky, AttackMode.Nightmare, 16f, 32f, 4, 0.7f, 3.2f, 2) { }
    Vector3 Inner(int i, float angle) => EclipseEncounter.ArenaCenter + Dir(angle + i * 360f / n) * EclipseArena.CoreRadius;
    Vector3 Outer(int i, float angle) => EclipseEncounter.ArenaCenter + Dir(angle + i * 360f / n) * (EclipseArena.SqueezeRadius + 1f);
    protected override IEnumerator Telegraph(float seconds)
    {
        n = Mathf.Clamp(Count, 3, 5);
        baseAngle = Random.Range(0f, 360f);
        direction = Random.value < 0.5f ? 1f : -1f;
        for (int i = 0; i < n; i++)
        {
            beams[i] = Keep(BossHazards.Line(Inner(i, baseAngle), Outer(i, baseAngle), 0.08f, new Color(0.66f, 0.25f, 1f, 0.35f)));
            cores[i] = null;
        }
        yield return Ramp(seconds, k => { for (int i = 0; i < n; i++) BossHazards.Tint(beams[i], new Color(0.66f, 0.25f, 1f, 0.3f + 0.5f * k), 0.08f + 0.12f * k); });
    }
    protected override IEnumerator Execute()
    {
        Impact();
        for (int i = 0; i < n; i++)
        {
            BossHazards.Tint(beams[i], Violet * 1.4f, 0.9f);
            cores[i] = Keep(BossHazards.Line(Inner(i, baseAngle), Outer(i, baseAngle), 0.25f, WhiteHot));
        }
        float speed = Speed * direction * (boss.Phase == 3 ? 1.25f : 1f);
        for (float t = 0f; t < T.duration; t += Time.deltaTime)
        {
            float angle = baseAngle + t * speed;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = Inner(i, angle), b = Outer(i, angle);
                BossHazards.SetLine(beams[i], a, b);
                BossHazards.SetLine(cores[i], a, b);
                BossHazards.HitSegment(a, b, 0.5f, T.damage);
            }
            yield return null;
        }
    }
}

sealed class ShatterGrid : BossAttack
{
    const float Side = 3.4f;
    readonly List<Vector3> tris = new List<Vector3>();
    readonly List<int> groups = new List<int>();
    Vector3 laneDir;
    public ShatterGrid() : base("shatter_grid", "SHATTER GRID", AttackLane.Ground, AttackMode.Nightmare, 15f, 0f, 4, 0.9f, 0.3f, 2) { }
    protected override IEnumerator Telegraph(float seconds)
    {
        tris.Clear(); groups.Clear();
        Vector3 origin = boss.ClampToArena(Player, 0f);
        laneDir = Dir(Random.Range(0f, 180f));
        Vector3 laneSide = new Vector3(laneDir.z, 0f, -laneDir.x);
        float h = Side * 0.866f;
        for (int row = -3; row <= 3; row++)
            for (int col = -6; col <= 6; col++)
            {
                bool up = ((row + col) & 1) == 0;
                Vector3 b = origin + new Vector3(col * Side * 0.5f, 0f, row * h);
                Vector3 p0 = b + new Vector3(-Side * 0.5f, 0f, up ? -h / 2f : h / 2f);
                Vector3 p1 = b + new Vector3(Side * 0.5f, 0f, up ? -h / 2f : h / 2f);
                Vector3 p2 = b + new Vector3(0f, 0f, up ? h / 2f : -h / 2f);
                Vector3 centroid = (p0 + p1 + p2) / 3f;
                if (!boss.InsideArena(centroid, 0.5f)) continue;
                Vector3 off = centroid - origin;
                // The safe path: a 3.4 m wide lane through the player's position.
                if (Mathf.Abs(Vector3.Dot(off, laneSide)) < 1.7f) continue;
                tris.Add(p0); tris.Add(p1); tris.Add(p2);
                groups.Add(Mathf.Clamp(Mathf.FloorToInt((Vector3.Dot(off, laneDir) + 12f) / 6f), 0, 3));
                var line = Keep(BossHazards.Line(p0, p1, 0.05f, new Color(1f, 0.55f, 0.1f, 0.5f)));
                line.positionCount = 3; line.loop = true;
                line.SetPosition(0, Flat(p0)); line.SetPosition(1, Flat(p1)); line.SetPosition(2, Flat(p2));
            }
        Keep(BossHazards.Line(origin - laneDir * 14f, origin + laneDir * 14f, 0.05f, new Color(0.3f, 0.9f, 1f, 0.35f)));
        yield return new WaitForSeconds(seconds);
    }
    static Vector3 Flat(Vector3 p) { p.y = BossHazards.FloorY; return p; }
    protected override IEnumerator Execute()
    {
        int count = groups.Count;
        for (int g = 0; g < 4; g++)
        {
            Impact();
            for (int i = 0; i < count; i++)
            {
                if (groups[i] != g) continue;
                Vector3 a = tris[i * 3], b = tris[i * 3 + 1], c = tris[i * 3 + 2];
                BossHazards.Tint(shapes[i], g % 2 == 0 ? Amber * 1.5f : WhiteHot, 0.3f);
                Vector3 centroid = (a + b + c) / 3f;
                Keep(BossHazards.Line(centroid, centroid + Vector3.up * 2.6f, 0.9f, new Color(0.25f, 0.05f, 0.08f, 1f), false));
                Keep(BossHazards.Line(centroid, centroid + Vector3.up * 2.2f, 0.25f, Amber, false));
                BossHazards.HitTriangle(a, b, c, T.damage);
            }
            ThirdPersonCamera.Shake(0.08f);
            yield return new WaitForSeconds(T.duration);
        }
        yield return new WaitForSeconds(0.3f);
    }
}

sealed class HunterOrbs : BossAttack, IBulletListener
{
    int live;
    public HunterOrbs() : base("hunter_orbs", "HUNTER ORBS", AttackLane.Homing, AttackMode.Nightmare, 10f, 6.5f, 3, 0.7f, 3f, 1) { }
    protected override IEnumerator Execute()
    {
        int n = Mathf.Clamp(Count, 1, 5);
        live = 0;
        float yaw = AimYaw(Emitter);
        for (int i = 0; i < n; i++)
        {
            var b = Orb(Emitter, yaw + (i - (n - 1) * 0.5f) * 40f, Speed, World.Nightmare, Soot, 0.35f, 1.9f);
            if (b == null) continue;
            live++;
            b.Home(T.duration, 95f).Life(T.duration).Shootable().Listen(this).Trail(0.3f, 0.9f);
        }
        float until = Time.time + T.duration + 0.5f;
        while (live > 0 && Time.time < until) yield return null;
    }
    public void OnBulletEnd(EnemyBullet b, BulletEnd how)
    {
        live--;
        if (how != BulletEnd.Expired || !Running) return;
        Impact();
        Vector3 p = b.transform.position;
        float yaw = AimYaw(p);
        Fx.Burst(p, Ember, 18);
        for (int i = 0; i < 5; i++) Orb(p, yaw + (i - 2) * 24f, 10f, World.Nightmare, Ember, 1f, 0.85f);
    }
}

sealed class EclipseMeteors : BossAttack, IBulletListener
{
    readonly LineRenderer[] rings = new LineRenderer[12];
    readonly Vector3[] spots = new Vector3[12];
    int pending;
    public EclipseMeteors() : base("eclipse_meteors", "ECLIPSE METEORS", AttackLane.Sky, AttackMode.Nightmare, 14f, 30f, 5, 0.6f, 3f, 1) { }
    protected override IEnumerator Telegraph(float seconds) { yield return new WaitForSeconds(0.25f); }
    protected override IEnumerator Execute()
    {
        int n = Mathf.Clamp(Count, 1, rings.Length);
        pending = 0;
        float tele = Tele(T.telegraph, true);
        for (int i = 0; i < n; i++)
        {
            Vector3 spot = i % 2 == 0 ? Player + boss.PlayerVelocity * tele : Player + Dir(Random.Range(0f, 360f)) * Random.Range(3f, 8f);
            spot = boss.ClampToArena(spot, 2f);
            spots[i] = spot;
            rings[i] = Keep(BossHazards.Polygon(spot, 2.2f, 40, 0f, 0.16f, Ember));
            Keep(BossHazards.Polygon(spot, 1.3f, 3, Random.Range(0f, 120f), 0.08f, Amber));
            float speed = Speed;
            Vector3 from = spot + Vector3.up * 15f + Dir(Random.Range(0f, 360f)) * 9f;
            float dist = (spot - from).magnitude;
            var b = boss.Orb(spot + (from - spot).normalized * speed * tele, (spot - from) / dist, speed, 0f, World.Nightmare, Ember, 1f, 1.7f);
            if (b != null) { pending++; b.Faller().Shootable().Listen(this, i).Trail(0.6f, 1.4f); }
            yield return new WaitForSeconds(0.3f);
        }
        float until = Time.time + 1.5f;
        while (pending > 0 && Time.time < until) yield return null;
    }
    public void OnBulletEnd(EnemyBullet b, BulletEnd how)
    {
        pending--;
        if (!Running) return;
        int i = b.Tag;
        BossHazards.Release(rings[i]);
        if (how != BulletEnd.Ground) return;
        Impact();
        Fx.Burst(spots[i], Ember, 30);
        ThirdPersonCamera.Shake(0.12f);
        BossHazards.HitCircle(spots[i], 2.2f, T.damage, false);
        BossHazards.AddZone(spots[i], 1.8f, T.duration, 4f, new Color(1f, 0.3f, 0.05f, 0.8f));
    }
}

sealed class WhipTendrils : BossAttack
{
    float yaw;
    public WhipTendrils() : base("whip_tendrils", "WHIP TENDRILS", AttackLane.Ground, AttackMode.Nightmare, 15f, 14f, 2, 0.8f, 0.9f, 1) { }
    protected override IEnumerator Telegraph(float seconds)
    {
        yaw = AimYaw(Emitter);
        var arc = Keep(BossHazards.Line(Emitter, Emitter, 0.2f, Blood));
        int segments = 24;
        arc.positionCount = segments;
        float r = Mathf.Clamp((Player - Emitter).magnitude, 8f, 18f);
        for (int i = 0; i < segments; i++)
        {
            Vector3 p = Emitter + Dir(yaw - 60f + 120f * i / (segments - 1)) * r;
            p.y = BossHazards.FloorY;
            arc.SetPosition(i, p);
        }
        yield return Ramp(seconds, k => BossHazards.Tint(arc, Blood * (0.5f + k), 0.15f + 0.5f * k));
    }
    protected override IEnumerator Execute()
    {
        int sweeps = Mathf.Clamp(Count, 1, 3);
        for (int i = 0; i < sweeps; i++)
        {
            GroundWave.Spawn(Emitter, boss.PlayerTransform, Speed, yaw + (i % 2 == 0 ? 0f : 25f), 60f, T.damage / 100f, Blood);
            ThirdPersonCamera.Shake(0.1f);
            yield return new WaitForSeconds(T.duration);
        }
    }
}

sealed class ThornBurst : BossAttack
{
    public ThornBurst() : base("thorn_burst", "THORN BURST", AttackLane.Air, AttackMode.Nightmare, 12f, 9f, 16, 0.8f, 0.45f, 1) { }
    protected override IEnumerator Execute()
    {
        int n = Mathf.Max(8, Count);
        int bursts = boss.Phase == 3 ? 3 : 2;
        Fx.Burst(Emitter + Vector3.up * 5f, Amber, 40);
        for (int k = 0; k < bursts; k++)
        {
            float offset = k * 180f / n;
            for (int i = 0; i < n; i++) Orb(Emitter, i * 360f / n + offset, Speed, World.Nightmare, k % 2 == 0 ? Amber : Ember, 1f, 1f);
            ThirdPersonCamera.Shake(0.12f);
            yield return new WaitForSeconds(T.duration);
        }
    }
}

sealed class TrackingBeams : BossAttack
{
    readonly LineRenderer[] lines = new LineRenderer[3];
    readonly Vector3[] aims = new Vector3[3];
    public TrackingBeams() : base("tracking_beams", "TRACKING BEAMS", AttackLane.Homing, AttackMode.Nightmare, 10f, 0f, 2, 1f, 0.25f, 1) { }
    Vector3 Origin(int i) => Emitter + Vector3.up * 4f + Dir(i * 120f) * 2.5f;
    protected override IEnumerator Telegraph(float seconds)
    {
        int n = Mathf.Clamp(Count, 1, 3);
        for (int i = 0; i < n; i++) { aims[i] = Player; lines[i] = Keep(BossHazards.Line(Origin(i), Player, 0.03f, new Color(1f, 0.1f, 0.1f, 0.6f), false)); }
        for (float t = 0f; t < seconds; t += Time.deltaTime)
        {
            for (int i = 0; i < n; i++)
            {
                aims[i] = Vector3.Lerp(aims[i], Player + Vector3.up, Time.deltaTime * (4f + i));
                BossHazards.SetLine(lines[i], Origin(i), aims[i], false);
            }
            yield return null;
        }
    }
    protected override IEnumerator Execute()
    {
        int n = Mathf.Clamp(Count, 1, 3);
        for (int i = 0; i < n; i++) BossHazards.Tint(lines[i], new Color(1f, 0.2f, 0.2f, 1f), 0.08f);
        yield return new WaitForSeconds(T.duration);
        Impact();
        for (int i = 0; i < n; i++)
        {
            Vector3 o = Origin(i), d = aims[i] - o;
            Vector3 end = o + d.normalized * 60f;
            BossHazards.SetLine(lines[i], o, end, false);
            BossHazards.Tint(lines[i], WhiteHot, 0.45f);
            // The beam slopes from the boss into the floor; resolve it on the floor plane.
            BossHazards.HitSegment(Emitter, aims[i] + (aims[i] - Emitter).normalized * 20f, 0.6f, T.damage);
        }
        yield return new WaitForSeconds(0.2f);
    }
}

// ---------------- Both worlds ----------------

sealed class MandalaBloom : BossAttack
{
    public MandalaBloom() : base("mandala_bloom", "MANDALA BLOOM", AttackLane.Air, AttackMode.Both, 9f, 5f, 20, 0.9f, 0.55f, 2) { }
    protected override IEnumerator Execute()
    {
        World w = ModeWorld;
        bool dream = w == World.Dream;
        int rings = 3 + (boss.Phase - 1);
        int n = Mathf.Max(12, Count);
        for (int r = 0; r < rings; r++)
        {
            float spin = boss.Phase >= 2 ? (r % 2 == 0 ? 18f : -18f) : 0f;
            for (int i = 0; i < n; i++)
            {
                // Four readable petal gaps per ring that walk one slot per ring.
                if ((i + r) % (n / 4) == 0) continue;
                var b = Orb(Emitter, i * 360f / n + r * 9f, Speed, w, dream ? (r % 2 == 0 ? Pink : Cyan) : (r % 2 == 0 ? Ember : Violet), dream ? 0f : 1f, dream ? 1.1f : 0.95f);
                if (b != null) b.Curve(spin);
            }
            yield return new WaitForSeconds(T.duration);
        }
    }
}

sealed class PetalBlade : BossAttack
{
    LineRenderer blade, edge, lane;
    Vector3 start, across, along;
    public PetalBlade() : base("petal_blade", "PETAL BLADE", AttackLane.Ground, AttackMode.Both, 13f, 10f, 1, 1f, 0f, 2) { }
    protected override IEnumerator Telegraph(float seconds)
    {
        // A huge tilted strip sweeps across the arena through the player's position.
        along = Dir(Random.Range(0f, 180f));
        across = new Vector3(along.z, 0f, -along.x);
        if (Vector3.Dot(Player - EclipseEncounter.ArenaCenter, across) < 0f) across = -across;
        start = Player + across * 16f;
        bool dream = DreamLook;
        Color c = dream ? Pink : Amber;
        lane = Keep(BossHazards.Line(Player - along * 30f, Player + along * 30f, 1.8f, new Color(c.r, c.g, c.b, 0.15f)));
        blade = Keep(BossHazards.Line(start - along * 30f, start + along * 30f, 0.2f, new Color(c.r, c.g, c.b, 0.5f)));
        edge = Keep(BossHazards.Line(start - along * 30f + Vector3.up * 1.5f, start + along * 30f + Vector3.up * 1.5f, 0.05f, c, false));
        yield return Ramp(seconds, k => BossHazards.Tint(lane, new Color(c.r, c.g, c.b, 0.1f + 0.25f * k), 1.8f));
    }
    protected override IEnumerator Execute()
    {
        bool dream = DreamLook;
        Color c = dream ? Coral : Soot, rim = dream ? Cream : Amber;
        BossHazards.Tint(blade, c * 1.5f, 1.8f);
        BossHazards.Tint(edge, rim * 1.5f, 0.35f);
        Impact();
        float travel = 32f, speed = Speed;
        for (float d = 0f; d < travel; d += speed * Time.deltaTime)
        {
            Vector3 p = start - across * d;
            BossHazards.SetLine(blade, p - along * 30f, p + along * 30f);
            BossHazards.SetLine(edge, p - along * 30f + Vector3.up * 1.4f, p + along * 30f + Vector3.up * 1.4f, false);
            BossHazards.HitSegment(p - along * 30f, p + along * 30f, 0.9f, T.damage);
            if (Time.frameCount % 3 == 0) Fx.Burst(p + along * Random.Range(-14f, 14f) + Vector3.up, rim, 3);
            yield return null;
        }
    }
}

// The boss dissolves, reappears beside the player on a floor sigil and slams.
// Dodge it and the boss is stunned for the wake window, taking full damage in both worlds.
sealed class Sleepwalk : BossAttack
{
    readonly Vector3[] points = new Vector3[4];
    public bool Dodged { get; private set; }
    public Sleepwalk() : base("sleepwalk", "SLEEPWALK", AttackLane.Ground, AttackMode.Both, 0f, 0f, 3, 0.9f, 0.35f, 3) { }
    protected override IEnumerator Telegraph(float seconds) { yield break; }
    protected override IEnumerator Execute()
    {
        var data = boss.Data;
        bool nightmare = WorldState.CurrentOrDream == World.Nightmare;
        bool chain = nightmare && boss.Phase >= 2;
        float damage = nightmare ? data.sleepwalkNightmareDamage : data.sleepwalkDreamDamage;
        Color tint = nightmare ? Ember : Pink, rim = nightmare ? Amber : Cream;
        float sigil = Mathf.Max(0.5f, boss.PhaseData.sigilTime / boss.Difficulty);
        Vector3 center = boss.ClampToArena(Player, 1f);
        int strikes;
        if (chain)
        {
            float a = Random.Range(0f, 360f);
            for (int i = 0; i < 3; i++) points[i] = boss.ClampToArena(center + Dir(a + i * 120f) * data.sleepwalkDistance, 1f);
            points[3] = center;
            strikes = 4;
        }
        else
        {
            Vector3 side = boss.CameraRight * (Random.value < 0.5f ? -1f : 1f);
            Vector3 offset = Random.value < 0.3f ? -boss.CameraForward : side;
            points[0] = boss.ClampToArena(Player + offset * data.sleepwalkDistance, 1f);
            strikes = 1;
        }

        Dodged = true;
        for (int s = 0; s < strikes; s++)
        {
            bool final = s == strikes - 1 && chain;
            Vector3 target = points[s];
            float radius = data.slamRadius * (final ? 1.35f : 1f);
            LucidAudio.Hook("boss_teleport_out");
            Fx.Burst(boss.transform.position, tint, 50);
            var ring = Keep(BossHazards.Polygon(target, radius, 48, 0f, 0.12f, tint));
            var starA = Keep(BossHazards.Polygon(target, radius * 0.8f, 3, 0f, 0.07f, rim));
            var starB = Keep(BossHazards.Polygon(target, radius * 0.8f, 3, 60f, 0.07f, rim));
            var whoosh = Keep(BossHazards.Line(boss.transform.position, target + Vector3.up, 0.3f, tint, false));
            Vector3 from = boss.transform.position;
            float wait = s == 0 ? sigil : Mathf.Max(0.45f, sigil * 0.75f);
            boss.BeginTell(final ? "SLEEPWALK  /  FINAL SLAM" : "SLEEPWALK  /  LEAVE THE SIGIL", wait);
            yield return Ramp(wait, k =>
            {
                boss.SetVanish(Mathf.Clamp01(1f - k * 4f));
                BossHazards.Tint(ring, tint * (0.5f + k), 0.12f + 0.3f * k);
                BossHazards.SetPolygon(starA, target, radius * 0.8f, 3, k * 120f);
                BossHazards.SetPolygon(starB, target, radius * 0.8f, 3, 60f - k * 120f);
                BossHazards.SetLine(whoosh, Vector3.Lerp(from, target + Vector3.up, k * k), target + Vector3.up, false);
            });
            boss.MoveTo(target + Vector3.up * 4.5f);
            boss.SetVanish(1f);
            LucidAudio.Hook("boss_teleport_in");
            Impact();
            Fx.Burst(target + Vector3.up * 0.5f, tint, 80);
            Fx.Burst(target + Vector3.up * 0.5f, Color.white, 25);
            ThirdPersonCamera.Shake(final ? 0.55f : 0.4f);
            GameFeel.HitStop(0.06f);
            if (BossHazards.HitCircle(target, radius, damage, false)) Dodged = false;
            GroundWave.Spawn(target, boss.PlayerTransform, 12f, 0f, 180f, damage * 0.6f / 100f, tint);
            int n = data.sleepwalkBurstCount;
            for (int i = 0; i < n; i++)
                boss.Orb(target + Vector3.up, Dir(i * 360f / n + s * 15f), nightmare ? 10f : 7f, damage * 0.6f, nightmare ? World.Nightmare : World.Dream,
                    nightmare ? Ember : Coral, nightmare ? 1f : 0f, 1f);
            for (int i = shapes.Count - 1; i >= 0; i--) BossHazards.Release(shapes[i]);
            shapes.Clear();
            if (s < strikes - 1) yield return new WaitForSeconds(0.3f);
        }
        if (Dodged)
        {
            boss.Stun(data.wakeWindow);
            yield return new WaitForSeconds(data.wakeWindow);
        }
        else yield return new WaitForSeconds(0.6f);
        LucidAudio.Hook("boss_teleport_out");
        Fx.Burst(boss.transform.position, tint, 40);
        yield return Ramp(0.25f, k => boss.SetVanish(1f - k));
        boss.MoveTo(boss.Home);
        yield return Ramp(0.3f, k => boss.SetVanish(k));
    }
    protected override void Cleanup()
    {
        base.Cleanup();
        if (boss != null) { boss.MoveTo(boss.Home); boss.SetVanish(1f); }
    }
}
