using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class NoticeText : MonoBehaviour
{
    [Header("Notice Colors")]
    public Color errorColor = Color.red;
    public Color successColor = Color.green;
    public Color waitColor = Color.yellow;

    private TextMeshProUGUI text;

    void Awake() {
        text = GetComponent<TextMeshProUGUI>();
    }

    public void SetError(string a_Text) {
        text.text = a_Text;
        text.color = errorColor;
    }

    public void SetSuccess(string a_Text) {
        text.text = a_Text;
        text.color = successColor;
    }

    public void SetWait(string a_Text) {
        text.text = a_Text;
        text.color = waitColor;
    }
}
