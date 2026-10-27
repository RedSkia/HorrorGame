using System.Collections;
using UnityEngine;
using UnityEngine.AI;

// A locked door; hold E with the matching key to unlock. Spawned by MazeSpawner.
// Exit (isExit): needs the Key; the gate swings open and shows a dark doorway, and walking into it (the trigger collider on this
// object) ends the level (GameManager listens for Escaped). Once the player holds the key it hums (looping 3D sound), so you can find it by ear.
// Shortcut: needs a Shortcut key and opens a carved shortcut for good. While closed it carves the NavMesh, so monsters cannot use it either.
public class Door : MonoBehaviour, IInteractable
{
    [SerializeField] bool isExit;
    [SerializeField] PlayerConfig config;    // unlockHoldSeconds
    [SerializeField] AudioClip unlockClip;   // key turns and the door opens, one sound

    [Header("Exit")]
    [Tooltip("The barred gate; swings open around its left edge when unlocked.")]
    [SerializeField] Transform gate;
    [Tooltip("Black doorway behind the gate, shown once it is open.")]
    [SerializeField] GameObject doorway;
    [Tooltip("The bars' blocking/interaction collider; removed once open.")]
    [SerializeField] Collider gateCollider;
    [Tooltip("Walk-through zone in front of the doorway; enabled once open.")]
    [SerializeField] Collider exitTrigger;
    [SerializeField] float openAngle = 100f;
    [Tooltip("Degrees per second.")]
    [SerializeField] float openSpeed = 200f;
    [Tooltip("Looping 3D hum; starts when the player has the key.")]
    [SerializeField] AudioSource hum;

    [Header("Shortcut")]
    [SerializeField] NavMeshObstacle obstacle;

    PlayerInventory playerInventory;   // exit: listened to for the key hum
    bool escaped;

    ItemType KeyType => isExit ? ItemType.Key : ItemType.ShortcutKey;

    public void Initialize(PlayerInventory inventory)
    {
        playerInventory = inventory;
        playerInventory.Changed += RefreshHum;
        RefreshHum();
    }

    void OnDisable()
    {
        if (playerInventory != null) playerInventory.Changed -= RefreshHum;
    }

    void RefreshHum()
    {
        bool hasKey = playerInventory.Has(ItemType.Key);
        if (hasKey && !hum.isPlaying) hum.Play();
        else if (!hasKey) hum.Stop();
    }

    public string GetPrompt(PlayerController player)
    {
        if (player.Inventory.Has(KeyType)) return isExit ? "Hold to unlock" : "Unlock shortcut";
        return isExit ? "Locked — find the key" : "Locked — needs a shortcut key";
    }

    public float GetHoldSeconds(PlayerController player) => player.Inventory.Has(KeyType) ? config.unlockHoldSeconds : 0f;

    public void Interact(PlayerController player)
    {
        if (!player.Inventory.Has(KeyType))
        {
            GameEvents.RaiseMessage(GetPrompt(player));
            return;
        }
        Fx.PlayAt(unlockClip, transform.position);
        if (isExit)
        {
            gateCollider.enabled = false;
            StartCoroutine(OpenGate());
            return;
        }
        player.Inventory.Remove(ItemType.ShortcutKey, 1);
        obstacle.enabled = false;   // the NavMesh opens up again
        GameEvents.RaiseMessage("Shortcut opened");
        gameObject.SetActive(false);   // not Destroy: MazeSpawner still holds it and removes it on the next level
    }

    IEnumerator OpenGate()
    {
        doorway.SetActive(true);
        yield return Fx.Tween(openAngle / openSpeed, t => gate.localRotation = Quaternion.Euler(0f, -openAngle * t, 0f));
        exitTrigger.enabled = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (escaped || !other.TryGetComponent(out PlayerController _)) return;
        escaped = true;
        GameEvents.RaiseEscaped();
    }
}
