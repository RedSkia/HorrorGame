using UnityEngine;

// Turns the Wall[,] grid from MazeGenerator into floor and wall objects. Used by GameManager and MazeSpawner (cell positions).
public class MazeBuilder : MonoBehaviour
{
    // Geometry shared with MazeSpawner and MazeMap (metres).
    public const float CellSize = 3f;
    public const float WallHeight = 3f;
    public const float WallThickness = 0.2f;

    [SerializeField] GameObject wallPrefab;
    [SerializeField] GameObject floorPrefab;

    Wall[,] grid;

    public void Build(Wall[,] newGrid)
    {
        grid = newGrid;
        Clear();

        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        float half = CellSize / 2f;

        SpawnFloor(width, height);

        // Neighbouring cells share walls, so every cell builds its North and West wall;
        // East and South walls are only built on the maze border.
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector3 centre = CellToWorld(x, y);
                if (grid[x, y].HasFlag(Wall.North)) SpawnWall(centre + Vector3.forward * half, true);
                if (grid[x, y].HasFlag(Wall.West)) SpawnWall(centre + Vector3.left * half, false);
                if (x == width - 1 && grid[x, y].HasFlag(Wall.East)) SpawnWall(centre + Vector3.right * half, false);
                if (y == 0 && grid[x, y].HasFlag(Wall.South)) SpawnWall(centre + Vector3.back * half, true);
            }
        }
    }

    public Vector3 CellToWorld(int x, int y)
    {
        return transform.position + new Vector3(x * CellSize, 0f, y * CellSize);
    }

    public Vector3 RandomCellPosition()
    {
        return CellToWorld(Random.Range(0, grid.GetLength(0)), Random.Range(0, grid.GetLength(1)));
    }

    // Deactivated first: Destroy is deferred, so a NavMesh rebuild this frame would still see the old walls.
    void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i).gameObject;
            child.SetActive(false);
            Destroy(child);
        }
    }

    void SpawnFloor(int width, int height)
    {
        float size = CellSize;
        var floor = Instantiate(floorPrefab, transform);
        floor.transform.position = (CellToWorld(0, 0) + CellToWorld(width - 1, height - 1)) / 2f - Vector3.up * 0.05f;
        floor.transform.localScale = new Vector3(width * size, 0.1f, height * size);

        // One texture tile per cell instead of one tile stretched over the whole floor.
        var block = new MaterialPropertyBlock();
        block.SetVector("_BaseMap_ST", new Vector4(width, height, 0f, 0f));
        floor.GetComponent<Renderer>().SetPropertyBlock(block);
    }

    // alongX: wall runs along the X axis (north/south edges).
    void SpawnWall(Vector3 edgeCentre, bool alongX)
    {
        float length = CellSize + WallThickness;
        var wall = Instantiate(wallPrefab, transform);
        wall.transform.position = edgeCentre + Vector3.up * WallHeight / 2f;
        wall.transform.localScale = alongX
            ? new Vector3(length, WallHeight, WallThickness)
            : new Vector3(WallThickness, WallHeight, length);
    }
}
