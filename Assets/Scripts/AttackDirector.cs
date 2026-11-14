using System.Collections.Generic;
using UnityEngine;

// Spends a per-phase budget each cycle: small attack 1, big 2, teleport 3. Picks lanes that differ,
// never repeats the last attack, and never starts anything while Sleepwalk runs.
public sealed class AttackDirector
{
    public readonly List<BossAttack> Catalog = BossAttack.Catalog();
    readonly BossAttack sleepwalk;
    readonly List<BossAttack> running = new List<BossAttack>();
    readonly List<BossAttack> candidates = new List<BossAttack>();
    readonly EclipseBoss boss;
    BossAttack last;
    bool inCycle;
    float gapUntil, nextTeleport;
    int debugIndex = -1;

    public AttackDirector(EclipseBoss owner, BossAttackData data)
    {
        boss = owner;
        sleepwalk = Catalog.Find(a => a.Id == "sleepwalk");
        foreach (var a in Catalog) a.Bind(data);
    }

    public bool Busy => inCycle;
    public bool Teleporting => sleepwalk.Running;

    public void ScheduleTeleport()
    {
        var p = boss.PhaseData;
        nextTeleport = Time.time + Random.Range(p.teleportMin, p.teleportMax);
    }

    public void Rest(float seconds) { gapUntil = Time.time + seconds; }

    // Returns true when a cycle just finished and the boss may change phase.
    public bool Tick()
    {
        for (int i = running.Count - 1; i >= 0; i--) if (!running[i].Running) running.RemoveAt(i);
        if (inCycle)
        {
            if (running.Count > 0) return false;
            inCycle = false;
            var p = boss.PhaseData;
            gapUntil = Time.time + Random.Range(p.gapMin, p.gapMax) / boss.Difficulty;
            return true;
        }
        if (Time.time < gapUntil) return false;
        StartCycle();
        return false;
    }

    void StartCycle()
    {
        var p = boss.PhaseData;
        if (!boss.BindingReady && sleepwalk.Enabled && Time.time >= nextTeleport)
        {
            ScheduleTeleport();
            Launch(sleepwalk, 0f);
            return;
        }
        World w = WorldState.CurrentOrDream;
        int budget = p.budget;
        var first = Pick(w, null, budget);
        if (first == null) { gapUntil = Time.time + 0.5f; return; }
        Launch(first, 0f);
        budget -= first.Cost;
        if (p.overlay > 0 && budget >= 1 && !boss.BindingReady)
        {
            var second = Pick(w, first, p.overlay == 1 ? 1 : budget);
            if (second != null) Launch(second, p.overlay == 1 ? 0.9f : 0.15f);
        }
    }

    BossAttack Pick(World w, BossAttack other, int maxCost)
    {
        candidates.Clear();
        foreach (var a in Catalog)
        {
            if (a == sleepwalk || !a.Enabled || a.Running || a == last || !a.Fits(w) || a.Cost > maxCost) continue;
            if (other != null && a.Lane == other.Lane) continue;
            // The finale needs absorbable seeds so the player can charge the binding.
            if (boss.BindingReady && (a.Mode != AttackMode.Dream || a.Lane != AttackLane.Air)) continue;
            candidates.Add(a);
        }
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)];
    }

    void Launch(BossAttack a, float delay)
    {
        last = a;
        inCycle = true;
        running.Add(a);
        boss.StartCoroutine(a.Run(boss, delay));
    }

    public void StopAll()
    {
        boss.StopAllCoroutines();
        foreach (var a in Catalog) a.Abort();
        running.Clear();
        inCycle = false;
    }

    public void ForceTeleport()
    {
        StopAll();
        ScheduleTeleport();
        Launch(sleepwalk, 0f);
    }

    // Debug: run every attack in turn, in its own world.
    public string CycleNext()
    {
        StopAll();
        debugIndex = (debugIndex + 1) % Catalog.Count;
        var a = Catalog[debugIndex];
        if (a.Mode != AttackMode.Both && WorldState.Instance != null)
            WorldState.Instance.Set(a.Mode == AttackMode.Dream ? World.Dream : World.Nightmare);
        Launch(a, 0.2f);
        return a.Name;
    }
}
