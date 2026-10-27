using UnityEngine;

// What a monster can see/hear: sight (shorter without the flashlight), flashlight glow, sprint noise, hidden player.
// Used by MonsterAI (same GameObject).
public class MonsterSenses : MonoBehaviour
{
    const float LightFleeRange = 12f;   // a light-shy monster flees when the beam hits it within this range

    [SerializeField] float eyeHeight = 1.6f;

    MonsterConfig config;   // assigned per monster type by MonsterAI.Initialize
    Transform player;
    Transform playerCamera;
    Flashlight flashlight;
    PlayerController playerController;
    bool sawPlayer;   // seen in the last frame before the player hid

    public Vector3 PlayerPosition => player.position;
    public float DistanceToPlayer => Vector3.Distance(transform.position, player.position);
    public bool PlayerHidden => playerController.IsHidden;
    public bool SawPlayerHide => playerController.IsHidden && sawPlayer;   // it saw the player step into the locker

    Vector3 Eye => transform.position + Vector3.up * eyeHeight;

    public void Initialize(MonsterConfig config, PlayerController playerController, Flashlight flashlight)
    {
        this.config = config;
        this.playerController = playerController;
        this.flashlight = flashlight;
        player = playerController.transform;
        playerCamera = Camera.main.transform;
    }

    // While the player is visible this tracks whether the monster sees them; the value freezes the moment they hide.
    void Update()
    {
        if (!playerController.IsHidden) sawPlayer = SeesPlayer();
    }

    public bool CanSeePlayer()
    {
        return !playerController.IsHidden && SeesPlayer();
    }

    public bool SensesGlow()
    {
        return !playerController.IsHidden && flashlight.IsLit && DistanceToEye() <= config.glowRange && !HasLineOfSight();
    }

    public bool HearsPlayer()
    {
        return !playerController.IsHidden && playerController.IsSprinting && DistanceToPlayer <= config.noiseRange;
    }

    // The beam hits this monster within range and no wall is in between.
    public bool LitByFlashlight()
    {
        Vector3 body = transform.position + Vector3.up * (eyeHeight / 2f);
        if (!flashlight.Lights(body, LightFleeRange)) return false;
        Vector3 direction = body - playerCamera.position;
        return !Physics.Raycast(playerCamera.position, direction, direction.magnitude - 0.5f, ~0, QueryTriggerInteraction.Ignore);
    }

    bool SeesPlayer()
    {
        float range = Mathf.Max(config.touchRange, flashlight.IsLit ? config.sightRangeLit : config.sightRangeDark);
        return DistanceToEye() <= range && HasLineOfSight();
    }

    float DistanceToEye() => Vector3.Distance(Eye, playerCamera.position);

    bool HasLineOfSight()
    {
        Vector3 direction = playerCamera.position - Eye;
        return Physics.Raycast(Eye, direction, out RaycastHit hit, direction.magnitude, ~0, QueryTriggerInteraction.Ignore)
            && hit.transform == player;
    }
}
