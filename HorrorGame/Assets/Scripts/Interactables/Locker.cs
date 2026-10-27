using System.Collections;
using UnityEngine;

// A locker to hide in. The door stands open; E: the player steps in and it swings shut behind them, E again leaves.
// Leaving rips the door off: it falls flat onto the floor in front of the locker, and the locker is broken for good.
// While hidden, MonsterSenses ignores the player. Spawned by MazeSpawner; the door pivot sits on its hinge edge.
public class Locker : MonoBehaviour, IInteractable
{
    [Tooltip("Where the camera sits while hiding (inside the locker, facing out).")]
    [SerializeField] Transform hidePoint;
    [Tooltip("The door mesh (pivot on the hinge); open until someone hides inside.")]
    [SerializeField] Renderer door;
    [Tooltip("See-through material for the whole locker (body and door) while someone hides inside.")]
    [SerializeField] Material hiddenMaterial;
    [SerializeField] AudioClip closeClip;
    [SerializeField] AudioClip breakClip;
    [Header("Door motion")]
    [Tooltip("Hinge rotation (degrees around Y) of the open door; negative swings it out into the corridor.")]
    [SerializeField] float openAngle = -105f;
    [SerializeField] float swingSeconds = 0.3f;
    [Tooltip("Seconds the torn-off door takes to fall flat.")]
    [SerializeField] float fallSeconds = 0.5f;
    [Tooltip("Door position (locker space) when it lies on the floor; it falls forward from the closed pose.")]
    [SerializeField] Vector3 fallenPosition = new Vector3(-0.31f, 0.03f, 0.31f);
    [SerializeField] float fallenYawRange = 20f;
    [SerializeField] Color brokenTint = new Color(0.45f, 0.4f, 0.38f);

    public bool IsBroken { get; private set; }

    Vector3 closedPosition;
    Quaternion closedRotation;
    Renderer[] visuals;   // all turn see-through while someone hides inside
    Material[][] normalMaterials;   // per visual, restored when the door falls

    void Awake()
    {
        closedPosition = door.transform.localPosition;
        closedRotation = door.transform.localRotation;
        visuals = GetComponentsInChildren<Renderer>();
        normalMaterials = System.Array.ConvertAll(visuals, r => r.sharedMaterials);
        door.transform.localRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public void SetOccupied(bool occupied)
    {
        StopAllCoroutines();
        if (occupied) StartCoroutine(EnterRoutine());
        else if (!IsBroken)
        {
            IsBroken = true;
            StartCoroutine(FallRoutine());
        }
    }

    IEnumerator EnterRoutine()
    {
        yield return Fx.Tween(swingSeconds, t => door.transform.localRotation = closedRotation * Quaternion.Euler(0f, Mathf.Lerp(openAngle, 0f, Mathf.SmoothStep(0f, 1f, t)), 0f));
        Fx.PlayAt(closeClip, door.transform.position);
        SetVisible(false);
    }

    IEnumerator FallRoutine()
    {
        SetVisible(true);
        door.material.color *= brokenTint;   // .material = this door's own copy
        Fx.PlayAt(breakClip, door.transform.position);
        Quaternion start = door.transform.localRotation;
        Quaternion end = Quaternion.Euler(90f, Random.Range(-fallenYawRange, fallenYawRange), 0f);
        yield return Fx.Tween(fallSeconds, t =>
        {
            float eased = t * t;   // gravity: slow at first, fast at the end
            door.transform.SetLocalPositionAndRotation(Vector3.Lerp(closedPosition, fallenPosition, eased), Quaternion.Slerp(start, end, eased));
        });
    }

    void SetVisible(bool visible)
    {
        for (int i = 0; i < visuals.Length; i++)
        {
            visuals[i].sharedMaterials = visible ? normalMaterials[i] : System.Array.ConvertAll(normalMaterials[i], _ => hiddenMaterial);
        }
    }

    public string GetPrompt(PlayerController player) =>
        player.Locker == this ? "Leave" : "Hide";

    public void Interact(PlayerController player)
    {
        if (player.Locker == this) player.Leave();
        else player.Hide(this, hidePoint);
    }
}
