using System.Collections.Generic;
using UnityEngine;

// Fills a built maze with the exit door, shortcut doors (+ their keys in extra chests), chests (one holds the key), lockers, monsters
// and the safe start zone.
// Called by GameManager after the NavMesh is baked; Atmosphere reads the monster list. Placement maths: MazePlacement.
public class MazeSpawner : MonoBehaviour
{
    // Depths (m) of the prefabs, used to put them flush against a wall.
    const float DoorDepth = 0.1f;
    const float ChestDepth = 0.5f;
    const float LockerDepth = 0.6f;
    const int LockerMinStartDistance = 4;   // lockers are at least this many steps from the start cell

    [SerializeField] GameConfig config;
    [SerializeField] MazeBuilder mazeBuilder;
    [SerializeField] MonsterAI monsterPrefab;
    [SerializeField] ItemData keyItem;
    [SerializeField] ItemData shortcutKeyItem;
    [SerializeField] PlayerInventory inventory;
    [SerializeField] GameObject doorPrefab;
    [SerializeField] GameObject shortcutDoorPrefab;
    [SerializeField] GameObject chestPrefab;
    [SerializeField] GameObject lockerPrefab;
    [SerializeField] Flashlight flashlight;
    [SerializeField] PlayerController playerController;

    List<GameObject> spawned = new List<GameObject>();
    Wall[,] grid;
    int[,] fromStart;
    SafeZone safeZone;

    public List<MonsterAI> Monsters { get; } = new List<MonsterAI>();
    public Vector2Int DoorCell { get; private set; }   // the exit door's cell, for the map

    // Call after the NavMesh is baked, so the spawned objects don't end up in it.
    public void Spawn(Wall[,] openGrid, int level, List<Shortcut> shortcuts)
    {
        // Everything is placed on the maze as it is while the shortcut doors are closed (the walls there are put back for the placement).
        grid = (Wall[,])openGrid.Clone();
        foreach (Shortcut shortcut in shortcuts) MazeGenerator.SetShortcut(grid, shortcut, false);
        fromStart = MazeGenerator.Distances(grid, 0, 0);

        if (!MazePlacement.PickDoorAndKey(grid, fromStart, out Vector2Int door, out Vector2Int keyCell))
        {
            Debug.LogError("Maze has no room for the exit door and the key");
            return;
        }
        DoorCell = door;
        SpawnAtWall(doorPrefab, door, -MazePlacement.OpenDirection(grid[door.x, door.y]), DoorDepth).GetComponent<Door>().Initialize(inventory);
        SpawnChest(keyCell).SetContents(keyItem, 1);   // always a Normal, safe chest

        // Chests (counts scale with the maze, see GameConfig): one per shortcut holds its key, the rest get a random kind. The key chest is already placed.
        var shortcutCells = new List<Vector2Int>();
        foreach (Shortcut shortcut in shortcuts)
        {
            SpawnShortcutDoor(shortcut);
            shortcutCells.Add(shortcut.cell);
            shortcutCells.Add(shortcut.Neighbour);
        }
        var used = new List<Vector2Int> { door, keyCell };   // cells taken so far (lockers keep away from them)
        List<Vector2Int> chestCells = MazePlacement.ChestCells(grid, fromStart, config.ChestCount(grid.Length) - 1 + shortcuts.Count, used, shortcutCells, door);
        used.AddRange(chestCells);
        foreach (Shortcut shortcut in shortcuts)
        {
            if (chestCells.Count > 0) SpawnChest(MazePlacement.TakeShortcutKeyCell(shortcut, fromStart, chestCells)).SetContents(shortcutKeyItem, 1);
        }
        foreach (Vector2Int cell in chestCells) SpawnChest(cell).Initialize(config.PickChestKind(level), config);

        used.AddRange(shortcutCells);
        SpawnLockers(config.LockerCount(grid.Length), used, keyCell, door);
        SpawnMonsters(Mathf.Min(config.monsterCount + (level - 1) / config.levelsPerExtraMonster, config.maxMonsters), level);

        safeZone = SafeZone.Create(mazeBuilder.CellToWorld(0, 0), transform, playerController.transform);
        spawned.Add(safeZone.gameObject);
    }

