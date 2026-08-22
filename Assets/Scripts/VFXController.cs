using UnityEngine;

// Controls instantiation, lifecycle, and audio triggering for particle visual effects.
public class VFXController : MonoBehaviour
{
    public static VFXController Instance { get; private set; }

    [Header("VFX Prefabs")]
    [Tooltip("Particle effect prefab spawned on Diamond tile collection.")]
    public GameObject diamondVFX;

    [Tooltip("Particle effect prefab spawned on Lava tile hit.")]
    public GameObject fireVFX;

    [Tooltip("Particle effect prefab spawned on Island/Grass tile click.")]
    public GameObject grassVFX;

    [Header("Text VFX Prefabs")]
    [Tooltip("Text particle prefab (+100) spawned under the primary Diamond particle effect.")]
    public GameObject diamondTextVFX;

    [Tooltip("Text particle prefab (OUCH) spawned under the primary Lava particle effect.")]
    public GameObject lavaTextVFX;

    [Tooltip("Vertical Y-axis offset for text particle effect placement.")]
    public float textVFXOffsetY = -0.5f;

    [Header("VFX Settings")]
    [Tooltip("Fallback lifetime in seconds if particle system duration cannot be calculated.")]
    public float defaultVFXLifetime = 2.0f;

    [Tooltip("Z-axis offset to ensure particles render in front of 2D tiles.")]
    public float spawnZOffset = -1.0f;

    [Tooltip("Triggers corresponding sound effect in AudioManager when playing VFX.")]
    public bool triggerAudioWithVFX = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // Plays the corresponding visual effect and audio for the specified tile type at a world position.
    // tileType: Tile type interacted with.
    // worldPosition: World coordinate of the interaction.
    public void PlayVFX(TileType tileType, Vector3 worldPosition)
    {
        switch (tileType)
        {
            case TileType.Diamond:
                PlayDiamondVFX(worldPosition);
                break;
            case TileType.Lava:
                PlayFireVFX(worldPosition);
                break;
            case TileType.Island:
                PlayGrassVFX(worldPosition);
                break;
        }
    }

    // Spawns Diamond particle and score text VFX, triggering positive audio.
    public void PlayDiamondVFX(Vector3 worldPosition)
    {
        SpawnAndConfigureVFX(diamondVFX, worldPosition, "DiamondVFX");

        if (diamondTextVFX != null)
        {
            Vector3 textPos = worldPosition + new Vector3(0f, textVFXOffsetY, 0f);
            SpawnAndConfigureVFX(diamondTextVFX, textPos, "DiamondTextVFX");
        }

        if (triggerAudioWithVFX && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPositiveAudio(worldPosition);
        }
    }

    // Spawns Fire particle and damage text VFX, triggering damage audio.
    public void PlayFireVFX(Vector3 worldPosition)
    {
        SpawnAndConfigureVFX(fireVFX, worldPosition, "FireVFX");

        if (lavaTextVFX != null)
        {
            Vector3 textPos = worldPosition + new Vector3(0f, textVFXOffsetY, 0f);
            SpawnAndConfigureVFX(lavaTextVFX, textPos, "LavaTextVFX");
        }

        if (triggerAudioWithVFX && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayDamageAudio(worldPosition);
        }
    }

    // Spawns Grass particle VFX and triggers grass rustling audio.
    public void PlayGrassVFX(Vector3 worldPosition)
    {
        SpawnAndConfigureVFX(grassVFX, worldPosition, "GrassVFX");

        if (triggerAudioWithVFX && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGrassRustlingAudio(worldPosition);
        }
    }

    // Instantiates a VFX prefab, configures single-play playback, and schedules auto-destruction.
    private GameObject SpawnAndConfigureVFX(GameObject prefab, Vector3 position, string debugName)
    {
        if (prefab == null)
        {
            Debug.LogWarning($"[VFXController] Prefab for '{debugName}' is not assigned in the Inspector!");
            return null;
        }

        Vector3 spawnPosition = position;
        spawnPosition.z += spawnZOffset;

        GameObject vfxInstance = Instantiate(prefab, spawnPosition, Quaternion.identity);
        vfxInstance.name = $"{debugName}_Instance";

        ParticleSystem[] particleSystems = vfxInstance.GetComponentsInChildren<ParticleSystem>();

        float maxLifetime = 0f;

        if (particleSystems != null && particleSystems.Length > 0)
        {
            foreach (ParticleSystem ps in particleSystems)
            {
                var mainModule = ps.main;

                mainModule.loop = false;

                float psDuration = mainModule.duration + GetMaxStartLifetime(mainModule.startLifetime);
                if (psDuration > maxLifetime)
                {
                    maxLifetime = psDuration;
                }

                ps.Play();
            }
        }

        float finalLifetime = maxLifetime > 0f ? maxLifetime : defaultVFXLifetime;

        Destroy(vfxInstance, finalLifetime);

        return vfxInstance;
    }

    // Calculates the maximum start lifetime value from a MinMaxCurve.
    private float GetMaxStartLifetime(ParticleSystem.MinMaxCurve minMaxCurve)
    {
        switch (minMaxCurve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return minMaxCurve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return minMaxCurve.constantMax;
            case ParticleSystemCurveMode.Curve:
            case ParticleSystemCurveMode.TwoCurves:
                return minMaxCurve.curveMax != null && minMaxCurve.curveMax.length > 0 
                    ? minMaxCurve.curveMax.keys[minMaxCurve.curveMax.length - 1].time 
                    : 1.0f;
            default:
                return 1.0f;
        }
    }
}
