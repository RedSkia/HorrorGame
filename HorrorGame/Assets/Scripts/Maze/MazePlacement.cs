using System.Collections.Generic;
using UnityEngine;

// Where things go in a generated maze (no GameObjects, so it can be tested on its own): exit door, key chest, chests, lockers,
// shortcut doors, monsters. Used by GameManager (shortcuts) and MazeSpawner. Every pick has a fallback, so the door and the key always
// get a cell, even in mazes with few dead ends. Distances are steps on foot with the shortcut doors still closed.
public static class MazePlacement
{
    const int ChestSpacing = 3;               // chests are at least this many steps apart
    const int ChestMinStartDistance = 2;      // ... and this far from the start cell
    const int LockerSpacing = 3;              // lockers are at least this many steps apart
    const int LockerFallbackStartDistance = 2;   // ... relaxed to this (and next to chests allowed) for a small maze that has too few other corridors
    const int ShortcutSpacing = 4;            // shortcut doors are at least this many cells apart (Manhattan)
    const int ShortcutMinStartDistance = 5;   // ... and this many steps from the start
    const int ShortcutMinSaved = 6;           // a shortcut must save at least this many steps between its two cells

    // World direction of the first open side.
    public static Vector3 OpenDirection(Wall cell)
    {
        if (!cell.HasFlag(Wall.North)) return Vector3.forward;
        if (!cell.HasFlag(Wall.East)) return Vector3.right;
        if (!cell.HasFlag(Wall.South)) return Vector3.back;
        return Vector3.left;
    }

    // World directions of all closed (walled) sides.
    public static List<Vector3> ClosedDirections(Wall cell)
    {
        var directions = new List<Vector3>();
        if (cell.HasFlag(Wall.North)) directions.Add(Vector3.forward);
        if (cell.HasFlag(Wall.East)) directions.Add(Vector3.right);
        if (cell.HasFlag(Wall.South)) directions.Add(Vector3.back);
        if (cell.HasFlag(Wall.West)) directions.Add(Vector3.left);
        return directions;
    }

    // A door or chest goes against the wall opposite the first open side, so that wall must exist.
    public static bool HasSlot(Wall cell)
    {
        return MazeGenerator.Openings(cell) >= 1 && ClosedDirections(cell).Contains(-OpenDirection(cell));
    }

    // Dead ends (one opening) except the start cell.
    public static List<Vector2Int> DeadEnds(Wall[,] grid)
    {
        return Cells(grid, cell => MazeGenerator.Openings(cell) == 1);
    }

    // Every cell except the start where a door or chest fits.
    public static List<Vector2Int> Slots(Wall[,] grid)
    {
        return Cells(grid, HasSlot);
    }

    // Door = the dead end farthest from the start; key = the dead end farthest from both start and door.
    // Without dead ends (or without a second one) any cell with a slot is used. False only if the maze has no two such cells.
    public static bool PickDoorAndKey(Wall[,] grid, int[,] fromStart, out Vector2Int door, out Vector2Int key)
    {
        door = key = Vector2Int.zero;
        List<Vector2Int> deadEnds = DeadEnds(grid);
        List<Vector2Int> slots = Slots(grid);
        if (slots.Count < 2) return false;

        door = Farthest(deadEnds.Count > 0 ? deadEnds : slots, fromStart);
        deadEnds.Remove(door);
        slots.Remove(door);
        int[,] fromDoor = MazeGenerator.Distances(grid, door.x, door.y);
        key = FarthestFromBoth(deadEnds.Count > 0 ? deadEnds : slots, fromStart, fromDoor);
        return true;
    }

    // "count" cells for monsters: as far as possible from the start and from each other,
    // never closer to the start than minDistance (unless the maze has no such cell). Ties are broken at random.
    public static List<Vector2Int> MonsterCells(Wall[,] grid, int[,] fromStart, int count, int minDistance)
    {
        List<Vector2Int> candidates = Cells(grid, cell => true);
        List<Vector2Int> far = candidates.FindAll(c => fromStart[c.x, c.y] >= minDistance);
        return Spread(grid, far.Count > 0 ? far : candidates, fromStart, count);
    }

