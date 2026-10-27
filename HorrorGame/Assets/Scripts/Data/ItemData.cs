using UnityEngine;
using UnityEngine.Serialization;

public enum ItemType { Battery, Key, Meat, ShortcutKey, MapFragment }

// One kind of item (Battery, Key, Meat, ShortcutKey, MapFragment). Used by PlayerInventory, LootTable, Chest and HUD (icon tinted with color).
[CreateAssetMenu(menuName = "HorrorGame/Item", fileName = "Item")]
public class ItemData : ScriptableObject
{
    public string displayName;
    public ItemType type;
    [FormerlySerializedAs("maxStack")]
    [Tooltip("Most the player can carry of this item.")]
    public int maxCount = 1;
    [Tooltip("White hotbar icon (UI/Icons), tinted with Color.")]
    public Texture2D icon;
    public Color color = Color.white;
}
