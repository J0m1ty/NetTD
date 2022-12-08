using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Colorizer : MonoBehaviour
{
    public MeshRenderer mr;

    void Awake()
    {
        mr = GetComponent<MeshRenderer>();
    }

    public void SetColor(Color color)
    {
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
