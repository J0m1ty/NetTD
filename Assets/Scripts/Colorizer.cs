using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Colorizer : MonoBehaviour
{
    private MeshRenderer mr;

    public Color color;

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
    }

    void Start()
    {
        SetAllColor(color);
    }

    public void SetAllColor(Color color) {
        this.color = color;

        SetColor(color);
        SetChildrenColor(color);
    }

    public void SetColor(Color color)
    {
        if (mr == null) return;

        Material newMat = new Material(mr.material);
        newMat.color = color;
        mr.material = newMat;
    }

    public void SetChildrenColor(Color color) {
        MeshRenderer[] mrs = GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer meshRenderer in mrs) {
            Material newMat = new Material(meshRenderer.material);
            newMat.color = color;
            meshRenderer.material = newMat;
        }
    }
}
