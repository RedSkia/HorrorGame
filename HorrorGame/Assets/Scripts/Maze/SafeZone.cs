using UnityEngine;
using UnityEngine.AI;

// Safe start: a carved NavMesh box around the start cell, so monsters cannot walk in (or reach into it with a bite)
// until the player has left the cell; then it switches itself off for the rest of the level.
// Created by MazeSpawner each level and re-armed after a respawn (after the monsters were sent away, so none is inside).
public class SafeZone : MonoBehaviour
{
    const float CarveMargin = 1f;   // metres the carved box reaches past the cell, so a monster at its edge cannot bite a player inside the cell
    const float CarveHeight = 3f;

    Transform player;

    public static SafeZone Create(Vector3 cellCentre, Transform parent, Transform player)
    {
        var zone = new GameObject("Safe Zone").AddComponent<SafeZone>();
        zone.transform.SetParent(parent);
        zone.transform.position = cellCentre;
        zone.player = player;

        var obstacle = zone.gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Box;
        obstacle.size = new Vector3(MazeBuilder.CellSize + 2f * CarveMargin, CarveHeight, MazeBuilder.CellSize + 2f * CarveMargin);
        obstacle.center = Vector3.up * CarveHeight / 2f;
        obstacle.carving = true;
        obstacle.carveOnlyStationary = false;   // carve at once, not after standing still for a while
        return zone;
    }

    // Gone once the player's centre is outside the start cell.
    void Update()
    {
        Vector3 offset = player.position - transform.position;
        float half = MazeBuilder.CellSize / 2f;
        if (Mathf.Abs(offset.x) > half || Mathf.Abs(offset.z) > half) gameObject.SetActive(false);
    }
}
