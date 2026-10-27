using UnityEngine;
using UnityEngine.InputSystem;

// The player's flashlight: F toggles it, the battery drains while it is on and it flickers when low.
// Reads PlayerController.IsHidden (off while hiding); used by PlayerInventory (recharge), MonsterSenses (light = beacon) and HUD.
public class Flashlight : MonoBehaviour
{
    public const float FlickerThreshold = 0.2f;   // below this charge (0-1) the light flickers (HUD shows the battery as low)
    const float BlackoutThreshold = 0.1f;         // below this it also blacks out now and then
    const float MinBlackoutInterval = 3f, MaxBlackoutInterval = 8f;   // seconds between blackouts
    const float MinBlackoutDuration = 0.5f, MaxBlackoutDuration = 1.5f;

    [SerializeField] PlayerConfig config;
    [SerializeField] PlayerController player;
    [SerializeField] Light spot;
    [SerializeField] InputActionReference toggle;
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip onClip;
    [SerializeField] AudioClip offClip;
    [SerializeField] AudioClip warningClip;   // once when the battery drops below the flicker threshold
    [SerializeField, Range(0f, 1f)] float warningVolume = 0.5f;

    float baseIntensity;
    float blackoutLeft;    // seconds the light stays fully dark
    float nextBlackoutIn;  // seconds until the next one
    bool warned;           // the low-battery warning played; re-armed once recharged above the threshold
    bool restoreOnLeave;   // was on when the player hid

    public bool IsOn { get; private set; }
    public bool IsLit => IsOn && blackoutLeft <= 0f;   // on and not blacked out (what monsters can see)
    public float Charge { get; private set; } = 1f;   // 0-1
    public bool IsFull => Charge >= 1f;

    void Awake()
    {
        baseIntensity = spot.intensity;
        SetOn(true);
    }

    void OnEnable()
    {
        toggle.action.Enable();
    }

    void OnDisable()
    {
        toggle.action.Disable();
    }

    void Update()
    {
        if (player.IsHidden)   // forced off in a locker: no toggle, no drain; back on after leaving
        {
            if (IsOn) { restoreOnLeave = true; SetOn(false); }
            return;
        }
        if (restoreOnLeave)
        {
            restoreOnLeave = false;
            if (Charge > 0f) SetOn(true);
        }
        if (!player.Frozen && toggle.action.WasPressedThisFrame()) Toggle();
        if (IsOn) DrainBattery();
    }

    public void Recharge()
    {
        Charge = 1f;
        blackoutLeft = 0f;
        spot.enabled = IsOn;
    }

    // True when the beam reaches the point: lit, within range and inside the cone (walls are checked by the caller).
    public bool Lights(Vector3 point, float range)
    {
        Vector3 offset = point - spot.transform.position;
        return IsLit && offset.magnitude <= range && Vector3.Angle(spot.transform.forward, offset) <= spot.spotAngle / 2f;
    }

    void Toggle()
    {
        if (!IsOn && Charge <= 0f) return;   // an empty battery can't be switched on
        SetOn(!IsOn);
        audioSource.PlayOneShot(IsOn ? onClip : offClip);
    }

    void DrainBattery()
    {
        if (!DebugCheats.GodMode) Charge = Mathf.Max(0f, Charge - config.flashlightDrainPerSecond * Time.deltaTime);
        if (Charge <= 0f)
        {
            SetOn(false);
            return;
        }
        WarnWhenLow();
        UpdateBlackout();
        spot.intensity = baseIntensity * Flicker();
    }

    void WarnWhenLow()
    {
        if (Charge >= FlickerThreshold) warned = false;
        else if (!warned)
        {
            warned = true;
            audioSource.PlayOneShot(warningClip, warningVolume);
        }
    }

    // A very low battery also kills the light completely for a moment, at random intervals.
    void UpdateBlackout()
    {
        if (Charge >= BlackoutThreshold)
        {
            blackoutLeft = 0f;
            nextBlackoutIn = 0f;
        }
        else if (blackoutLeft > 0f) blackoutLeft -= Time.deltaTime;
        else
        {
            if (nextBlackoutIn <= 0f) nextBlackoutIn = Random.Range(MinBlackoutInterval, MaxBlackoutInterval);
            nextBlackoutIn -= Time.deltaTime;
            if (nextBlackoutIn <= 0f) blackoutLeft = Random.Range(MinBlackoutDuration, MaxBlackoutDuration);
        }
        spot.enabled = blackoutLeft <= 0f;
    }

    void SetOn(bool on)
    {
        IsOn = on;
        blackoutLeft = 0f;
        spot.enabled = on;
    }

    // Normal brightness, except in short random dips when the battery is low.
    float Flicker()
    {
        if (Charge >= FlickerThreshold || Random.value > 0.15f) return 1f;
        return Random.Range(0.1f, 0.5f);
    }
}