    // After the player was caught (and is back at the start): the monsters go to the cells farthest from the player again,
    // then the safe start zone is back (no monster can be inside it any more).
    public void ResetMonsters()
    {
        List<Vector2Int> cells = MazePlacement.MonsterCells(grid, fromStart, Monsters.Count, config.minMonsterDistance);
        for (int i = 0; i < Monsters.Count && i < cells.Count; i++)
            Monsters[i].Respawn(mazeBuilder.CellToWorld(cells[i].x, cells[i].y));
        if (safeZone != null) safeZone.gameObject.SetActive(true);
    }

    // Objects are deactivated before Destroy because Destroy only happens at the end of the frame,
    // and a NavMesh bake in the same frame would still see them. Looted chests have already destroyed themselves.
    public void Clear()
    {
        foreach (GameObject obj in spawned) Remove(obj);
        foreach (MonsterAI monster in Monsters) Remove(monster.gameObject);
        spawned.Clear();
        Monsters.Clear();
    }

    // The chest goes against the wall opposite the open side.
    Chest SpawnChest(Vector2Int cell)
    {
        GameObject chest = SpawnAtWall(chestPrefab, cell, -MazePlacement.OpenDirection(grid[cell.x, cell.y]), ChestDepth);
        return chest.GetComponent<Chest>();
    }

    // Lockers stand against a wall of a corridor cell near a junction, spread out and along the escape routes (MazePlacement.LockerCells); not at chests, doors or shortcuts.
    void SpawnLockers(int count, List<Vector2Int> avoid, Vector2Int key, Vector2Int door)
    {
        foreach (Vector2Int cell in MazePlacement.LockerCells(grid, fromStart, count, LockerMinStartDistance, avoid, key, door))
        {
            List<Vector3> walls = MazePlacement.ClosedDirections(grid[cell.x, cell.y]);
            SpawnAtWall(lockerPrefab, cell, walls[Random.Range(0, walls.Count)], LockerDepth);
        }
    }

    // Monsters start on the cells farthest from the player and from each other (never closer than minMonsterDistance).
    void SpawnMonsters(int count, int level)
    {
        foreach (Vector2Int cell in MazePlacement.MonsterCells(grid, fromStart, count, config.minMonsterDistance))
        {
            MonsterAI monster = Instantiate(monsterPrefab, mazeBuilder.CellToWorld(cell.x, cell.y), Quaternion.identity, transform);
            monster.Initialize(config.PickMonster(level), mazeBuilder, playerController, flashlight);
            Monsters.Add(monster);
        }
    }

    // The door fills the opening that was carved in the wall (centred on the shared edge).
    void SpawnShortcutDoor(Shortcut shortcut)
    {
        Vector3 toWall = shortcut.side == Wall.North ? Vector3.forward : Vector3.right;
        Vector3 position = mazeBuilder.CellToWorld(shortcut.cell.x, shortcut.cell.y) + toWall * MazeBuilder.CellSize / 2f;
        spawned.Add(Instantiate(shortcutDoorPrefab, position, Quaternion.LookRotation(toWall), transform));
    }

    // Places a prefab flush against the cell's wall in direction toWall, facing into the cell.
    GameObject SpawnAtWall(GameObject prefab, Vector2Int cell, Vector3 toWall, float depth)
    {
        float offset = MazeBuilder.CellSize / 2f - MazeBuilder.WallThickness / 2f - depth / 2f;
        Vector3 position = mazeBuilder.CellToWorld(cell.x, cell.y) + toWall * offset;
        GameObject obj = Instantiate(prefab, position, Quaternion.LookRotation(-toWall), transform);
        spawned.Add(obj);
        return obj;
    }

    static void Remove(GameObject obj)
    {
        if (obj == null) return;
        obj.SetActive(false);
        Destroy(obj);
    }
}
