using System.Collections;
using UnityEngine;

// Attacks added with the owl. They use the same building blocks as the rest of the catalogue
// (floor rings, HitCircle, GroundWave, Orb) and the same tuning table, keyed by id.

// The owl takes off, hangs over the arena, then dives onto the player's position. The landing hits
// everything in the ring and sends a shockwave across the floor (jump it), plus a burst of feathers.
sealed class TalonDive : BossAttack
{
    Vector3 start, target;
    const float Radius = 6f, Climb = 10f;
    public TalonDive() : base("talon_dive", "TALON DIVE", AttackLane.Ground, AttackMode.Both, 16f, 13f, 14, 1.25f, 0.4f, 2) { }

    protected override IEnumerator Telegraph(float seconds)
    {
        Color tint = DreamLook ? Gold : Ember;
        start = boss.transform.position;
        target = boss.ClampToArena(Player, 1f);
        var ring = Keep(BossHazards.Polygon(target, Radius, 56, 0f, 0.14f, tint));
        var cross = Keep(BossHazards.Polygon(target, Radius * 0.75f, 4, 45f, 0.08f, WhiteHot));
        var fill = Keep(BossHazards.Polygon(target, 0.5f, 56, 0f, 0.1f, tint));
        OwlAudio.Play(OwlAudio.Sound.Screech, 0.9f, 0.9f);
        OwlAudio.Play(OwlAudio.Sound.Whoosh, 0.8f, 0.8f);
        yield return Ramp(seconds, k =>
        {
            // The mark follows the player for the first part of the climb, then locks.
            if (k < 0.65f) target = boss.ClampToArena(Player, 1f);
            EclipseVisual.Flight = Mathf.Clamp01(k * 3f);
            float rise = 1f - (1f - k) * (1f - k);
            boss.MoveTo(Vector3.Lerp(start, new Vector3(Mathf.Lerp(start.x, target.x, 0.5f), start.y + Climb, Mathf.Lerp(start.z, target.z, 0.5f)), rise));
            BossHazards.SetPolygon(ring, target, Radius, 56, 0f);
            BossHazards.SetPolygon(cross, target, Radius * 0.75f, 4, 45f + k * 180f);
            BossHazards.SetPolygon(fill, target, Mathf.Lerp(0.5f, Radius, k), 56, 0f);
            BossHazards.Tint(ring, tint * (0.5f + k), 0.14f + 0.3f * k);
        });
    }

    protected override IEnumerator Execute()
    {
        Color tint = DreamLook ? Gold : Ember;
        World world = ModeWorld;
        // It lands on the far side of the ring from where it took off, so it never drops onto the camera.
        Vector3 away = start - target; away.y = 0f;
        Vector3 from = boss.transform.position, land = target + away.normalized * 3.2f + Vector3.up * 4.5f;
        OwlAudio.Play(OwlAudio.Sound.Whoosh, 1f, 1.5f);
        yield return Ramp(0.26f, k => boss.MoveTo(Vector3.Lerp(from, land, k * k)));
        for (int i = shapes.Count - 1; i >= 0; i--) BossHazards.Release(shapes[i]);
        shapes.Clear();

        Impact();
        EclipseVisual.Slam = 1f;
        OwlAudio.Play(OwlAudio.Sound.Boom, 1f);
        ThirdPersonCamera.Shake(0.65f);
        GameFeel.HitStop(0.06f);
        Fx.Burst(target + Vector3.up * 0.5f, tint, 90);
        Fx.Burst(target + Vector3.up * 0.5f, Color.white, 30);
        CombatPulse.Spawn(target + Vector3.up * 0.2f, Radius * 1.6f, tint);
        BossHazards.HitCircle(target, Radius, T.damage, false);
        GroundWave.Spawn(target, boss.PlayerTransform, Speed, 0f, 180f, T.damage * 0.6f / 100f, tint);
        int n = Count;
        for (int i = 0; i < n; i++)
            boss.Orb(target + Vector3.up, Dir(i * 360f / n), world == World.Dream ? 7f : 10f, T.damage * 0.5f, world, tint, world == World.Dream ? 0f : 1f, 1.05f);
        yield return new WaitForSeconds(T.duration);
        if (boss.Phase >= 2)
        {
            GroundWave.Spawn(target, boss.PlayerTransform, Speed * 1.25f, 0f, 180f, T.damage * 0.6f / 100f, tint);
            OwlAudio.Play(OwlAudio.Sound.Boom, 0.6f, 1.3f);
        }
        yield return new WaitForSeconds(0.55f);

        // Lift off and glide back to its perch.
        EclipseVisual.Flight = 1f;
        OwlAudio.Play(OwlAudio.Sound.Whoosh, 0.7f, 0.9f);
        Vector3 back = boss.transform.position;
        yield return Ramp(0.7f, k =>
        {
            float s = Mathf.SmoothStep(0f, 1f, k);
            boss.MoveTo(Vector3.Lerp(back, boss.Home, s) + Vector3.up * Mathf.Sin(s * Mathf.PI) * 3f);
            EclipseVisual.Flight = 1f - k * k;
        });
    }

