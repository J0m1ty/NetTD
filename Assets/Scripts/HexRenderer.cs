
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public struct Face 
{
    public List<Vector3> vertices { get; private set; }
    public List<int> triangles { get; private set; }
    public List<Vector2> uvs { get; private set;}

    public Face(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs) {
        this.vertices = vertices;
        this.triangles = triangles;
        this.uvs = uvs;
    }
}

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class HexRenderer : MonoBehaviour
{
    public Mesh m_mesh { get; private set; }
    private MeshFilter m_meshFilter;
    private MeshRenderer m_meshRenderer;
    private List<Face> m_faces;

    [Header("Hex Info")]
    public Material material;
    public Color hexColor;
    public float innerSize;
    public float outerSize;
    public float height;
    public bool isFlatTopped;

    [Header("Game Info")]
    public int index;
    
    public TurretScript turret;

    public Vector3 position {
        get {
            return transform.position;
        }
        private set {
            transform.position = value;
        }
    }

    private void Awake() {
        m_meshFilter = GetComponent<MeshFilter>();
        m_meshRenderer = GetComponent<MeshRenderer>();

        m_mesh = new Mesh();
        m_mesh.name = "Hex";

        m_meshFilter.mesh = m_mesh;

        if (hexColor != null) {
            Material hexMaterial = new Material(Shader.Find("Standard"));
            if (material != null) {
                hexMaterial.CopyPropertiesFromMaterial(this.material);
            }
            
            hexMaterial.color = hexColor;

            SetMaterial(hexMaterial);
        }
        else {
            SetMaterial(material);
        }
    }
    
    private void OnEnable() {
        DrawMesh();
    }

    public void DrawMesh() {
        DrawFaces();
        CombineFaces();
    }

    public void SetMaterial(Material material) {
        if (material == null) {
            return;
        }

        this.material = material;
        m_meshRenderer.material = material;
    }

    private void DrawFaces() {
        m_faces = new List<Face>();

        // Top faces
        for (int point = 0; point < 6; point++) {
            m_faces.Add(CreateFace(innerSize, outerSize, height / 2f, height / 2f, point));
        }

        // Bottom faces
        for (int point = 0; point < 6; point++) {
            m_faces.Add(CreateFace(innerSize, outerSize, - height / 2f, - height / 2f, point, true));
        }

        // Outer faces
        for (int point = 0; point < 6; point++) {
            m_faces.Add(CreateFace(outerSize, outerSize, height / 2f, - height / 2f, point, true));
        }

        // Inner faces
        for (int point = 0; point < 6; point++) {
            m_faces.Add(CreateFace(innerSize, innerSize, height / 2f, - height / 2f, point));
        }
    }

    private Face CreateFace(float innerRad, float outerRad, float heightA, float heightB, int point, bool reverse = false) {

        Vector3 pointA = GetPoint(innerRad, heightB, point);
        Vector3 pointB = GetPoint(innerRad, heightB, (point < 5) ? point + 1 : 0);
        Vector3 pointC = GetPoint(outerRad, heightA, (point < 5) ? point + 1 : 0);
        Vector3 pointD = GetPoint(outerRad, heightA, point);

        List<Vector3> vertices = new List<Vector3>() { pointA, pointB, pointC, pointD };
        List<int> triangles = new List<int>() { 0, 1, 2, 2, 3, 0 };
        List<Vector2> uvs = new List<Vector2>() { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };

        if (reverse) {
            vertices.Reverse();
        }

        return new Face(vertices, triangles, uvs);
    }

    protected Vector3 GetPoint(float size, float height, int index) {
        float angle_deg = isFlatTopped ? 60 * index : 60 * index - 30;
        float angle_rad = Mathf.PI / 180f * angle_deg;
        return new Vector3((size * Mathf.Cos(angle_rad)), height, (size * Mathf.Sin(angle_rad)));
    }

    private void CombineFaces() {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int i = 0; i < m_faces.Count; i++) {
            // Add verticies
            vertices.AddRange(m_faces[i].vertices);
            uvs.AddRange(m_faces[i].uvs);

            // Offset triangles
            int offset = (4 * i);
            foreach (int triangle in m_faces[i].triangles) {
                triangles.Add(triangle + offset);
            }
        }

        m_mesh.vertices = vertices.ToArray();
        m_mesh.triangles = triangles.ToArray();
        m_mesh.uv = uvs.ToArray();
        m_mesh.RecalculateNormals();
        m_mesh.RecalculateBounds();
    }
}