    // "count" cells for chests: dead ends first, then other cells with a chest slot; spread over the whole maze, at least
    // ChestSpacing steps from each other and from the "placed" cells (exit door, key chest), never in or beside the exit door cell,
    // in the "avoid" cells (shortcut doors) or in the first ChestMinStartDistance steps from the start. The spacing relaxes if a small maze needs it.
    public static List<Vector2Int> ChestCells(Wall[,] grid, int[,] fromStart, int count, List<Vector2Int> placed, List<Vector2Int> avoid, Vector2Int door)
    {
        List<Vector2Int> slots = Slots(grid);
        slots.RemoveAll(c => fromStart[c.x, c.y] < ChestMinStartDistance || avoid.Contains(c) || Manhattan(c, door) <= 1);
        List<Vector2Int> picked = Spread(grid, slots.FindAll(c => MazeGenerator.Openings(grid[c.x, c.y]) == 1), fromStart, count, placed, ChestSpacing);
        for (int spacing = ChestSpacing; spacing >= 1 && picked.Count < count; spacing--)
        {
            var all = new List<Vector2Int>(placed);
            all.AddRange(picked);
            picked.AddRange(Spread(grid, slots.FindAll(c => !picked.Contains(c)), fromStart, count - picked.Count, all, spacing));
        }
        return picked;
    }

    // Of the chest cells, takes the one for a shortcut's key: a random one that can be reached before the shortcut (not much farther
    // from the start than the shortcut), else the one closest to the start. The cell is removed from the list.
    public static Vector2Int TakeShortcutKeyCell(Shortcut shortcut, int[,] fromStart, List<Vector2Int> cells)
    {
        int near = Mathf.Min(fromStart[shortcut.cell.x, shortcut.cell.y], fromStart[shortcut.Neighbour.x, shortcut.Neighbour.y]);
        List<Vector2Int> before = cells.FindAll(c => fromStart[c.x, c.y] <= near + 2);
        Vector2Int pick = cells[0];
        if (before.Count > 0) pick = before[Random.Range(0, before.Count)];
        else foreach (Vector2Int c in cells) if (fromStart[c.x, c.y] < fromStart[pick.x, pick.y]) pick = c;
        cells.Remove(pick);
        return pick;
    }

    // "count" walls to open as locked shortcut doors (the grid itself is not changed; MazeGenerator.SetShortcut opens them).
    // Each one joins two cells that are far apart on foot, saves walking on the way from the start to the exit, is not near the start,
    // not at the exit door or key cell and not near another shortcut. Several shortcuts are chosen one after the other, each on the maze
    // with the earlier ones open. Picked at random among the best quarter.
    public static List<Shortcut> PickShortcuts(Wall[,] grid, int count)
    {
        var picked = new List<Shortcut>();
        Wall[,] open = (Wall[,])grid.Clone();
        if (!PickDoorAndKey(open, MazeGenerator.Distances(open, 0, 0), out Vector2Int door, out Vector2Int key)) return picked;

        for (int i = 0; i < count; i++)
        {
            if (!TryBestShortcut(open, picked, door, key, true, out Shortcut pick) && !TryBestShortcut(open, picked, door, key, false, out pick)) break;
            picked.Add(pick);
            MazeGenerator.SetShortcut(open, pick, true);
        }
        return picked;
    }

