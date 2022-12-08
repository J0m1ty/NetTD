using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseColorScript : MonoBehaviour
{
    public Color baseColor;

    void Start()
    {
        SetColor();
    }

    public void SetColor() {
        SetColor(baseColor);
    }

    public void SetColor(Color nc) {
        baseColor = nc;

        foreach (Transform child in transform)
        {
            var newMat = new Material(Shader.Find("Standard"));
            newMat.CopyPropertiesFromMaterial(child.GetChild(0).GetComponent<MeshRenderer>().material);
            newMat.color = baseColor;
            child.GetChild(0).GetComponent<MeshRenderer>().material = newMat;
        }
    }
}
