using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Pulse : MonoBehaviour
{
    [Header("Color Settings")]
    public Color color1;
    public Color color2;

    private TextMeshProUGUI text;

    void Awake() {
        text = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        text.color = Color32.Lerp(color1, color2, Mathf.PingPong(Time.time, 1));
    }
}