    static bool TryBestShortcut(Wall[,] grid, List<Shortcut> taken, Vector2Int door, Vector2Int key, bool strict, out Shortcut result)
    {
        int[,] fromStart = MazeGenerator.Distances(grid, 0, 0);
        int[,] fromDoor = MazeGenerator.Distances(grid, door.x, door.y);
        int minSaved = strict ? ShortcutMinSaved : ShortcutMinSaved / 2;
        var scored = new List<KeyValuePair<int, Shortcut>>();
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                foreach (Wall side in new[] { Wall.North, Wall.East })   // every wall is looked at once
                {
                    var s = new Shortcut { cell = new Vector2Int(x, y), side = side };
                    Vector2Int b = s.Neighbour;
                    if (!grid[x, y].HasFlag(side) || b.x >= grid.GetLength(0) || b.y >= grid.GetLength(1)) continue;
                    if (s.cell == door || s.cell == key || b == door || b == key) continue;
                    if (taken.Exists(t => Manhattan(t.cell, s.cell) < ShortcutSpacing)) continue;

                    int a = fromStart[x, y], c = fromStart[b.x, b.y];
                    if (Mathf.Min(a, c) < (strict ? ShortcutMinStartDistance : 2)) continue;
                    int saved = MazeGenerator.Distances(grid, x, y)[b.x, b.y] - 1;   // steps the walk gets shorter between the two cells
                    if (saved < minSaved) continue;
                    int onRoute = fromStart[door.x, door.y] - Mathf.Min(a + 1 + fromDoor[b.x, b.y], c + 1 + fromDoor[x, y]);   // ... and on the way to the exit
                    scored.Add(new KeyValuePair<int, Shortcut>(saved + 2 * Mathf.Max(onRoute, 0), s));
                }
            }
        }
        result = default;
        if (scored.Count == 0) return false;
        scored.Sort((p, q) => q.Key.CompareTo(p.Key));
        result = scored[Random.Range(0, Mathf.Max(1, scored.Count / 4))].Value;
        return true;
    }

    // "count" cells for lockers: corridor cells (2 openings, so never a trap) next to a junction (3+ openings), where a chased player can
    // reach one; spread over the maze, at least minStartDistance from the start and not in or beside the "avoid" cells (chests, door, shortcuts).
    // Half of them go near the walk from the start to the key and on to the exit (escape routes), the rest anywhere, farthest from the others first.
    // At least LockerSpacing steps apart (relaxed to 2 for a small maze); falls back to any corridor cell (then also closer to the start) when there are too few near junctions.
    public static List<Vector2Int> LockerCells(Wall[,] grid, int[,] fromStart, int count, int minStartDistance, List<Vector2Int> avoid, Vector2Int key, Vector2Int door)
    {
        List<Vector2Int> corridors = Cells(grid, cell => MazeGenerator.Openings(cell) == 2);
        corridors.RemoveAll(c => fromStart[c.x, c.y] < LockerFallbackStartDistance || avoid.Contains(c));
        List<Vector2Int> far = corridors.FindAll(c => fromStart[c.x, c.y] >= minStartDistance && !avoid.Exists(a => Manhattan(a, c) <= 1));
        List<Vector2Int> nearJunction = far.FindAll(c => NextToJunction(grid, c));

        List<Vector2Int> route = MazeGenerator.Path(grid, Vector2Int.zero, key);
        route.AddRange(MazeGenerator.Path(grid, key, door));
        List<Vector2Int> onRoute = nearJunction.FindAll(c => route.Exists(r => Manhattan(r, c) <= 2));

        var picked = new List<Vector2Int>();
        AddSpread(grid, picked, onRoute, fromStart, (count + 1) / 2, LockerSpacing);
        for (int spacing = LockerSpacing; spacing >= 2 && picked.Count < count; spacing--)
        {
            AddSpread(grid, picked, nearJunction, fromStart, count, spacing);
            AddSpread(grid, picked, far, fromStart, count, spacing);
        }
        AddSpread(grid, picked, corridors, fromStart, count, 2);   // small maze: last resort, closer to the start and next to chests
        return picked;
    }

    // Adds cells to "picked" until it holds "limit" (Spread, counting the cells already picked).
    static void AddSpread(Wall[,] grid, List<Vector2Int> picked, List<Vector2Int> candidates, int[,] fromStart, int limit, int spacing)
    {
        List<Vector2Int> rest = candidates.FindAll(c => !picked.Contains(c));
        picked.AddRange(Spread(grid, rest, fromStart, limit - picked.Count, new List<Vector2Int>(picked), spacing));
    }

    // True when an open side leads to a cell with 3 or more openings.
    static bool NextToJunction(Wall[,] grid, Vector2Int c)
    {
        Wall w = grid[c.x, c.y];
        return (!w.HasFlag(Wall.North) && MazeGenerator.Openings(grid[c.x, c.y + 1]) >= 3)
            || (!w.HasFlag(Wall.East) && MazeGenerator.Openings(grid[c.x + 1, c.y]) >= 3)
            || (!w.HasFlag(Wall.South) && MazeGenerator.Openings(grid[c.x, c.y - 1]) >= 3)
            || (!w.HasFlag(Wall.West) && MazeGenerator.Openings(grid[c.x - 1, c.y]) >= 3);
    }

    static int Manhattan(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    // Greedy farthest-point pick: each cell is as far as possible (in steps) from the start and from the cells picked before it
    // (and from the "placed" cells). Cells closer than minSpacing steps to a picked or placed cell are dropped. Ties are broken at random.
    static List<Vector2Int> Spread(Wall[,] grid, List<Vector2Int> candidates, int[,] fromStart, int count, List<Vector2Int> placed = null, int minSpacing = 0)
    {
        candidates = new List<Vector2Int>(candidates);

        var picked = new List<Vector2Int>();
        var gap = new int[grid.GetLength(0), grid.GetLength(1)];   // steps to the nearest picked or placed cell
        for (int x = 0; x < gap.GetLength(0); x++)
            for (int y = 0; y < gap.GetLength(1); y++)
                gap[x, y] = int.MaxValue;
        if (placed != null)
            foreach (Vector2Int p in placed) Closer(gap, MazeGenerator.Distances(grid, p.x, p.y));

        for (int i = 0; i < count; i++)
        {
            candidates.RemoveAll(c => gap[c.x, c.y] < minSpacing);
            if (candidates.Count == 0) break;

            int best = -1;
            var top = new List<Vector2Int>();
            foreach (Vector2Int c in candidates)
            {
                int score = Mathf.Min(fromStart[c.x, c.y], gap[c.x, c.y]);
                if (score > best)
                {
                    best = score;
                    top.Clear();
                }
                if (score == best) top.Add(c);
            }

            Vector2Int pick = top[Random.Range(0, top.Count)];
            picked.Add(pick);
            candidates.Remove(pick);
            Closer(gap, MazeGenerator.Distances(grid, pick.x, pick.y));
        }
        return picked;
    }

    static void Closer(int[,] gap, int[,] distances)
    {
        for (int x = 0; x < gap.GetLength(0); x++)
            for (int y = 0; y < gap.GetLength(1); y++)
                gap[x, y] = Mathf.Min(gap[x, y], distances[x, y]);
    }

    // All cells except the start that match the filter.
    public static List<Vector2Int> Cells(Wall[,] grid, System.Predicate<Wall> filter)
    {
        var cells = new List<Vector2Int>();
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (x == 0 && y == 0) continue;
                if (filter(grid[x, y])) cells.Add(new Vector2Int(x, y));
            }
        }
        return cells;
    }

    // The cell with the biggest distance.
    static Vector2Int Farthest(List<Vector2Int> cells, int[,] distances)
    {
        Vector2Int best = cells[0];
        foreach (Vector2Int cell in cells)
        {
            if (distances[cell.x, cell.y] > distances[best.x, best.y]) best = cell;
        }
        return best;
    }

    // The cell whose shorter distance (to A or to B) is the biggest, so it is far from both.
    static Vector2Int FarthestFromBoth(List<Vector2Int> cells, int[,] fromA, int[,] fromB)
    {
        Vector2Int best = cells[0];
        int bestDistance = -1;
        foreach (Vector2Int cell in cells)
        {
            int distance = Mathf.Min(fromA[cell.x, cell.y], fromB[cell.x, cell.y]);
            if (distance > bestDistance)
            {
                best = cell;
                bestDistance = distance;
            }
        }
        return best;
    }
}
