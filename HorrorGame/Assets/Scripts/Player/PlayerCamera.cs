using UnityEngine;

// On the player camera. Applies the FOV setting, bobs the camera position while walking (more while sprinting), nothing when standing still or hidden;
// shakes it (GameEvents.CameraShake) and, when caught, snaps it toward the monster for the jump scare.
// Bob = position (set absolutely from the rest pose every frame), shake and jump scare = rotation, applied after PlayerController has set the look rotation.
public class PlayerCamera : MonoBehaviour
{
    // Camera bob height (m) and steps per second, walking / sprinting; BlendSpeed = how fast the bob fades in and out.
    const float AmplitudeWalk = 0.03f, AmplitudeSprint = 0.06f;
    const float FrequencyWalk = 2f, FrequencySprint = 3.1f;
    const float BlendSpeed = 6f;
    const float ScareHeight = 1.6f;
    const float Frequency = 40f;
    const float JumpScareShakeDegrees = 4f;

    [SerializeField] PlayerController player;

    Camera cam;
    Vector3 restPosition;
    float phase;     // bob steps
    float weight;    // 0-1 blend so the bob eases in and out
    float degrees;
    float seconds;
    float duration;
    Transform scareMonster;
    float scareEndTime;

    void Awake()
    {
        cam = GetComponent<Camera>();
        restPosition = transform.localPosition;
    }

    void OnEnable()
    {
        GameEvents.CameraShake += Shake;
        GameEvents.PlayerCaught += OnPlayerCaught;
    }

    void OnDisable()
    {
        GameEvents.CameraShake -= Shake;
        GameEvents.PlayerCaught -= OnPlayerCaught;
        scareMonster = null;
    }

    void Shake(float newDegrees, float newSeconds)
    {
        degrees = newDegrees;
        seconds = duration = newSeconds;
    }

    void OnPlayerCaught(Transform monster)
    {
        if (DebugCheats.GodMode) return;
        scareMonster = monster;
        scareEndTime = Time.time + GameManager.JumpScareSeconds;
        Shake(JumpScareShakeDegrees, GameManager.JumpScareSeconds);
    }

    void LateUpdate()
    {
        cam.fieldOfView = Settings.Fov;
        Bob();
        ApplyShake();
    }

    void Bob()
    {
        if (player.IsHidden) { weight = 0f; return; }   // the camera sits at the locker's hide point
        if (!Settings.HeadBob) { weight = 0f; transform.localPosition = restPosition; return; }

        bool moving = player.IsMoving;
        weight = Mathf.MoveTowards(weight, moving ? 1f : 0f, BlendSpeed * Time.deltaTime);

        bool sprint = player.IsSprinting;
        if (moving) phase += (sprint ? FrequencySprint : FrequencyWalk) * Time.deltaTime;
        float amplitude = (sprint ? AmplitudeSprint : AmplitudeWalk) * weight;

        // Vertical bob once per step (phase = steps), sideways sway once per two steps.
        Vector3 offset = new Vector3(Mathf.Sin(phase * Mathf.PI) * 0.5f, Mathf.Sin(phase * 2f * Mathf.PI), 0f) * amplitude;
        transform.localPosition = restPosition + offset;
    }

    void ApplyShake()
    {
        if (scareMonster != null)
        {
            if (Time.time >= scareEndTime) scareMonster = null;
            else transform.rotation = Quaternion.LookRotation(scareMonster.position + Vector3.up * ScareHeight - transform.position);
        }

        if (seconds <= 0f) return;
        seconds -= Time.deltaTime;
        float strength = degrees * Mathf.Clamp01(seconds / duration);   // fades out
        float t = Time.time * Frequency;
        transform.rotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f * strength,
                                               (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f * strength,
                                               (Mathf.PerlinNoise(t, t) - 0.5f) * 2f * strength);
    }
}
