using UnityEngine;
using UnityEngine.InputSystem;

// Looks at what is in front of the camera and presses E on it (chest, door, locker, pickup).
// The HUD reads Current to show the "[E] ..." prompt; held interactions (doors) raise GameEvents.InteractProgress.
public class Interactor : MonoBehaviour
{
    const float InteractRange = 2.5f;

    [SerializeField] Transform cameraTransform;
    [SerializeField] PlayerController playerController;
    [SerializeField] InputActionReference interact;

    // The thing we are looking at within interactRange, or null.
    // While hiding, the camera is inside the locker, so the locker is the target ("Leave").
    public IInteractable Current { get; private set; }

    float held;       // seconds E has been held on Current
    float progress;   // last value sent to the HUD (0-1)

    void OnEnable() => interact.action.Enable();

    void OnDisable()
    {
        interact.action.Disable();
        SetProgress(0f);
    }

    void Update()
    {
        if (playerController.Frozen)
        {
            Current = null;
            SetProgress(0f);
            return;
        }
        IInteractable target = playerController.IsHidden ? playerController.Locker : FindTarget();
        if (target != Current) held = 0f;
        Current = target;
        if (Current == null)
        {
            SetProgress(0f);
            return;
        }

        float seconds = Current.GetHoldSeconds(playerController);
        if (seconds <= 0f)
        {
            if (interact.action.WasPressedThisFrame()) Current.Interact(playerController);
            return;
        }
        // Held interaction: the bar fills while E is down; releasing or looking away resets it.
        held = interact.action.IsPressed() ? held + Time.deltaTime : 0f;
        SetProgress(held / seconds);
        if (held < seconds) return;
        held = 0f;
        Current.Interact(playerController);
    }

    void SetProgress(float value)
    {
        if (value == progress) return;
        progress = value;
        GameEvents.RaiseInteractProgress(value);
    }

    IInteractable FindTarget()
    {
        bool hit = Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit info,
            InteractRange, ~0, QueryTriggerInteraction.Ignore);
        var target = hit ? info.collider.GetComponentInParent<IInteractable>() : null;
        return target is Locker { IsBroken: true } ? null : target;   // a broken locker cannot be used
    }
}

// Anything the player can press E on. Interactor finds it with a raycast; the HUD shows GetPrompt.
public interface IInteractable
{
    string GetPrompt(PlayerController player);

    // Seconds E must be held before Interact runs (0 = a press). Interactor cancels when released or looking away.
    float GetHoldSeconds(PlayerController player) => 0f;

    void Interact(PlayerController player);
}
