using UnityEngine;

// Maze size, progression and placement numbers (asset: Settings/GameConfig). Used by GameManager, MazeSpawner, Chest, HUD (also chest loot).
[CreateAssetMenu(menuName = "HorrorGame/Game Config", fileName = "GameConfig")]
public class GameConfig : ScriptableObject
{
    [Header("Maze")]
    public int startMazeSize = 6;
    [Tooltip("One 2x2 room per this many cells of maze size (clamped to 1..Max Rooms).")]
    public int cellsPerRoom = 3;
    public int maxRooms = 3;
    [Tooltip("Chance (0-1) that a dead end gets an extra opening, creating loops.")]
    [Range(0f, 1f)] public float braidChance = 0.3f;

    [Header("Progression")]
    public int maxLives = 3;
    [Tooltip("Monsters on level 1; +1 every Levels Per Extra Monster levels, capped by Max Monsters.")]
    public int monsterCount = 1;
    public int levelsPerExtraMonster = 3;
    public int maxMonsters = 4;
    [Tooltip("Minimum maze distance (cells) from the start.")]
    public int minMonsterDistance = 4;

    [Header("Monster types")]
    [Tooltip("Types a monster can be; each is picked by weight once the level reaches Min Level.")]
    public MonsterSpawn[] monsterTypes;

    [Header("Placement")]
    [Tooltip("Chests, lockers and shortcut doors scale with the maze: cells x per-cell density, rounded, clamped to min..max (chests include the key chest).")]
    public float chestsPerCell = 0.1f;
    public int minChests = 6;
    public int maxChests = 24;
    public float lockersPerCell = 0.06f;
    public int minLockers = 4;
    public int maxLockers = 16;
    public float shortcutsPerCell = 0.017f;
    public int minShortcuts = 1;
    public int maxShortcuts = 5;

    [Header("Chest types")]
    [Tooltip("Relative chances of each chest type (the key chest is always Normal).")]
    public float normalChestWeight = 10f;
    public float rareChestWeight = 1.5f;
    public float trappedChestWeight = 2f;
    [Tooltip("What a normal chest holds.")]
    public LootTable loot;
    [Tooltip("What a rare (gold) chest holds.")]
    public LootTable rareLoot;

    void OnValidate()
    {
        if (loot != null && !Mathf.Approximately(loot.Total(), 100f)) Debug.LogWarning($"Loot percentages add up to {loot.Total()}, not 100.", this);
        if (rareLoot != null && !Mathf.Approximately(rareLoot.Total(), 100f)) Debug.LogWarning($"Rare loot percentages add up to {rareLoot.Total()}, not 100.", this);
    }

    public int ChestCount(int cells) { return Scaled(cells, chestsPerCell, minChests, maxChests); }
    public int LockerCount(int cells) { return Scaled(cells, lockersPerCell, minLockers, maxLockers); }
    public int ShortcutCount(int cells) { return Scaled(cells, shortcutsPerCell, minShortcuts, maxShortcuts); }

    static int Scaled(int cells, float perCell, int min, int max)
    {
        return Mathf.Clamp(Mathf.RoundToInt(cells * perCell), min, max);
    }

    // Weighted pick of a chest type.
    public ChestKind PickChestKind(int level)
    {
        float roll = Random.value * (normalChestWeight + rareChestWeight + trappedChestWeight);
        if ((roll -= normalChestWeight) <= 0f) return ChestKind.Normal;
        if ((roll -= rareChestWeight) <= 0f) return ChestKind.Rare;
        return ChestKind.Trapped;
    }

    // Weighted pick among the monster types unlocked at this level.
    public MonsterConfig PickMonster(int level)
    {
        float total = 0f;
        foreach (MonsterSpawn type in monsterTypes)
            if (level >= type.minLevel) total += type.weight;

        float roll = Random.value * total;
        MonsterConfig pick = monsterTypes[0].config;
        foreach (MonsterSpawn type in monsterTypes)
        {
            if (level < type.minLevel) continue;
            pick = type.config;
            roll -= type.weight;
            if (roll <= 0f) break;
        }
        return pick;
    }
}

[System.Serializable]
public class MonsterSpawn
{
    public MonsterConfig config;
    public int minLevel = 1;
    public float weight = 1f;
}

// What a chest can contain, as percentages that add up to 100 (an item or nothing). Used by Chest.
[System.Serializable]
public class LootTable
{
    [System.Serializable]
    public struct Entry
    {
        [Tooltip("Empty = nothing inside.")]
        public ItemData item;
        public int count;
        [Tooltip("Chance in percent; all entries together should be 100.")]
        public float percent;
    }

    public Entry[] entries;

    public float Total()
    {
        float total = 0f;
        if (entries == null) return 0f;
        foreach (var entry in entries) total += entry.percent;
        return total;
    }

    public Entry Roll()
    {
        float pick = Random.value * 100f;
        foreach (var entry in entries)
        {
            pick -= entry.percent;
            if (pick <= 0f) return entry;
        }
        return default;
    }
}
