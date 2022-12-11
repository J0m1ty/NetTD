using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class Communicator : MonoBehaviour
{
    [Header("Username Info")]
    public TextMeshProUGUI nameText;

    [Header("Chat Settings")]
    public TMP_InputField inputField;
    public Transform textContent;
    public GameObject textPrefab;

    public Transform userTextContent;
    public GameObject userTextPrefab;

    public int welcomes = 0;

    [Header("Room Settings")]
    public GameObject roomInfo;
    public TMP_InputField roomIdDisplay;
    public Transform roomPlayersContent;
    public GameObject roomPlayerTilePrefab;
    public TMP_InputField roomField;
    public bool mainScene = false;

    void Start()
    {
        UpdateName();

        welcomes = 0;

        if (mainScene) {
            this.JoinRoom(WSClient.instance.mainRoomId);
        }
    }

    public void UpdateName() {
        var name = WSClient.instance?.player?.username;

        if (!nameText) return;

        nameText.text = string.IsNullOrEmpty(name) ? "Username" : name;
    }

    public void Logout() {
        WSClient.instance.Logout();
    }

    public void HostRoom() {
        WSClient.instance.HostRoom();
    }

    public void JoinRoom() {
        var roomId = roomField.text;
        if (roomId == "MAIN") {return;}
        JoinRoom(roomId);
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

    public void StartMatch() {
        WSClient.instance.StartMatch();
    }

    public void SubmitMessage() {
        var message = inputField.text;

        if (string.IsNullOrEmpty(message)) return;

        WSClient.instance.EmitMessage(message);

        WriteMessage(WSClient.instance.player?.username, message, WSClient.instance.player?.usernameColor);

        inputField.text = "";

        inputField.Select();
    }

    public void WriteMessage(string username, string message, string hex = "#FFFFFF") {
        if (textContent == null || textPrefab == null) return;
        
        if (username == "Server" && message.StartsWith("Welcome")) {
            welcomes++;
            if (welcomes > 1) return;
        }
        
        var text = Instantiate(textPrefab, textContent);
        text.GetComponent<TextMeshProUGUI>().text = $"<color={hex}>{username}: {message}</color>";
    }

    public void ClearUsers() {
        if (userTextContent == null) return;

        foreach (Transform child in userTextContent) {
            Destroy(child.gameObject);
        }
    }

    public void AddUser(string username, string hex = "#FFFFFF") {
        if (userTextContent == null || userTextPrefab == null) return;
        
        var text = Instantiate(userTextPrefab, userTextContent);
        text.GetComponent<TextMeshProUGUI>().text = $"<color={hex}>{username}</color>";
    }

    public void SetRoomInfo(bool to, string roomId = "") {
        if (roomInfo == null || roomIdDisplay == null) return;

        roomIdDisplay.text = roomId;

        roomInfo.SetActive(to);
    }

    public void ClearRoomPlayers() {
        if (roomPlayersContent == null) return;

        foreach (Transform child in roomPlayersContent) {
            if (child.name == "PlayerCount") continue;
            Destroy(child.gameObject);
        }
    }

    public void AddRoomPlayer(string username, string hex = "#FFFFFF") {
        if (roomPlayersContent == null || roomPlayerTilePrefab == null) return;
        
        var text = Instantiate(roomPlayerTilePrefab, roomPlayersContent);
        text.GetComponentInChildren<TextMeshProUGUI>().text = $"<color={hex}>{username}</color>";
    }
}
