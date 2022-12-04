using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Communicator : MonoBehaviour
{
    public TextMeshProUGUI nameText;

    public TMP_InputField inputField;
    public Transform textContent;
    public GameObject textPrefab;

    void Start()
    {
        UpdateName();
    }

    public void UpdateName() {
        var name = WSClient.instance.player?.username;

        if (!nameText) return;

        nameText.text = string.IsNullOrEmpty(name) ? "Username" : name;
    }

    public void Logout() {
        WSClient.instance.Logout();
    }

    public void HostRoom() {
        WSClient.instance.HostRoom();
    }

    public void JoinRoom(string id) {
        WSClient.instance.JoinRoom(id);
    }

    public void Register() {
        WSClient.instance.Register();
    }

    public void Continue() {
        WSClient.instance.Continue();
    }

    public void SubmitMessage() {
        var message = inputField.text;

        if (string.IsNullOrEmpty(message)) return;

        WSClient.instance.EmitMessage(message);

        WriteMessage(WSClient.instance.player?.username, message, WSClient.instance.player?.color);

        inputField.text = "";

        inputField.Select();
    }

    public void WriteMessage(string username, string message, string hex = "#FFFFFF") {
        if (textContent == null || textPrefab == null) return;

        // make new text
        var text = Instantiate(textPrefab, textContent);
        text.GetComponent<TextMeshProUGUI>().text = $"<color={hex}>{username}: {message}</color>";
    }
}
