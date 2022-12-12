using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;

public class GameOverGroup : MonoBehaviour
{
    public TextMeshProUGUI winText;
    public TextMeshProUGUI methodText;
    public TextMeshProUGUI moneyText;
    public TextMeshProUGUI lifeText;
    public Image background;

    public Color winColor;
    public Color loseColor;

    public Gradient backgroundGradient;
    public TimeSpan fadeDuration;
    private float fadeStartTime;

    public string winTextString;
    public string loseTextString;

    void OnEnable() {
        fadeStartTime = Time.time;

        var chat = GameObject.Find("Chat");
        chat.transform.SetAsLastSibling();
        chat.transform.position = new Vector3(-1925f, -1047.5f, 0f);
    }

    public void SetResult(bool win, string method, int money, int life) {
        if (win) {
            winText.text = winTextString;
            winText.color = winColor;
        } else {
            winText.text = loseTextString;
            winText.color = loseColor;
        }

        methodText.text = "(by " + method + ")";
        moneyText.text = $"Total Credits Earned:\n<size=100><b><color=#E0BA06>{(money).ToString()}¢</color>";
        lifeText.text = $"Health Remaining:\n<size=100><b><color=#933042>{(life / PersonalInfo.maxLife * 100f).ToString()}%</color>";
    } 
    
    void Update()
    {
        // apply gradient to background
        background.color = backgroundGradient.Evaluate(Mathf.Clamp01((Time.time - fadeStartTime) / (float)fadeDuration.TotalSeconds));
    }
}
