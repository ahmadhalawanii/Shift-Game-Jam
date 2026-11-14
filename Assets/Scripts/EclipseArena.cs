using UnityEngine;

// Combat staging only. A dark, flush floor makes bright orbs and ground tells legible.
public sealed class EclipseArena : MonoBehaviour
{
    GameObject root;
    Material floor, markings;
    Mesh mesh, ruinMesh;
    Material ruins;
    GameObject anchor;
    LineRenderer boundary, fracture;
    EclipseBoss boss;
    public const float FusedRadius = 22f;
    public const float CoreRadius = 6f;
    const float TellSeconds = 1.4f;
    const float StrikeSeconds = 0.32f;
    const float RuneRadius = 3.4f;
    readonly LineRenderer[] runes = new LineRenderer[3];
    LineRenderer coreBoundary;
    PlayerController player;
    float nextHazard, hazardStarted;
    int activeRunes;
    bool struck;
    void Start()
    {
        root=new GameObject("Eclipse combat floor");
        root.transform.position=EclipseEncounter.ArenaCenter+Vector3.up*0.09f;
        floor=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floor.SetColor("_BaseColor",new Color(0.055f,0.065f,0.085f)); floor.SetFloat("_Smoothness",0.25f);
        markings=new Material(Shader.Find("Lucid/GlowParticle")); markings.SetFloat("_Trail",1f);
        markings.SetColor("_Color",new Color(0.22f,0.28f,0.4f));
        const int count=128;
        var vertices=new Vector3[count+1]; var uv=new Vector2[count+1]; var triangles=new int[count*3];
        for(int i=0;i<count;i++)
        {
            float a=i*Mathf.PI*2f/count;
            vertices[i+1]=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*(EclipseEncounter.ArenaRadius+0.7f);
            uv[i+1]=new Vector2(vertices[i+1].x,vertices[i+1].z)*0.05f;
            triangles[i*3]=0; triangles[i*3+1]=(i+1)%count+1; triangles[i*3+2]=i+1;
        }
        mesh=new Mesh { name="Eclipse arena floor" }; mesh.vertices=vertices; mesh.triangles=triangles; mesh.uv=uv;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        root.AddComponent<MeshFilter>().sharedMesh=mesh; root.AddComponent<MeshRenderer>().sharedMaterial=floor;
        boss = GetComponent<EclipseEncounter>().Boss;
        boundary = Ring(27f, 0.14f);
        Ring(20f, 0.035f);
        Ring(10f, 0.035f);
        coreBoundary = Ring(CoreRadius, 0.12f);
        for (int i = 0; i < runes.Length; i++)
        {
            runes[i] = Ring(RuneRadius, 0.18f);
            runes[i].name = "Arena eruption warning";
            runes[i].enabled = false;
        }
        player = FindFirstObjectByType<PlayerController>();
        fracture = Ring(FusedRadius, 0.28f);
        fracture.startColor = fracture.endColor = new Color(1f, 0.22f, 0.42f);
        var sigil = Ring(1.65f, 0.17f);
        sigil.transform.position = EclipseBoss.DreamAnchorPosition;
        sigil.transform.rotation = Quaternion.Euler(65f, 0f, 0f);
        sigil.startColor = sigil.endColor = new Color(0.15f, 1f, 0.9f);
        anchor = sigil.gameObject;
        BuildRuins();
        // Hide only small scattered nature inside the combat footprint, at runtime.
        foreach (GameObject group in gameObject.scene.GetRootGameObjects())
        {
            if (group.name != "Meadow_DreamOnly" && group.name != "DeadWood_NightmareOnly" &&
                group.name != "Rocks") continue;
            foreach (Transform child in group.transform)
            {
                Vector3 delta = child.position - EclipseEncounter.ArenaCenter;
                delta.y = 0f;
                if (delta.sqrMagnitude < 26f * 26f) child.gameObject.SetActive(false);
            }
        }
    }
    void BuildRuins()
    {
        ruins = new Material(floor);
        const int sides = 6;
        var vertices = new Vector3[sides * 3 + 1];
        var triangles = new int[sides * 15];
        for (int i = 0; i < sides; i++)
        {
            float angle = i * Mathf.PI * 2f / sides;
            Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            vertices[i] = radial * 1.05f;
            vertices[i + sides] = radial * 0.76f + Vector3.up * 0.65f;
            vertices[i + sides * 2] = radial * 0.56f + new Vector3(0.35f, 3.1f + 0.25f * Mathf.Sin(angle), 0f);
            int next = (i + 1) % sides;
            int offset = i * 15;
            for (int ring = 0; ring < 2; ring++)
            {
                int row = ring * sides;
                int j = offset + ring * 6;
                triangles[j] = row + i;
                triangles[j + 1] = row + i + sides;
                triangles[j + 2] = row + next;
                triangles[j + 3] = row + next;
                triangles[j + 4] = row + i + sides;
                triangles[j + 5] = row + next + sides;
            }
            triangles[offset + 12] = sides * 3;
            triangles[offset + 13] = sides * 2 + next;
            triangles[offset + 14] = sides * 2 + i;
        }
        vertices[sides * 3] = new Vector3(0.35f, 3.2f, 0f);
        ruinMesh = new Mesh { name = "Broken hexagonal standing stone" };
        ruinMesh.vertices = vertices;
        ruinMesh.triangles = triangles;
        ruinMesh.RecalculateNormals();
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI * 2f / 10f + 0.2f;
            var stone = new GameObject("Broken outer standing stone");
            stone.transform.SetParent(root.transform, false);
            stone.transform.localPosition = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 29.5f;
            stone.transform.localRotation = Quaternion.Euler(0f, i * 137f, i % 2 == 0 ? 12f : -8f);
            stone.transform.localScale = Vector3.one * (0.8f + i % 3 * 0.22f);
            stone.AddComponent<MeshFilter>().sharedMesh = ruinMesh;
            stone.AddComponent<MeshRenderer>().sharedMaterial = ruins;
        }
    }

    void Update()
    {
        bool dream = WorldState.Instance == null || WorldState.Instance.Current == World.Dream;
        bool fused = boss != null && boss.Phase == 3;
        anchor.SetActive(dream && boss != null && boss.Engaged);
        fracture.enabled = fused;
        boundary.enabled = !fused;
        coreBoundary.enabled = boss != null && boss.Engaged;
        UpdateHazards(dream);
        float blend = WorldState.Instance != null ? WorldState.Instance.Mix : 0f;
        ruins.SetColor("_BaseColor", Color.Lerp(new Color(0.39f, 0.32f, 0.44f),
            new Color(0.091f, 0.042f, 0.062f), blend));
        floor.SetColor("_BaseColor", Color.Lerp(new Color(0.17f, 0.145f, 0.23f),
            new Color(0.072f, 0.035f, 0.055f), blend));
        markings.SetColor("_Color", Color.Lerp(new Color(0.41f, 0.75f, 0.85f),
            new Color(0.8f, 0.27f, 0.35f), blend));
    }

    void UpdateHazards(bool dream)
    {
        bool suspended = boss == null || !boss.Engaged || boss.InTransformation || boss.BindingReady ||
            EclipseEncounter.Finished || player == null || player.Recalling;
        if (suspended)
        {
            EndHazard();
            nextHazard = Time.time + 4f;
            return;
        }

        if (activeRunes == 0)
        {
            // The arena starts attacking in Nightmare, then stays active in the fused phase.
            if ((dream && boss.Phase < 3) || Time.time < nextHazard) return;
            BeginHazard();
        }

        float age = Time.time - hazardStarted;
        if (age >= TellSeconds + StrikeSeconds)
        {
            EndHazard();
            nextHazard = Time.time + (boss.Phase == 3 ? 3.2f : 5f);
            return;
        }

        bool impact = age >= TellSeconds;
        float progress = Mathf.Clamp01(age / TellSeconds);
        for (int i = 0; i < activeRunes; i++)
        {
            var rune = runes[i];
            rune.widthMultiplier = impact ? 0.55f : Mathf.Lerp(0.10f, 0.26f, progress);
            Color tint = impact ? new Color(1f, 0.72f, 0.4f) : Color.Lerp(
                new Color(1f, 0.24f, 0.07f), new Color(1f, 0.53f, 0.12f), progress);
            rune.startColor = rune.endColor = tint;
        }
        if (!impact || struck) return;
        struck = true;
        for (int i = 0; i < activeRunes; i++)
        {
            Vector3 center = runes[i].transform.position;
            Fx.Burst(center, new Color(1f, 0.34f, 0.12f), 12);
            Vector3 offset = player.transform.position - center;
            float height = offset.y;
            offset.y = 0f;
            if (offset.sqrMagnitude <= RuneRadius * RuneRadius && height < 0.7f)
                player.Damage(0.13f);
        }
    }

    void BeginHazard()
    {
        hazardStarted = Time.time;
        struck = false;
        activeRunes = boss.Phase == 1 ? 1 : boss.Phase == 2 ? 2 : 3;
        Vector3 offset = player.transform.position - EclipseEncounter.ArenaCenter;
        offset.y = 0f;
        Vector3 direction = offset.sqrMagnitude > 0.01f ? offset.normalized : Vector3.back;
        float limit = boss.Phase == 3 ? FusedRadius : EclipseEncounter.ArenaRadius;
        float distance = Mathf.Clamp(offset.magnitude, CoreRadius + 2f, limit - 1f);
        for (int i = 0; i < activeRunes; i++)
        {
            float angle = i == 0 ? 0f : (i == 1 ? 36f : -36f);
            runes[i].transform.position = EclipseEncounter.ArenaCenter + Vector3.up * 0.13f +
                Quaternion.Euler(0f, angle, 0f) * direction * distance;
            runes[i].enabled = true;
        }
        LucidAudio.Play(LucidAudio.Cue.Warning);
    }

    void EndHazard()
    {
        for (int i = 0; i < activeRunes; i++) runes[i].enabled = false;
        activeRunes = 0;
    }

    LineRenderer Ring(float radius,float width)
    {
        var go=new GameObject("Arena engraving"); go.transform.SetParent(root.transform,false);
        var line=go.AddComponent<LineRenderer>(); line.sharedMaterial=markings; line.useWorldSpace=false; line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        line.loop=true; line.widthMultiplier=width; line.positionCount=128;
        for(int i=0;i<128;i++) { float a=i*Mathf.PI*2f/128f; line.SetPosition(i,new Vector3(Mathf.Cos(a)*radius,0.015f,Mathf.Sin(a)*radius)); }
        return line;
    }
    void OnDestroy()
    {
        if(root!=null) Destroy(root); if(mesh!=null) Destroy(mesh);
        if (ruinMesh != null) Destroy(ruinMesh);
        if (ruins != null) Destroy(ruins);
        if(floor!=null) Destroy(floor); if(markings!=null) Destroy(markings);
    }
}
