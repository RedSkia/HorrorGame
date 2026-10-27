using UnityEngine;
using UnityEngine.UIElements;

// Draws a MazeMap (north up): revealed cells, their walls, the exit door once revealed, the player arrow. Never monsters.
// Created and redrawn by the HUD (full map, M).
public class MapView : VisualElement
{
    static readonly Color FloorColor = new Color(0.16f, 0.15f, 0.14f, 0.9f);
    static readonly Color WallColor = new Color(0.88f, 0.86f, 0.8f);
    static readonly Color PlayerColor = new Color(0.86f, 0.18f, 0.16f);
    static readonly Color DoorColor = new Color(0.9f, 0.78f, 0.3f);

    readonly MazeMap map;

    public MapView(MazeMap map)
    {
        this.map = map;
        pickingMode = PickingMode.Ignore;
        generateVisualContent += Draw;
    }

    void Draw(MeshGenerationContext context)
    {
        if (map.Grid == null) return;
        var painter = context.painter2D;
        int width = map.Grid.GetLength(0);
        int height = map.Grid.GetLength(1);
        float cells = Mathf.Max(width, height);
        float size = Mathf.Min(contentRect.width, contentRect.height) / cells;
        Vector2 centre = new Vector2((width - 1) / 2f, (height - 1) / 2f);

        // Cell units to pixels: y flips because north is up.
        Vector2 ToPixel(float x, float y) => contentRect.center + new Vector2(x - centre.x, centre.y - y) * size;

        painter.lineWidth = Mathf.Max(2f, size * 0.08f);
        painter.lineCap = LineCap.Round;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (!map.IsRevealed(x, y)) continue;
                Vector2 northWest = ToPixel(x - 0.5f, y + 0.5f);
                Vector2 northEast = ToPixel(x + 0.5f, y + 0.5f);
                Vector2 southWest = ToPixel(x - 0.5f, y - 0.5f);
                Vector2 southEast = ToPixel(x + 0.5f, y - 0.5f);

                painter.fillColor = FloorColor;
                painter.BeginPath();
                painter.MoveTo(northWest);
                painter.LineTo(northEast);
                painter.LineTo(southEast);
                painter.LineTo(southWest);
                painter.ClosePath();
                painter.Fill();

                painter.strokeColor = WallColor;
                Wall walls = map.Grid[x, y];
                if (walls.HasFlag(Wall.North)) Line(painter, northWest, northEast);
                if (walls.HasFlag(Wall.East)) Line(painter, northEast, southEast);
                if (walls.HasFlag(Wall.South)) Line(painter, southWest, southEast);
                if (walls.HasFlag(Wall.West)) Line(painter, northWest, southWest);
            }
        }

        if (map.DoorRevealed)
        {
            Vector2 door = ToPixel(map.Door.x, map.Door.y);
            float half = size * 0.22f;
            painter.fillColor = DoorColor;
            painter.BeginPath();
            painter.MoveTo(door + new Vector2(-half, -half));
            painter.LineTo(door + new Vector2(half, -half));
            painter.LineTo(door + new Vector2(half, half));
            painter.LineTo(door + new Vector2(-half, half));
            painter.ClosePath();
            painter.Fill();
        }

        // Arrow: tip forward, two back corners. Yaw 0 = north = up on the screen.
        float yaw = map.PlayerYaw * Mathf.Deg2Rad;
        Vector2 forward = new Vector2(Mathf.Sin(yaw), -Mathf.Cos(yaw));
        Vector2 right = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
        Vector2 player = ToPixel(map.PlayerPosition.x, map.PlayerPosition.y);
        float arrow = Mathf.Max(size * 0.35f, 7f);
        painter.fillColor = PlayerColor;
        painter.BeginPath();
        painter.MoveTo(player + forward * arrow);
        painter.LineTo(player - forward * arrow * 0.7f + right * arrow * 0.6f);
        painter.LineTo(player - forward * arrow * 0.7f - right * arrow * 0.6f);
        painter.ClosePath();
        painter.Fill();
    }

    static void Line(Painter2D painter, Vector2 from, Vector2 to)
    {
        painter.BeginPath();
        painter.MoveTo(from);
        painter.LineTo(to);
        painter.Stroke();
    }
}
