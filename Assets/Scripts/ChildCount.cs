using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class ChildCount : MonoBehaviour
{
    public TextMeshProUGUI text;

    void Start()
    {
        text = GetComponent<TextMeshProUGUI>();
    }

    void Update() {
        var count = transform.parent.childCount - 1;
        text.text = "Players: " + count;
    }
}
