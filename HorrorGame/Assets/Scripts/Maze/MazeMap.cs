using UnityEngine;

// What the player knows of the current maze: the cells walked through, or all of them after a map fragment.
// GameManager calls Begin for each level; MapView (HUD) draws it. Cell units: cell (x, y) has its centre at (x, y).
public class MazeMap : MonoBehaviour
{
    [SerializeField] MazeBuilder mazeBuilder;
    [SerializeField] Transform player;

    bool[,] revealed;

    public Wall[,] Grid { get; private set; }
    public Vector2Int Door { get; private set; }

    public bool IsRevealed(int x, int y) => revealed[x, y];
    public bool DoorRevealed => Grid != null && revealed[Door.x, Door.y];
    public float PlayerYaw => player.eulerAngles.y;

    public Vector2 PlayerPosition => ToCell(player.position);

    void OnEnable()
    {
        GameEvents.MapFragmentFound += RevealAll;
    }

    void OnDisable()
    {
        GameEvents.MapFragmentFound -= RevealAll;
    }

    Vector2 ToCell(Vector3 world)
    {
        Vector3 local = world - mazeBuilder.CellToWorld(0, 0);
        return new Vector2(local.x, local.z) / MazeBuilder.CellSize;
    }

    public void Begin(Wall[,] grid, Vector2Int door)
    {
        Grid = grid;
        Door = door;
        revealed = new bool[grid.GetLength(0), grid.GetLength(1)];
    }

    void Update()
    {
        if (Grid == null) return;
        Vector2 position = PlayerPosition;
        int x = Mathf.Clamp(Mathf.RoundToInt(position.x), 0, revealed.GetLength(0) - 1);
        int y = Mathf.Clamp(Mathf.RoundToInt(position.y), 0, revealed.GetLength(1) - 1);
        revealed[x, y] = true;
    }

    // A map fragment reveals the whole maze.
    void RevealAll()
    {
        if (Grid == null) return;
        for (int x = 0; x < revealed.GetLength(0); x++)
            for (int y = 0; y < revealed.GetLength(1); y++)
                revealed[x, y] = true;
    }
}
