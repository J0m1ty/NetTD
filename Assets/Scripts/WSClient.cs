using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MyBox;

using SocketIOClient;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Dynamic;
using Newtonsoft.Json;

public class WSClient : MonoBehaviour
{
    public static WSClient instance;

    private SocketIOUnity socket;

    [Header("SocketIO")]
    public string url = "http://192.168.1.20:3000";

    [Header("Auth Settings")]
    public NoticeText noticeText;
    public SceneReference registerScene;
    public SceneReference nextScene;
    public int timeout;

    [Header("Player Info")]
    public string savePath = "saveData.dat";
    
    private bool isAuth;
    private bool checkingSaved;
    private PlayerData player;

    private Queue<Action> jobs = new Queue<Action>();

    void Awake() {
        if (instance != null) {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        isAuth = false;
        checkingSaved = false;

        var uri = new System.Uri(url);

        socket = new SocketIOUnity(uri, new SocketIOOptions {
            Query = new Dictionary<string, string> {
                { "token", "UNITY" }
            }
        });

        socket.Connect();
        
        socket.OnConnected += (sender, e) => {
            WSClient.instance.AddJob(() => {
                CheckSavedPlayer();
            });
        };
    }

    private void CheckSavedPlayer() {
        if (isAuth || checkingSaved) {return;}

        var saved = LoadJsonData();
        
        if (saved != null) {
            checkingSaved = true;
            Auth(saved, noticeText);
        }
    }

    void Update() {
        if (!isAuth && SceneManager.GetActiveScene().name != registerScene.SceneName && !checkingSaved) {
            player = null;
            SceneManager.LoadScene(registerScene.SceneName);
        }
        else if (isAuth && player != null && !checkingSaved && SceneManager.GetActiveScene().name == registerScene.SceneName) {
            SceneManager.LoadScene(nextScene.SceneName);
        }
        
        while (jobs.Count > 0) {
            jobs.Dequeue().Invoke();
        }
    }

    public void Register() {
        var inputField = GameObject.Find("InputField").GetComponent<TMP_InputField>();
        Register(inputField.text, noticeText);
    }

    async private void Register(string a_Name, NoticeText a_Notice) {
        a_Name = a_Name.Trim();

        a_Notice.SetWait("Registering...");
        player = null;
        isAuth = false;
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(a_Name, @"^[a-zA-Z0-9_]+$")) {
            a_Notice.SetError("Invalid name characters");
            return;
        }
        
        if (a_Name.Length < 3 || a_Name.Length > 12) {
            a_Notice.SetError("Name too long or short");
            return;
        }

        try {
            var registerTask = socket.EmitAsync("register", (response) => {
                WSClient.instance.AddJob(() => {
                    var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                        error = string.Empty,
                        player = new Dictionary<string, string>()
                    });

                    if (result.error != null && result.error.Length > 0) {
                        a_Notice.SetError(result.error);
                    }
                    else if (result.player != null && result.player.Count > 0) {
                        Auth(new PlayerData(result.player["id"], result.player["name"]), a_Notice);
                    }
                    else {
                        a_Notice.SetError("Register error");
                    }
                });
                
            }, new {name = a_Name});

            await Task.WhenAny(registerTask, Task.Delay(TimeSpan.FromMilliseconds(timeout)));
        }
        catch (Exception) {
            a_Notice.SetError("Request Timeout");
            return;
        }
    }

    async private void Auth(PlayerData unauthorizedPlayer, NoticeText a_Notice) {
        try {
            var authTask = socket.EmitAsync("auth", (response) => {
                WSClient.instance.AddJob(() => {
                    checkingSaved = false;

                    var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                        error = string.Empty,
                        player = new Dictionary<string, string>()
                    });

                    if (result.error != null && result.error.Length > 0) {
                        a_Notice.SetError(result.error);
                    }
                    else if (result.player != null && result.player.Count > 0) {
                        player = unauthorizedPlayer;
                        player.id = result.player["id"];
                        player.name = result.player["name"];

                        a_Notice.SetSuccess("Success!");

                        isAuth = true;

                        SaveJsonData(player);
                        
                        SceneManager.LoadScene(nextScene.SceneName);
                    }
                    else {
                        a_Notice.SetError("Auth error");
                    }
                });
            }, new { id = unauthorizedPlayer.id, name = unauthorizedPlayer.name });

            await Task.WhenAny(authTask, Task.Delay(TimeSpan.FromMilliseconds(timeout)));
        } 
        catch (Exception) {
            a_Notice.SetError("Request Timeout");
            checkingSaved = false;
            return;
        }
    }

    private bool SaveJsonData(PlayerData a_PlayerData) {
        return FileManager.WriteToFile(savePath, a_PlayerData.ToJSON());
    }

    private PlayerData LoadJsonData() {
        if (FileManager.LoadFromFile(savePath, out var json)) {
            return new PlayerData(json);
        }
        else {
            return null;
        }
    }

    internal void AddJob(Action newJob) {
        jobs.Enqueue(newJob);
    }
}
