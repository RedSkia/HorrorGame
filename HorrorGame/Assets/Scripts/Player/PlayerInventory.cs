using System;
using UnityEngine;
using UnityEngine.InputSystem;

// The hotbar: one fixed slot per item type (hotbarItems, in order), each holding a count up to the item's maxCount.
// Select with 1-4, use with R (battery recharges, meat is thrown from the camera); keys do nothing when used.
// Used by Interactor (passed to chests/door/lockers), GameManager (removes keys, rewards) and HUD.
public class PlayerInventory : MonoBehaviour
{
    const float ThrowSpawnDistance = 0.6f;
    const float ThrowForce = 9f;      // m/s the meat leaves the camera with
    const float MeatLifetime = 60f;   // seconds until a thrown meat disappears

    [Serializable]
    public struct Stack
    {
        public ItemData item;
        public int count;
    }

    [SerializeField] Flashlight flashlight;
    [SerializeField] PlayerController player;
    [SerializeField] PlayerConfig config;
    [SerializeField] Transform cameraTransform;
    [SerializeField] Rigidbody meatPrefab;
    [SerializeField] AudioClip throwClip;
    [Tooltip("One entry per slot, in hotbar order. Map fragments take no slot.")]
    [SerializeField] ItemData[] hotbarItems;
    [SerializeField] InputActionReference[] slotSelect;
    [SerializeField] InputActionReference use;
    [SerializeField] Stack[] startingItems;

    int[] counts;   // created on first use (the HUD can read it before Start)

    public ItemData[] Items => hotbarItems;
    public int SelectedSlot { get; private set; }
    public event Action Changed;

    int[] Counts => counts ??= new int[hotbarItems.Length];

    public int Count(int slot) => Counts[slot];

    void OnEnable()
    {
        foreach (var action in slotSelect) action.action.Enable();
        use.action.Enable();
    }

    void OnDisable()
    {
        foreach (var action in slotSelect) action.action.Disable();
        use.action.Disable();
    }

    void Start()
    {
        foreach (var s in startingItems) Add(s.item, s.count);
    }

    void Update()
    {
        if (player.Frozen) return;
        for (int i = 0; i < slotSelect.Length; i++)
        {
            if (slotSelect[i].action.WasPressedThisFrame()) Select(i);
        }
        if (use.action.WasPressedThisFrame()) UseSelected();
    }

    // Adds up to the item's cap. Returns false when that slot is already full (the item stays where it was).
    // A map fragment takes no slot: it reveals map at once.
    public bool Add(ItemData item, int count)
    {
        if (item.type == ItemType.MapFragment)
        {
            for (int i = 0; i < count; i++) GameEvents.RaiseMapFragmentFound();
            return true;
        }
        int slot = Array.IndexOf(hotbarItems, item);
        if (Counts[slot] >= item.maxCount) return false;
        Counts[slot] = Mathf.Min(item.maxCount, Counts[slot] + count);
        Changed?.Invoke();
        return true;
    }

    public bool HasRoom(ItemData item) => Counts[Array.IndexOf(hotbarItems, item)] < item.maxCount;

    public bool Has(ItemType type) => Counts[SlotOf(type)] > 0;

    public void Remove(ItemType type, int count)
    {
        int slot = SlotOf(type);
        Counts[slot] = Mathf.Max(0, Counts[slot] - count);
        Changed?.Invoke();
    }

    int SlotOf(ItemType type) => Array.FindIndex(hotbarItems, item => item.type == type);

    void Select(int index)
    {
        SelectedSlot = index;
        Changed?.Invoke();
    }

    // Selecting an empty slot is fine; using it does nothing.
    public void UseSelected()
    {
        ItemType type = hotbarItems[SelectedSlot].type;
        if (Counts[SelectedSlot] == 0) return;
        if (type == ItemType.Battery && !flashlight.IsFull)
        {
            flashlight.Recharge();
            Remove(type, 1);
        }
        else if (type == ItemType.Meat)
        {
            ThrowMeat();
            Remove(type, 1);
        }
    }

    void ThrowMeat()
    {
        Rigidbody meat = Instantiate(meatPrefab, cameraTransform.position + cameraTransform.forward * ThrowSpawnDistance, UnityEngine.Random.rotation);
        meat.linearVelocity = cameraTransform.forward * ThrowForce;
        meat.GetComponent<Meat>().Initialize(config.meatNoiseRadius);
        Destroy(meat.gameObject, MeatLifetime);
        Fx.PlayAt(throwClip, cameraTransform.position);
        GameEvents.RaiseMeatThrown();
    }
}
