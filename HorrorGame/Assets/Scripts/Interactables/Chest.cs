using System.Collections;
using UnityEngine;

public enum ChestKind { Normal, Rare, Trapped }

// A chest: the first E opens it (rolls the GameConfig loot table unless contents were set); E again takes the item.
// Spawned by MazeSpawner at dead ends; one of them gets the key via SetContents. Opening swings the lid up around its hinge.
// Kinds (MazeSpawner picks them, weights in GameConfig) all look identical: Rare = good loot; Trapped = opening alerts monsters.
public class Chest : MonoBehaviour, IInteractable
{
    [SerializeField] AudioClip unlockClip;   // then, after a short delay, the lid opens
    [SerializeField] AudioClip openClip;
    [SerializeField] float openClipDelay = 0.6f;
    [SerializeField] AudioClip pickupClip;   // item handed to the player
    [SerializeField] AudioClip warningClip;   // trapped chest: the alarm that explains the monsters coming
    [Tooltip("Pivot on the lid's back edge; the lid mesh is its child.")]
    [SerializeField] Transform lidHinge;
    [SerializeField] float openAngle = -105f;
    [Tooltip("Degrees per second.")]
    [SerializeField] float openSpeed = 160f;

    [Header("Vanish when looted")]
    [Tooltip("Seconds after the lid is open and the contents are taken before the chest sinks away.")]
    [SerializeField] float vanishDelay = 1.5f;
    [SerializeField] float sinkSpeed = 0.8f;
    [SerializeField] float sinkDepth = 1f;

    // Camera shake (degrees, seconds) and trap noise.
    const float TrapShakeDegrees = 3f, TrapShakeSeconds = 0.7f;
    const float TrapNoiseRadius = 25f;   // monsters within this distance (m) hear a trapped chest open

    GameConfig config;
    ChestKind kind;
    bool opened;
    LootTable.Entry contents;

    public string GetPrompt(PlayerController player)
    {
        if (!opened) return "Open chest";
        return contents.item != null ? $"Take {contents.item.displayName}" : "Empty";
    }

    // Gives a spawned chest its kind: the guaranteed loot of a rare chest.
    public void Initialize(ChestKind kind, GameConfig config)
    {
        this.kind = kind;
        this.config = config;
        if (kind == ChestKind.Rare) contents = config.rareLoot.Roll();
    }

    public void SetContents(ItemData item, int count)
    {
        contents = new LootTable.Entry { item = item, count = count };
    }

    public void Interact(PlayerController player)
    {
        if (!opened)
        {
            Open();
            if (kind == ChestKind.Trapped) return;   // the trap message stays; E again takes the item
        }
        if (contents.item == null)
        {
            GameEvents.RaiseMessage("Nothing inside");
            return;
        }

        // The loot is taken exactly once: what does not fit is lost, and the chest closes either way (no second take, no chest left open).
        ItemData item = contents.item;
        int count = contents.count;
        contents = default;
        GetComponent<Collider>().enabled = false;
        if (player.Inventory.Add(item, count))
        {
            Fx.PlayAt(pickupClip, player.transform.position);
            GameEvents.RaiseMessage($"Found {item.displayName}");
        }
        else GameEvents.RaiseMessage($"Can't carry more {item.displayName}");
    }

    // Lid swings up, then the chest waits until it is looted (an empty chest at once), sinks into the floor and is removed.
    IEnumerator OpenRoutine()
    {
        yield return Fx.Tween(Mathf.Abs(openAngle) / openSpeed, t => lidHinge.localRotation = Quaternion.Euler(openAngle * t, 0f, 0f));

        yield return new WaitUntil(() => contents.item == null);
        GetComponent<Collider>().enabled = false;   // no longer interactable
        yield return new WaitForSeconds(vanishDelay);
        Vector3 start = transform.position;
        yield return Fx.Tween(sinkDepth / sinkSpeed, t => transform.position = start + Vector3.down * sinkDepth * t);
        Destroy(gameObject);
    }

    void Open()
    {
        opened = true;
        StartCoroutine(OpenRoutine());
        GameEvents.RaiseChestOpened();
        if (contents.item == null) contents = config.loot.Roll();
        Fx.PlayAt(unlockClip, transform.position);
        Fx.PlayAt(openClip, transform.position, openClipDelay);
        if (kind != ChestKind.Trapped) return;

        Fx.PlayAt(warningClip, transform.position);
        GameEvents.RaiseNoiseMade(transform.position, TrapNoiseRadius);
        GameEvents.RaiseCameraShake(TrapShakeDegrees, TrapShakeSeconds);
        GameEvents.RaiseMessage("A trap! The noise carries...");
    }
}
