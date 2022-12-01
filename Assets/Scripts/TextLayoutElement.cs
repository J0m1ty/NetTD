using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(LayoutElement))]
[RequireComponent(typeof(TextMeshProUGUI))]
public class TextLayoutElement : MonoBehaviour
{
    public int maxChars;
    public float m_maxWidth;
    public float m_maxHeight;

    private UnityEngine.UI.LayoutElement layoutElement;
    private TextMeshProUGUI text;

    private void Awake()
    {
        layoutElement = GetComponent<UnityEngine.UI.LayoutElement>();
        text = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        layoutElement.preferredWidth = Mathf.Min(text.preferredWidth, m_maxWidth);
        layoutElement.preferredHeight = Mathf.Min(text.preferredHeight, m_maxHeight);

        if (maxChars > 0)
        {
            text.text = text.text.Substring(0, Mathf.Min(text.text.Length, maxChars));
        }
    }
}