    protected override void Cleanup()
    {
        base.Cleanup();
        EclipseVisual.Flight = 0f;
        if (boss != null) boss.MoveTo(boss.Home);
    }
}

// Four wingbeats, each throwing a wide fan of fast feathers at the player with one gap to slip through.
sealed class FeatherGale : BossAttack
{
    public FeatherGale() : base("feather_gale", "FEATHER GALE", AttackLane.Air, AttackMode.Nightmare, 10f, 15f, 11, 0.7f, 0.42f, 1) { }

    protected override IEnumerator Execute()
    {
        int feathers = Mathf.Clamp(Count, 7, 15);
        int gusts = boss.Phase >= 2 ? 4 : 3;
        int gap = Random.Range(2, feathers - 2);
        for (int g = 0; g < gusts; g++)
        {
            EclipseVisual.Gust = 1f;
            OwlAudio.Play(OwlAudio.Sound.Whoosh, 0.9f, 1f + g * 0.08f);
            ThirdPersonCamera.Shake(0.12f);
            float aim = AimYaw(Emitter);
            for (int i = 0; i < feathers; i++)
            {
                if (Mathf.Abs(i - gap) <= 1) continue;
                float yaw = aim + Mathf.Lerp(-52f, 52f, (float)i / (feathers - 1));
                var b = Orb(Emitter, yaw, Speed, World.Nightmare, g % 2 == 0 ? Violet : Ember, 1f, 1.1f);
                if (b != null) b.Trail(0.35f, 0.5f);
            }
            // The gap walks sideways so each wave asks for a small step, not a guess.
            gap = Mathf.Clamp(gap + (Random.value < 0.5f ? -2 : 2), 2, feathers - 3);
            yield return new WaitForSeconds(T.duration);
        }
    }
}

// Dream counterpart: feathers drift down in a slow ring that closes on the player, then lifts away.
sealed class MoonlitMolt : BossAttack
{
    public MoonlitMolt() : base("moonlit_molt", "MOONLIT MOLT", AttackLane.Air, AttackMode.Dream, 8f, 5f, 16, 0.8f, 1.1f, 1) { }

    protected override IEnumerator Execute()
    {
        int n = Mathf.Clamp(Count, 10, 22);
        for (int wave = 0; wave < 2; wave++)
        {
            EclipseVisual.Gust = 0.6f;
            OwlAudio.Play(OwlAudio.Sound.Hoot, 0.8f, wave == 0 ? 1f : 0.85f);
            Vector3 center = Player;
            float radius = 12f, offset = wave * 180f / n;
            for (int i = 0; i < n; i++)
            {
                // One quarter of the ring is left open: walk out through it.
                if (i % n < n / 4) continue;
                float yaw = i * 360f / n + offset;
                Vector3 from = center + Dir(yaw) * radius + Vector3.up;
                var b = boss.Orb(from, -Dir(yaw), Speed, T.damage, World.Dream, i % 2 == 0 ? Cyan : Gold, i % 2 == 0 ? 0f : 1f, 1.1f);
                if (b != null) b.Life(radius * 2f / Mathf.Max(1f, Speed)).Trail(0.45f, 0.55f);
            }
            yield return new WaitForSeconds(T.duration);
        }
    }
}
