using UnityEngine;
using UnityEngine.InputSystem;

// First-person walking, mouse look, sprint with stamina, footsteps, and hiding in a locker.
// Frozen (set by GameManager) blocks looking, walking, interacting, flashlight toggle and item use.
// Used by Interactor and interactables (Inventory, Locker, Leave), MonsterSenses (IsHidden/IsSprinting), PlayerCamera (IsMoving), HUD (stamina).
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    const float DegreesPerMouseCount = 0.022f;   // Source-engine yaw: degrees per mouse count at sensitivity 1 (so the setting reads like in CS-style games)
    const float MaxMouseCountsPerFrame = 300f;   // larger per-frame deltas are browser/editor spikes, not hand movement
    const int SkipFramesAfterLock = 2;           // the browser sends a bogus delta when pointer lock is granted
    const float StickLookSpeed = 150f;           // degrees per second at full gamepad stick (at the default sensitivity)
    const float Gravity = -20f;
    const float MaxPitch = 85f;
    const float HiddenMaxYaw = 70f;       // degrees the player can turn left/right from the locker's facing while hiding
    const float HiddenAmbientBoost = 8f;  // ambient light multiplier while hiding (flashlight is off, the view out stays readable)
    const float StaminaResumeThreshold = 0.2f;   // after running out, sprint stays blocked until stamina is above this
    const float StepIntervalWalk = 0.5f, StepIntervalSprint = 0.32f;   // seconds between footsteps
    const float StepVolume = 0.6f;
    const float BreathStartStamina = 0.4f;   // below this stamina the heavy breathing fades in (full volume at 0)
    const float BreathFadeSpeed = 2f;        // volume change per second

    [SerializeField] PlayerConfig config;
    [SerializeField] Transform cameraTransform;
    [SerializeField] PlayerInventory inventory;
    [Tooltip("Own visuals (held flashlight, body) hidden while in a locker.")]
    [SerializeField] Renderer[] ownVisuals;
    [SerializeField] InputActionReference move;
    [SerializeField] InputActionReference look;
    [SerializeField] InputActionReference sprint;
    [SerializeField] AudioSource footstepSource;
    [SerializeField] AudioClip[] footstepClips;
    [Tooltip("2D looping heavy breathing; volume follows how exhausted the player is.")]
    [SerializeField] AudioSource breathSource;

    CharacterController controller;
    float pitch;                // camera up/down angle
    float verticalVelocity;
    bool exhausted;             // stamina ran out; sprint is blocked until it recovers a bit
    Vector3 cameraLocalPosition;
    Quaternion cameraLocalRotation;
    float hideYaw;              // camera yaw while hidden (world degrees)
    float hideCenterYaw;        // the locker's facing
    float pitchBeforeHide;      // restored on Leave
    Color normalAmbient;        // the scene's ambient light; ApplyAmbient brightens it while hiding
    float stepTimer;
    int lastStep = -1;
    int framesToSkip;           // mouse frames still ignored after the cursor got locked

    public bool IsSprinting { get; private set; }
    public float Stamina01 { get; private set; } = 1f;   // 0-1
    public Locker Locker { get; private set; }            // the locker we are hiding in, or null
    public bool IsHidden => Locker != null;
    public bool Frozen { get; set; }                       // standing still and unable to act (map open, caught, escaping); GameManager sets it
    public PlayerInventory Inventory => inventory;

    // Walking or sprinting on the ground (drives footsteps and the camera bob).
    public bool IsMoving
    {
        get
        {
            Vector3 velocity = controller.velocity;
            velocity.y = 0f;
            return controller.isGrounded && velocity.magnitude > 0.1f;
        }
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        normalAmbient = RenderSettings.ambientLight;
    }

    void OnEnable()
    {
        move.action.Enable();
        look.action.Enable();
        sprint.action.Enable();
        framesToSkip = SkipFramesAfterLock;   // resuming from pause re-locks the cursor
        GameEvents.CheatsChanged += ApplyAmbient;
        ApplyAmbient();   // fullbright may have been switched while the player was inactive (paused)
    }

    void OnDisable()
    {
        move.action.Disable();
        look.action.Disable();
        sprint.action.Disable();
        GameEvents.CheatsChanged -= ApplyAmbient;
    }

    // Ambient = the scene's, brighter while hiding; fullbright (DebugCheats) overrides both, so they work in any order.
    void ApplyAmbient()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = DebugCheats.Fullbright ? DebugCheats.FullbrightAmbient : normalAmbient * (IsHidden ? HiddenAmbientBoost : 1f);
    }

    void Update()
    {
        if (Frozen)
        {
            IsSprinting = false;
            controller.Move(Vector3.zero);   // clears the velocity, so footsteps and the bob stop
            if (!IsHidden) cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);   // rest pose each frame, so a camera shake cannot add up
        }
        else if (IsHidden)   // hidden: look around, no walking, stamina recovers
        {
            controller.Move(Vector3.zero);   // clears the running velocity, so no footsteps while hidden
            LookFromLocker();
            UpdateStamina();
        }
        else
        {
            Look();
            Move();
        }
        UpdateFootsteps();
        UpdateBreathing();
    }

    // The camera jumps to the locker's hide point; Leave() puts it back.
    public void Hide(Locker locker, Transform hidePoint)
    {
        if (IsHidden) return;
        Locker = locker;
        locker.SetOccupied(true);
        ApplyAmbient();
        IsSprinting = false;
        SetOwnVisuals(false);
        cameraLocalPosition = cameraTransform.localPosition;
        cameraLocalRotation = cameraTransform.localRotation;
        pitchBeforeHide = pitch;
        cameraTransform.SetPositionAndRotation(hidePoint.position, hidePoint.rotation);
        hideCenterYaw = hideYaw = hidePoint.eulerAngles.y;
        pitch = 0f;
    }

    public void Leave()
    {
        if (!IsHidden) return;
        Locker.SetOccupied(false);
        Locker = null;
        ApplyAmbient();
        SetOwnVisuals(true);
        cameraTransform.localPosition = cameraLocalPosition;
        cameraTransform.localRotation = cameraLocalRotation;
        pitch = pitchBeforeHide;   // otherwise the next Look would snap to the pitch used inside the locker
    }

    void SetOwnVisuals(bool visible)
    {
        foreach (var visual in ownVisuals) visual.enabled = visible;
    }

    // Hidden: only the camera turns (limited around the locker's facing); the body stays put.
    void LookFromLocker()
    {
        Vector2 delta = LookDelta();
        hideYaw = Mathf.Clamp(hideYaw + delta.x, hideCenterYaw - HiddenMaxYaw, hideCenterYaw + HiddenMaxYaw);
        pitch = Mathf.Clamp(pitch - delta.y, -MaxPitch, MaxPitch);
        cameraTransform.rotation = Quaternion.Euler(pitch, hideYaw, 0f);
    }

    // Degrees to turn this frame. Mouse: raw counts * a fixed angle, never * deltaTime (frame-rate independent), no smoothing or acceleration.
    // The mouse is ignored while the cursor is not locked (the browser reports plain cursor movement then) and for a few frames after it locks.
    // Gamepad stick: a rate.
    Vector2 LookDelta()
    {
        Vector2 input = look.action.ReadValue<Vector2>();
        if (look.action.activeControl?.device is Gamepad)
            return input * (StickLookSpeed * Time.deltaTime * Settings.Sensitivity / Settings.DefaultSensitivity);

        if (Cursor.lockState != CursorLockMode.Locked) framesToSkip = SkipFramesAfterLock;
        if (framesToSkip > 0)
        {
            framesToSkip--;
            return Vector2.zero;
        }
        return Vector2.ClampMagnitude(input, MaxMouseCountsPerFrame) * (DegreesPerMouseCount * Settings.Sensitivity);
    }

    void Look()
    {
        Vector2 delta = LookDelta();
        transform.Rotate(0f, delta.x, 0f);   // turn the body left/right
        pitch = Mathf.Clamp(pitch - delta.y, -MaxPitch, MaxPitch);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);   // tilt the camera up/down
    }

    void Move()
    {
        Vector2 input = move.action.ReadValue<Vector2>();
        IsSprinting = sprint.action.IsPressed() && input != Vector2.zero && !exhausted;
        UpdateStamina();

        float speed = IsSprinting ? config.sprintSpeed : config.walkSpeed;
        Vector3 velocity = (transform.right * input.x + transform.forward * input.y) * speed;

        // Small downward push keeps isGrounded reliable on the floor.
        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += Gravity * Time.deltaTime;
        velocity.y = verticalVelocity;

        controller.Move(velocity * Time.deltaTime);
    }

    void UpdateStamina()
    {
        float rate = IsSprinting ? -config.staminaDrainPerSecond : config.staminaRegenPerSecond;
        Stamina01 = Mathf.Clamp01(Stamina01 + rate * Time.deltaTime);
        if (DebugCheats.GodMode) Stamina01 = 1f;

        if (Stamina01 <= 0f) exhausted = true;
        else if (Stamina01 >= StaminaResumeThreshold) exhausted = false;
    }

    // Heavy breathing fades in as stamina runs low and out as it recovers.
    void UpdateBreathing()
    {
        float target = Mathf.Clamp01(1f - Stamina01 / BreathStartStamina);
        breathSource.volume = Mathf.MoveTowards(breathSource.volume, target, BreathFadeSpeed * Time.deltaTime);
        if (breathSource.volume > 0f && !breathSource.isPlaying) breathSource.Play();
        else if (breathSource.volume <= 0f && breathSource.isPlaying) breathSource.Pause();
    }

    // A random footstep on a timer while walking (faster when sprinting).
    void UpdateFootsteps()
    {
        if (!IsMoving)
        {
            stepTimer = 0f;
            return;
        }
        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f) return;

        stepTimer = IsSprinting ? StepIntervalSprint : StepIntervalWalk;
        int index = Random.Range(0, footstepClips.Length);
        if (index == lastStep) index = (index + 1) % footstepClips.Length;
        lastStep = index;
        footstepSource.pitch = Random.Range(0.95f, 1.05f);
        footstepSource.PlayOneShot(footstepClips[index], StepVolume * (IsSprinting ? 1f : 0.8f));
    }
}
