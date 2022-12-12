using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

[System.Serializable]
public enum GridType
{
    Square,
    Hexagon
};

public static class Radial {
    public static int Layer(int index) {
        return (int)Mathf.Floor((3f + Mathf.Sqrt(12f * index - 3f)) / 6f);
    }
    public static int Position(int index, int layer) {
        return (int)(index - 3f * layer * (layer - 1f) - 1f);
    }
    public static int Index(int layer, int position) {
        return (int)(3f * layer * (layer - 1) + 1 + position);
    }
    public static int Mod(int x, int y) {
        if (y == 0) return 0;
        return ((x - (x / y) * y) + y) % y;
    }
}

[System.Serializable]
public class GridUnit
{
    public int index;
    public HexRenderer hexRenderer;
    public Tower tower;
    
    public GridUnit(int index, HexRenderer hex) {
        this.index = index;
        hexRenderer = hex;
        tower = null;

        hexRenderer.gridRef = this;
    }

    public bool AddTower(Tower tower, bool force = false) {
        if (this.tower != null && !force) return false;

        this.tower = tower;
        return true;
    }

    public int GetNeighbor(int direction) {
        return GetNeighbors()[direction];
    }

    public int[] GetNeighbors() {
        int[] neighbors = new int[6];
        
        if (index == 0) {
            for (int i = 0; i < 6; i++) {
                neighbors[i] = Radial.Index(0, i);
            }
        }
        else {
            var layer = index == 0 ? 0 : Radial.Layer(index);
            var position = layer == 0 ? 0 : Radial.Position(index, layer);

            var corner = layer <= 1 ? true : (Radial.Mod(position, layer) == layer - 1);
            
            if (corner) {
                neighbors = new int[]{
                    Radial.Index(layer - 1, position),
                    Radial.Index(layer, position - 1),
                    Radial.Index(layer, position + 1),
                    Radial.Index(layer + 1, position - 1),
                    Radial.Index(layer + 1, position),
                    Radial.Index(layer + 1, position + 1)
                };
            }
            else {
                neighbors = new int[]{
                    Radial.Index(layer - 1, position - 1),
                    Radial.Index(layer - 1, position),
                    Radial.Index(layer, position - 1),
                    Radial.Index(layer, position + 1),
                    Radial.Index(layer + 1, position),
                    Radial.Index(layer + 1, position + 1)
                };
            }
        }
        
        return neighbors;
    }

    public Vector3 GetWorldPosition() {
        return hexRenderer.transform.position;
    }

    public float Distance(GridUnit other) {
        return Vector3.Distance(hexRenderer.transform.position, other.hexRenderer.transform.position);
    }
}

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

    [Header("Noise Settings")]
    public bool hasUniformHeight;
    [ConditionalField("hasUniformHeight", false, true)]
    public float height;
    [ConditionalField("hasUniformHeight", false, false)]
    public MinMaxFloat heightRange;
    [ConditionalField("hasUniformHeight", false, false)]
    public float noiseMult;
    [ConditionalField("hasUniformHeight", false, false)]
    public MinMaxFloat mountainHeightRange;
    [ConditionalField("hasUniformHeight", false, false)]
    public float mountainNoiseMult;
    private float noiseOffset;

    [Header("Hex Info")]
    public List<GridUnit> hexes = new List<GridUnit>();
    public Tower[] towers {
        get {
            List<Tower> towers = new List<Tower>();
            foreach (var hex in hexes) {
                if (hex.tower != null) towers.Add(hex.tower);
            }
            return towers.ToArray();
        }
    }
    
    public void Generate(float setNoiseOffset = 10_000f) {
        noiseOffset = setNoiseOffset;
        
        switch (gridType) {
            case GridType.Square:
                SquareGrid();
                break;
            case GridType.Hexagon:
                HexagonGrid();
                break;
        }
        
        GameManager.instance?.Simplify(transform);
    }

    private void SquareGrid() {
        for (int y = 0; y < gridSize.y; y++)
        {
            for (int x = 0; x < gridSize.x; x++)
            {
                int i = x + y * gridSize.x;
                AddHex($"Tile {x}, {y}", i, GetPositionForHexFromCoordinates(new Vector2Int(x, y)));
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
        Vector2 drawPointer = new Vector2(0, 0);
        int direction = 5;
        for (int layer = 0; layer < gridRadius; layer++) {
            int layerSize = layer == 0 ? 1 : 6 * layer;
            for (int pos = 0; pos < layerSize; pos++) {
                int i = layer == 0 ? 0 : Radial.Index(layer, pos);
                int r = layer == 0 ? 0 : Radial.Layer(i);
                int p = layer == 0 ? 0 : Radial.Position(i, r);

                bool corner = layer <= 1 ? true : (Radial.Mod(p, r) == r - 1);
                
                if (p == layerSize - 1) {
                    direction = 5;
                }
                else if (corner && direction != 5) {
                    direction = Radial.Mod(direction + 1, 6);
                }

                AddHex($"Hex {i}", i, new Vector3(drawPointer.x, 0f, drawPointer.y));

                float theta = direction * Mathf.PI / 3f + (isFlatTopped ? Mathf.PI / 6f : 0f);
                drawPointer.x += outerSize * Mathf.Cos(theta) * Mathf.Sqrt(3);
                drawPointer.y += outerSize * Mathf.Sin(theta) * Mathf.Sqrt(3);

                if (p == layerSize - 1) {
                    direction = 0;
                }
            }
        }
    }

    private void AddHex(string name, int index, Vector3 position) {
        GameObject tile = new GameObject(name, typeof(HexRenderer));
        tile.transform.SetParent(transform, true);
        tile.transform.position = position;
        tile.layer = 8;
        
        var noise01 = Mathf.Clamp01(Mathf.PerlinNoise(noiseOffset + position.x * noiseMult, noiseOffset + position.z * noiseMult));
        var noiseHeight = hasUniformHeight ? height : Mathf.Lerp(heightRange.Min, heightRange.Max, noise01);

        var mountainNoise = Mathf.Clamp01(Mathf.PerlinNoise((noiseOffset * 2f) + position.x * mountainNoiseMult, (noiseOffset * 2f) + position.z * mountainNoiseMult));
        var mountainHeight = hasUniformHeight ? height : Mathf.Lerp(mountainHeightRange.Min, mountainHeightRange.Max, mountainNoise);
        
        var hexHeight = Mathf.Lerp(noiseHeight, mountainHeight, 0.5f);
        var hexNoise = Mathf.Lerp(noise01, mountainNoise, 0.5f);

        tile.transform.position += new Vector3(0f, hexHeight / 2f, 0f);

        Material hexMaterial = new Material(Shader.Find("Standard"));
        hexMaterial.CopyPropertiesFromMaterial(material);
        hexMaterial.color = gradient.Evaluate(hexNoise);

        HexRenderer hexRenderer = tile.GetComponent<HexRenderer>();
        hexRenderer.SetMaterial(hexMaterial);
        
        hexRenderer.outerSize = outerSize;
        hexRenderer.innerSize = innerSize;
        hexRenderer.height = hexHeight;
        hexRenderer.isFlatTopped = isFlatTopped;
        
        hexRenderer.DrawMesh();
        
        hexes.Add(new GridUnit(index, hexRenderer));
        
        MeshCollider meshCollider = tile.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = hexRenderer.mesh;
    }

    public GridUnit GetHexFromIndex(int index) {
        return hexes.Find(hex => hex.index == index);
    }
}