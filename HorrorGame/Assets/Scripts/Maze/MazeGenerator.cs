using System.Collections.Generic;
using UnityEngine;

// Which walls a cell still has. Several at once are combined, e.g. Wall.North | Wall.East.
[System.Flags]
public enum Wall { None = 0, North = 1, East = 2, South = 4, West = 8, All = 15 }

// A wall that was opened as a shortcut: the cell and which side of it (North or East). MazeSpawner puts a locked door there.
public struct Shortcut
{
    public Vector2Int cell;
    public Wall side;

    // The cell on the other side of the wall.
    public Vector2Int Neighbour => cell + (side == Wall.North ? Vector2Int.up : Vector2Int.right);
}

// Maze maths only (no GameObjects): DFS maze + 2x2 rooms + braiding, and BFS distances. Used by GameManager and MazeSpawner.
// x = column (east is +), y = row (north is +).
public static class MazeGenerator
{
    // The 4 directions, in the order North, East, South, West. Direction d + 2 is always the opposite side.
    static readonly int[] StepX = { 0, 1, 0, -1 };
    static readonly int[] StepY = { 1, 0, -1, 0 };
    static readonly Wall[] Walls = { Wall.North, Wall.East, Wall.South, Wall.West };

    // Recursive backtracker: walk to a random unvisited neighbour and knock down the wall between;
    // at a dead end, step back until a cell has unvisited neighbours again. Every cell ends up reachable.
    public static Wall[,] Generate(int width, int height, int seed, int roomCount, float braidChance)
    {
        var grid = new Wall[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                grid[x, y] = Wall.All;

        var random = new System.Random(seed);   // same seed = same maze
        var visited = new bool[width, height];
        var path = new Stack<Vector2Int>();
        var options = new List<int>();

        visited[0, 0] = true;
        path.Push(new Vector2Int(0, 0));

        while (path.Count > 0)
        {
            Vector2Int cell = path.Peek();

            options.Clear();
            for (int d = 0; d < 4; d++)
            {
                int nx = cell.x + StepX[d];
                int ny = cell.y + StepY[d];
                if (Inside(grid, nx, ny) && !visited[nx, ny]) options.Add(d);
            }

            if (options.Count == 0)
            {
                path.Pop();
                continue;
            }

            int direction = options[random.Next(options.Count)];
            Open(grid, cell.x, cell.y, direction);
            var next = new Vector2Int(cell.x + StepX[direction], cell.y + StepY[direction]);
            visited[next.x, next.y] = true;
            path.Push(next);
        }

        CarveRooms(grid, random, roomCount);
        Braid(grid, random, braidChance);
        return grid;
    }

    // Opens (open = true) or closes again the wall of a shortcut, from both cells. The maze stays connected without any shortcut.
    public static void SetShortcut(Wall[,] grid, Shortcut shortcut, bool open)
    {
        int d = shortcut.side == Wall.North ? 0 : 1;
        if (open)
        {
            Open(grid, shortcut.cell.x, shortcut.cell.y, d);
            return;
        }
        Vector2Int other = shortcut.Neighbour;
        grid[shortcut.cell.x, shortcut.cell.y] |= Walls[d];
        grid[other.x, other.y] |= Walls[(d + 2) % 4];
    }

    // The cells of a shortest walk from one cell to another (both included).
    public static List<Vector2Int> Path(Wall[,] grid, Vector2Int from, Vector2Int to)
    {
        int[,] distances = Distances(grid, to.x, to.y);
        var path = new List<Vector2Int> { from };
        Vector2Int cell = from;
        while (cell != to)
        {
            for (int d = 0; d < 4; d++)
            {
                int nx = cell.x + StepX[d];
                int ny = cell.y + StepY[d];
                if (grid[cell.x, cell.y].HasFlag(Walls[d]) || !Inside(grid, nx, ny) || distances[nx, ny] != distances[cell.x, cell.y] - 1) continue;
                cell = new Vector2Int(nx, ny);
                break;
            }
            path.Add(cell);
        }
        return path;
    }

    // Number of open sides of a cell (1 = dead end, 2 = corridor, 3+ = junction).
    public static int Openings(Wall cell)
    {
        int count = 0;
        foreach (Wall wall in Walls)
            if (!cell.HasFlag(wall)) count++;
        return count;
    }

    // Steps from the start cell to every cell, walking only through open sides (breadth-first search).
    public static int[,] Distances(Wall[,] grid, int startX, int startY)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        var distances = new int[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                distances[x, y] = -1;   // -1 = not reached yet

        var queue = new Queue<Vector2Int>();
        distances[startX, startY] = 0;
        queue.Enqueue(new Vector2Int(startX, startY));

        while (queue.Count > 0)
        {
            Vector2Int cell = queue.Dequeue();
            for (int d = 0; d < 4; d++)
            {
                int nx = cell.x + StepX[d];
                int ny = cell.y + StepY[d];
                if (grid[cell.x, cell.y].HasFlag(Walls[d]) || !Inside(grid, nx, ny) || distances[nx, ny] >= 0) continue;

                distances[nx, ny] = distances[cell.x, cell.y] + 1;
                queue.Enqueue(new Vector2Int(nx, ny));
            }
        }
        return distances;
    }

    static bool Inside(Wall[,] grid, int x, int y)
    {
        return x >= 0 && y >= 0 && x < grid.GetLength(0) && y < grid.GetLength(1);
    }

    // Removes the wall between a cell and its neighbour in direction d (from both cells).
    static void Open(Wall[,] grid, int x, int y, int d)
    {
        grid[x, y] &= ~Walls[d];
        grid[x + StepX[d], y + StepY[d]] &= ~Walls[(d + 2) % 4];
    }

    // Removes the inner walls of random 2x2 blocks. Only removes walls, so the maze stays connected.
    static void CarveRooms(Wall[,] grid, System.Random random, int roomCount)
    {
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        if (width < 2 || height < 2) return;

        for (int i = 0; i < roomCount; i++)
        {
            int x = random.Next(width - 1);
            int y = random.Next(height - 1);
            Open(grid, x, y, 1);       // east of bottom-left
            Open(grid, x, y + 1, 1);   // east of top-left
            Open(grid, x, y, 0);       // north of bottom-left
            Open(grid, x + 1, y, 0);   // north of bottom-right
        }
    }

    // Opens one extra wall in some of the dead ends, so the maze gets loops.
    static void Braid(Wall[,] grid, System.Random random, float braidChance)
    {
        var closed = new List<int>();
        for (int x = 0; x < grid.GetLength(0); x++)
        {
            for (int y = 0; y < grid.GetLength(1); y++)
            {
                if (Openings(grid[x, y]) != 1 || random.NextDouble() >= braidChance) continue;

                closed.Clear();
                for (int d = 0; d < 4; d++)
                {
                    if (grid[x, y].HasFlag(Walls[d]) && Inside(grid, x + StepX[d], y + StepY[d])) closed.Add(d);
                }
                if (closed.Count > 0) Open(grid, x, y, closed[random.Next(closed.Count)]);
            }
        }
    }
}
