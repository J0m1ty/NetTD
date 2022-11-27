using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using SocketIOClient;

public class WSClient : MonoBehaviour
{
    public SocketIOUnity socket;

    [Header("SocketIO")]
    public string url = "http://192.168.1.20:3000";

    void Start() {
        var uri = new System.Uri(url);

        socket = new SocketIOUnity(uri);

        socket.Emit("unity", "Hello from Unity");

        socket.On("someConnection", (response) => {
            var obj = response.GetValue();
            Debug.Log("someConnection");
            Debug.Log("obj: " + obj.ToString());
        });

        socket.Connect();
    }

    void Update() {
        
    }
}
