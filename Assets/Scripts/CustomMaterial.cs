using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomMaterial : MonoBehaviour
{
    public Shader shader;

    void OnEnable() {
        var image = GetComponent<UnityEngine.UI.Image>();
        // get sprite
        var sprite = image.sprite;
        // get material of spirte
        
    }
}
