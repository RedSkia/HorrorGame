using UnityEngine;

// Thrown meat (Rigidbody), bait for monsters. The first impact is a wet thud that monsters in range run toward
// (repeated every second while unclaimed); the first one to arrive claims it and eats it (MonsterAI Eat state), then it is destroyed.
// Spawned by PlayerInventory, which sets the noise radius and destroys it after a while.
public class Meat : MonoBehaviour
{
    [SerializeField] AudioClip splatClip;

    const float PulseSeconds = 1f;

    float noiseRadius;
    float pulseTimer;
    bool landed;
    bool claimed;

    // Meat does not follow the player into the next level.
    void OnEnable() => GameEvents.LevelStarted += OnLevelStarted;

    void OnDisable() => GameEvents.LevelStarted -= OnLevelStarted;

    void OnLevelStarted(int level, int best) => Destroy(gameObject);

    // Unclaimed meat keeps calling, so monsters that come near later (or were busy) also find it.
    void Update()
    {
        if (!landed || claimed) return;
        pulseTimer -= Time.deltaTime;
        if (pulseTimer > 0f) return;
        pulseTimer = PulseSeconds;
        GameEvents.RaiseMeatLanded(this, noiseRadius);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (landed) return;
        landed = true;
        pulseTimer = PulseSeconds;
        Fx.PlayAt(splatClip, transform.position);
        GameEvents.RaiseMeatLanded(this, noiseRadius);
    }

    public void Initialize(float noiseRadius) => this.noiseRadius = noiseRadius;

    // True for the first monster only; the others fall back to investigating.
    public bool TryClaim()
    {
        if (claimed) return false;
        claimed = true;
        return true;
    }
}
