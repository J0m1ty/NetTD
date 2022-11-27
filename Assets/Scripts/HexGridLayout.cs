using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;

public enum GridType
{
    Square,
    Hexagon,
};

public class HexGridLayout : MonoBehaviour
{
    [Header("Grid Settings")]
    public GridType gridType;
    [ConditionalField("gridType", false, GridType.Square)]
    public Vector2Int gridSize;
    [ConditionalField("gridType", false, GridType.Hexagon)]
    public int gridRadius;

    [Header("Color Settings")]
    public Gradient gradient;

    [Header("Tile Settings")]
    public float outerSize= 1f;
    public float innerSize = 0f;
    public bool isFlatTopped;
    public Material material;
    public bool hasUniformHeight;
    [ConditionalField("hasUniformHeight", false, true)]
    public float height;
    [ConditionalField("hasUniformHeight", false, false)]
    public Vector2 heightRange;
    [ConditionalField("hasUniformHeight", false, false)]
    public float noiseMult;

    // Limit mirroring
    private float noiseOffset = 10_000f;

    private void OnEnable() {
        switch (gridType) {
            case GridType.Square:
                SquareGrid();
                break;
            case GridType.Hexagon:
                HexagonGrid();
                break;
        }
    }

    private void SquareGrid() {
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                AddHex($"Tile {x}, {y}", GetPositionForHexFromCoordinates(new Vector2Int(x, y)));
            }
        }
    }

    private Vector3 GetPositionForHexFromCoordinates(Vector2Int coordinate) {
        int column = coordinate.x;
        int row = coordinate.y;

        float width;
        float height;
        float xPosition;
        float yPosition;
        bool shouldOffset;
        float horizontalDistance;
        float verticalDistance;
        float offset;
        float size = outerSize;

        if (!isFlatTopped) {
            shouldOffset = (row % 2) == 0;
            width = Mathf.Sqrt(3) * size;
            height = 2f * size;

            horizontalDistance = width;
            verticalDistance = height * (3f / 4f);

            offset = (shouldOffset) ? width / 2f : 0f;

            xPosition = (horizontalDistance * column) + offset;
            yPosition = verticalDistance * row;
        }
        else {
            shouldOffset = (column % 2) == 0;
            width = 2f * size;
            height = Mathf.Sqrt(3) * size;

            horizontalDistance = width * (3f / 4f);
            verticalDistance = height;

            offset = (shouldOffset) ? height / 2f : 0f;

            xPosition = horizontalDistance * column;
            yPosition = (verticalDistance * row) + offset;
        }

        return new Vector3(xPosition, 0f, -yPosition);
    }

    private void HexagonGrid() {
        int GetRadius(int i) {
            return (int)Mathf.Floor((3f + Mathf.Sqrt(12f * i - 3f)) / 6f);
        }
        int GetPosition(int i, int r) {
            return (int)(i - 3f * r * (r - 1f) - 1f);
        }
        int GetIndex(int r, int p) {
            return (int)(3f * r * (r - 1f) + p + 1f);
        }

        Vector2 drawPointer = new Vector2(0, 0);
        int direction = 5;
        for (int layer = 0; layer < gridRadius; layer++) {
            int layerSize = layer == 0 ? 1 : 6 * layer;
            for (int pos = 0; pos < layerSize; pos++) {
                int i = layer == 0 ? 0 : GetIndex(layer, pos);
                int r = layer == 0 ? 0 : GetRadius(i);
                int p = layer == 0 ? 0 : GetPosition(i, r);

                bool corner = layer <= 1 ? true : (Mod(p, r) == r - 1);
                
                if (p == layerSize - 1) {
                    direction = 5;
                }
                else if (corner && direction != 5) {
                    direction = Mod(direction + 1, 6);
                }

                AddHex($"r: {r}, p: {p}, info: {corner}, Dir: {direction}", new Vector3(drawPointer.x, 0f, drawPointer.y));

                float theta = direction * Mathf.PI / 3f + (isFlatTopped ? Mathf.PI / 6f : 0f);
                drawPointer.x += outerSize * Mathf.Cos(theta) * Mathf.Sqrt(3);
                drawPointer.y += outerSize * Mathf.Sin(theta) * Mathf.Sqrt(3);

                if (p == layerSize - 1) {
                    direction = 0;
                }
            }
        }
    }

    private void AddHex(string name, Vector3 position) {
        GameObject tile = new GameObject(name, typeof(HexRenderer));
        tile.transform.position = position;
        
        HexRenderer hexRenderer = tile.GetComponent<HexRenderer>();

        Material hexMaterial = new Material(Shader.Find("Standard"));
        hexMaterial.CopyPropertiesFromMaterial(material);

        var noise01 = Mathf.Clamp01(Mathf.PerlinNoise(noiseOffset + position.x * noiseMult, noiseOffset + position.z * noiseMult));
        var hexHeight = hasUniformHeight ? height : Mathf.Lerp(heightRange.x, heightRange.y, noise01);

        hexMaterial.color = gradient.Evaluate(noise01);
        hexRenderer.height = hexHeight;

        hexRenderer.isFlatTopped = isFlatTopped;
        hexRenderer.outerSize = outerSize;
        hexRenderer.innerSize = innerSize;
        hexRenderer.SetMaterial(hexMaterial);
        hexRenderer.DrawMesh();

        tile.transform.SetParent(transform, true);
    }

    public int Mod(int x, int y) {
        if (y == 0) return 0;
        return ((x - (x / y) * y) + y) % y;
    }
}