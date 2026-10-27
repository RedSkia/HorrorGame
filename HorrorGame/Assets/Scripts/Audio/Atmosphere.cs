using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The heartbeat, on the "Heartbeat" scene object; reads the monster list from MazeSpawner.
// Speeds up when a monster is near, and raises the screen vignette with it.
public class Atmosphere : MonoBehaviour
{
    const float MaxDistance = 25f;        // monsters further away are silent
    const float CurvePower = 3f;          // closeness^power: the heartbeat only builds up in the last metres
    const float MinVolume = 0.2f;         // volume just inside the range, rising to 1 next to the player
    const float SlowInterval = 1.2f, FastInterval = 0.25f;   // seconds between beats far / near
    const float VignetteFar = 0.35f, VignetteNear = 0.8f;    // screen vignette when no monster is near / next to the player

    [SerializeField] MazeSpawner spawner;
    [SerializeField] Transform player;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip heartbeat;
    [SerializeField] Volume volume;

    float timer;
    Vignette vignette;

    void Start() => volume.profile.TryGet(out vignette);

    void Update()
    {
        float t = Mathf.Pow(1f - Mathf.Clamp01(Nearest(player.position) / MaxDistance), CurvePower);
        if (vignette != null) vignette.intensity.value = Mathf.Lerp(VignetteFar, VignetteNear, t);

        if (t <= 0f)
        {
            timer = 0f;
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;

        timer = Mathf.Lerp(SlowInterval, FastInterval, t);
        audioSource.PlayOneShot(heartbeat, Mathf.Lerp(MinVolume, 1f, t));
    }

    // Distance from the point to the closest monster.
    float Nearest(Vector3 point)
    {
        float nearest = float.MaxValue;
        foreach (var monster in spawner.Monsters)
            nearest = Mathf.Min(nearest, Vector3.Distance(monster.transform.position, point));
        return nearest;
    }
}
