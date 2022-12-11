using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;

using SocketIOClient;
using System;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;
using Newtonsoft.Json;
using System.Text;
using System.Runtime.CompilerServices;
using UnityEngine.UI;
using System.Text.RegularExpressions;
using TMPro;

public class WSClient : MonoBehaviour
{
    // Singleton
    public static WSClient instance;

    // Socket.io
    private SocketIOUnity socket;

    // Allows async tasks to be executed on the main thread (for Unity)
    private Queue<Action> jobs = new Queue<Action>();

    [Header("SocketIO")]
    public string url = "http://192.168.1.20:3000"; // server IP address
    public int timeoutMS; // server request delay
    public int delayMS; // server request delay

    public Communicator communicator { 
        get {
            return GameObject.Find("WSCommunicator")?.GetComponent<Communicator>();
        }
    }

    [Header("Auth Settings")]
    public GameObject authPopup; // if not in a register scene, show popup
    public GameObject disconnectPopup; // if currently disconnected, show popup
    public static bool isInputEnabled = true; // singleton to disable input while auth
    private bool isAuth; // is the player authenticated
    private bool checkingSaved; // is async memory being checked
    private bool disconnectError; // show disconnect error next time register scene is updated
    private bool safeDisconnecting; // is the player disconnecting

    // todo: make job manager (only allow registering or auth, and cancel other jobs, and timeouts)

    // get/set event for registration UI display modes 
    public RegisterStep registerStep {
        get { return _registerStep; }
        set {
            _registerStep = value;

            if (SceneManager.GetActiveScene().name == registerScene) {
                var rg = GameObject.Find("RegisterGroup").GetComponent<RegisterGroup>();

                var children = rg.fieldGroup.transform.GetComponentsInChildren<Button>();

                switch (_registerStep) {
                    case RegisterStep.Pending:
                        rg.submitButton.interactable = false;
                        rg.submitButton.gameObject.SetActive(true);
                        rg.continueButton.interactable = false;
                        rg.continueButton.gameObject.SetActive(false);
                        Array.ForEach(children, x => {x.interactable = false;});
                        rg.fieldGroup.gameObject.SetActive(true);
                        break;
                    case RegisterStep.Success:
                        rg.submitButton.interactable = false;
                        rg.submitButton.gameObject.SetActive(false);
                        rg.continueButton.interactable = true;
                        rg.continueButton.gameObject.SetActive(true);
                        Array.ForEach(children, x => {x.interactable = false;});
                        rg.fieldGroup.gameObject.SetActive(false);
                        break;
                    case RegisterStep.Failure:
                    default:
                        rg.submitButton.interactable = true;
                        rg.submitButton.gameObject.SetActive(true);
                        rg.continueButton.interactable = false;
                        rg.continueButton.gameObject.SetActive(false);
                        Array.ForEach(children, x => {x.interactable = true;});
                        rg.fieldGroup.gameObject.SetActive(true);
                        break;
                }
            }
        }
    }
    private RegisterStep _registerStep;

    [Header("Player Info")]
    public string savePath = "saveData.dat"; // save file
    public PlayerData player { get; private set; }  // player data

    [Header("Room Info")]
    public string mainRoomId = "MAIN"; // main room id
    private string currentRoomId = ""; // current room id
    private int numUsers = 0; // number of users in current room (not main)
    public GameObject transitionPopup; // transition popup

    // Scene references
    [Header("Scene Info")]
    public SceneWrapper[] scenes;
    private string registerScene;
    private string menuScene;
    private string gameScene;

    // Handles custom scene info
    [Serializable]
    public struct SceneWrapper {
        public SceneReference scene;
        public bool requireAuth;
        public SceneType type;
    }

    // Types of scene
    [Serializable]
    public enum SceneType {
        Normal,
        Auth,
        Menu,
        Game
    }
    
    // Possible registration steps
    [Serializable]
    public enum RegisterStep {
        None,
        Pending,
        Success,
        Failure
    }

    // Events
    private void Awake() {
        if (instance != null) {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        ConnectSocket();
    }

    private void OnEnable() {
        isAuth = false;
        checkingSaved = false;
        registerStep = RegisterStep.None;
        
        foreach (var s in scenes) {
            if (s.type == SceneType.Auth) {
                registerScene = s.scene.SceneName;
            } else if (s.type == SceneType.Menu) {
                menuScene = s.scene.SceneName;
            }
            else if (s.type == SceneType.Game) {
                gameScene = s.scene.SceneName;
            }
        }

        /** scene loaded event **/
        SceneManager.sceneLoaded += OnSceneLoaded;

        /** socket events **/
        SocketEvents();
        
        /** connection events **/
        socket.OnConnected += (sender, e) => {
            WSClient.instance.AddJob(() => {
                CheckSavedPlayer();
            });
        };

        socket.OnDisconnected += (sender, e) => {
            WSClient.instance.AddJob(() => {
                if (!safeDisconnecting) {
                    EnableDisconnectPopup();
                }
                safeDisconnecting = false;
            });
        };

        socket.OnReconnectAttempt += (sender, e) => {
            WSClient.instance.AddJob(() => {
                EnableDisconnectPopup();
                Debug.Log($"Reconnecting... {e}");
            });
        };

        socket.OnReconnectFailed += (sender, e) =>
        {
            WSClient.instance.AddJob(() => {
                DisableDisconnectPopup();
                DisconnectSocket();
                disconnectError = true;
                SceneManager.LoadScene(registerScene);
            });
        };

        socket.OnReconnected += (sender, e) => {
            WSClient.instance.AddJob(() => {
                DisableDisconnectPopup();

                if (player != null) {
                    Debug.Log("Reconnected, sending auth");

                    isAuth = false;
                    checkingSaved = false;
                    CheckSavedPlayer(optionalData: player);
                }
            });
        };

        socket.OnAnyInUnityThread((name, response) =>
        {
            Debug.Log("Received On " + name + " : " + response.GetValue().GetRawText());
        });
    }

    private void OnDisable() {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnApplicationQuit() {
        DisconnectSocket();
    }

    private void Update() {
        if (!isAuth && SceneAuth(SceneManager.GetActiveScene().name)) {
            SceneManager.LoadScene(registerScene);
            return;
        }

        if (disconnectError && SceneManager.GetActiveScene().name == registerScene) {
            var rg = GameObject.Find("RegisterGroup")?.GetComponent<RegisterGroup>()?.noticeText;

            if (rg != null) {
                rg.SetError("Server disconnect");
            }

            disconnectError = false;
            return;
        }

        while (jobs.Count > 0) {
            jobs.Dequeue().Invoke();
        }
    }
    
    private void SocketEvents() {
        /** message event **/
        socket.On("message", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("received message: " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {username: string, message: string, roomId: string, timestamp: string}
                // users : [{username: string}]

                if (result?.data?["roomId"] == "MAIN" && SceneManager.GetActiveScene().name != menuScene) {
                    return;
                }
                else if (result?.data?["roomId"] != "MAIN" && result?.data?["roomId"] != currentRoomId) {
                    return;
                }
                else if (SceneManager.GetActiveScene().name == menuScene && (result?.data?["username"].Equals("Server") ?? false) && (result?.data?["message"].Contains("has joined") ?? false)) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("received message err " + result.error);
                } else {
                    Debug.Log("received message data " + result.data);

                    UpdateUsernames(result.data["roomId"], result.users);

                    if (result.data["username"] == player.username) {
                        return;
                    }
                    communicator.WriteMessage(result.data["username"], result.data["message"]);
                }
            });
        });

        /** update users event **/
        socket.On("users", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("users: " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {roomId: string}
                // users : [{username: string}]

                if ((result?.data?["roomId"] == "MAIN" && SceneManager.GetActiveScene().name != menuScene) || (result?.data?["roomId"] != "MAIN" && result?.data?["roomId"] != currentRoomId)) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("users err " + result.error);
                } else {
                    UpdateUsernames(result.data["roomId"], result.users);
                    
                    Debug.Log("users data " + result.data.ToString());
                }
            });
        });

        /** start event **/
        socket.On("start", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("start: " + response.ToString());

                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {roomId: string}
                // users : [{username: string}]

                if (result?.data?["roomId"] == "MAIN" || SceneManager.GetActiveScene().name != menuScene || result?.data?["roomId"] != currentRoomId) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("start err " + result.error);
                } else {
                    UpdateUsernames(result.data["roomId"], result.users);
                    
                    StartCoroutine(StartTransition(seconds: 3, to: gameScene));
                    
                    Debug.Log("start data " + result.data.ToString());
                }
            });
        });
    
        /** allReady event **/
        socket.On("allReady", (response) => {
            WSClient.instance.AddJob(async () => {
                Debug.Log("allReady: " + response.ToString());

                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    bases = new Dictionary<string, int>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {roomId: string }
                // bases : {friendlyBase: int, enemyBase: int}
                // users : [{username: string}]

                if (SceneManager.GetActiveScene().name != gameScene) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("allReady err " + result.error);
                } else {
                    UpdateUsernames(result.data["roomId"], result.users);

                    int friendly = result.bases["friendlyBase"];
                    int enemy = result.bases["enemyBase"];

                    GameManager.instance.SetBases(friendly: friendly, enemy: enemy);

                    await Task.Delay(2000);

                    StartCoroutine(StartTransition(seconds: 3, callback: () => {
                        Debug.Log("enable input");
                        GameManager.instance.DisablePopup(GameManager.instance.waitingPopupPrefab);
                    }));
                }
            });
        });

        /** setTowers event **/
        socket.On("setTowers", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("setTowers: " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    towerData = new List<Dictionary<string, int>>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {roomId: string}
                // towerData: [{index: int, team: int, type: int}]
                // users : [{username: string}]

                if (result?.data?["roomId"] != currentRoomId) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("setTowers err " + result.error);
                } else {
                    Debug.Log("setTowers data " + result.data.ToString());

                    UpdateUsernames(result.data["roomId"], result.users);

                    var easyTowers = new List<EasyTower>();

                    foreach (var tower in result.towerData) {
                        var easyTower = new EasyTower(tower["index"], (TowerType)tower["type"], (TeamType)tower["team"]);
                        easyTowers.Add(easyTower);
                    }

                    GameManager.instance.UpdateTowers(easyTowers, true);
                }
            });
        });
    }

    private async void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
        if (scene.name == gameScene) {
            // emit ready event
            await socket.EmitAsync("ready", (response) => {
                WSClient.instance.AddJob(() => {
                    // get error back
                    var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                        error = string.Empty,
                        data = new Dictionary<string, string>(),
                        users = new List<Dictionary<string, string>>()
                    });

                    // error : string
                    // data : {roomId: string}
                    // users : [{username: string}]

                    if (result?.data?["roomId"] == "MAIN" || SceneManager.GetActiveScene().name != gameScene || result?.data?["roomId"] != currentRoomId) {
                        return;
                    }

                    if (!string.IsNullOrEmpty(result.error)) {
                        Debug.Log("ready err " + result.error);
                    } else {
                        UpdateUsernames(result.data["roomId"], result.users);
                        
                        Debug.Log("ready data " + result.data.ToString());
                    }
                });
            }, new { roomId = currentRoomId });
        }
    }

    // Methods
    private void ConnectSocket() {
        var uri = new System.Uri(url);

        socket = new SocketIOUnity(uri, new SocketIOOptions {
            Query = new Dictionary<string, string> {
                { "token", "UNITY" }
            },
            ConnectionTimeout = TimeSpan.FromMilliseconds(timeoutMS),
            ReconnectionAttempts = 3,
        });

        socket.Connect();
    }
    
    private void DisconnectSocket() {
        isAuth = false;
        player = null;
        checkingSaved = false;
        registerStep = RegisterStep.None;
        jobs.Clear();
        socket?.Disconnect();
    }
    
    public async void HostRoom() {
        Debug.Log("HostRoom");
        await socket.EmitAsync("hostRoom", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("host room " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {roomId : string}
                // users : [{username : string}]

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("host err: " + result.error);
                } else {
                    currentRoomId = result.data["roomId"];

                    UpdateUsernames(result.data["roomId"], result.users);

                    Debug.Log("host data: " + result.data);
                }
            });
        });
    }

    public async void JoinRoom(string id) {
        Debug.Log("JoinRoom" + " " + id);

        var inputId = id.ToUpper();
        
        if (inputId.Length != 4 || !Regex.IsMatch(inputId, @"^[a-zA-Z0-9]+$")) {
            Debug.Log("Invalid room ID");
            return;
        }
        
        await socket.EmitAsync("joinRoom", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("join room " + response.ToString());

                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data: {roomId : string}
                // users: [{username : string}]

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("join:err " + result.error);
                } else {
                    currentRoomId = result.data["roomId"];

                    UpdateUsernames(result.data["roomId"], result.users);

                    Debug.Log("join:data " + result.data.ToString());
                }
            });
        }, new { roomId = inputId });
    }

    public async void StartMatch() {
        if (SceneManager.GetActiveScene().name != menuScene) {
            return;
        }

        if (currentRoomId == "MAIN") {
            Debug.Log("Not in a valid room");
            return;
        }

        if (numUsers < 2) {
            Debug.Log("Not enough players");
            return;
        }

        await socket.EmitAsync("startMatch", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("start match " + response.ToString());

                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data: { roomId : string }
                // users: [{ username : string }]

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("start:err " + result.error);
                } else {
                    UpdateUsernames(result.data["roomId"], result.users);

                    Debug.Log("start:data " + result.data.ToString());
                }
            });
        }, new { roomId = currentRoomId });
    }

    private IEnumerator StartTransition(int seconds = 3, string to = "", Action callback = null) {
        // show transitionPopup
        var popup = Instantiate(transitionPopup, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
        popup.transform.SetParent(GameObject.Find("Canvas").transform, false);
        popup.name = "TransitionPopup";
        
        isInputEnabled = false;

        var text = popup.GetComponentInChildren<TextMeshProUGUI>();

        for (int i = Mathf.Clamp(seconds, 0, 10); i > 0; i--) {
            text.text = i.ToString();
            yield return new WaitForSeconds(1);
        }

        isInputEnabled = true;

        Destroy(popup);

        callback?.Invoke();

        if (!string.IsNullOrEmpty(to)) {
            SceneManager.LoadScene(to);
        }
    }

    public async void EmitMessage(string message) {
        if (SceneManager.GetActiveScene().name == menuScene) {
            await EmitMessage(message, "MAIN");
        }
        else if (!String.IsNullOrEmpty(currentRoomId)) {
            await EmitMessage(message, currentRoomId);
        }
        else {
            Debug.Log("No room id");
        }
    }

    private async Task EmitMessage(string message, string roomId) {
        if (message.Trim().Length > 100 || message.Trim().Length <= 1) {
            Debug.Log("Message too long or too short");
            return;
        }

        Debug.Log("EmitMessage" + " " + message + " " + roomId);

        await socket.EmitAsync("message", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("message callback: " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {username: string, message: string, roomId: string, timestamp: string}
                // users : [{username: string}]

                if ((result?.data?["roomId"] == "MAIN" && SceneManager.GetActiveScene().name != menuScene) || (result?.data?["roomId"] != "MAIN" && result?.data?["roomId"] != currentRoomId)) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("message callback err " + result.error);
                } else {
                    Debug.Log("message callback data " + result.data.ToString());

                    UpdateUsernames(result.data["roomId"], result.users);

                    if (result.data["username"] == player.username) {
                        return;
                    }

                    communicator.WriteMessage(result.data["username"], result.data["message"]);
                }
            });
        }, new { roomId = roomId, message = message });
    }

    public async void EmitTowers(List<EasyTower> towers) {
        if (SceneManager.GetActiveScene().name != gameScene) {
            return;
        }

        if (towers.Count == 0) {
            return;
        }

        if (string.IsNullOrEmpty(currentRoomId)) {
            Debug.Log("No room id");
            return;
        }

        var towerData = new List<Dictionary<string, string>>();

        foreach (var t in towers) {
            towerData.Add(new Dictionary<string, string>() {
                { "index", t.index.ToString() },
                { "team", t.team.ToString() },
                { "type", t.type.ToString() },
            });
        }

        await socket.EmitAsync("towers", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("towers callback: " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>(),
                    users = new List<Dictionary<string, string>>()
                });

                // error : string
                // data : {roomId: string}
                // users : [{username: string}]

                if (result?.data?["roomId"] != currentRoomId) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("towers callback err " + result.error);
                } else {
                    Debug.Log("towers callback data " + result.data.ToString());

                    UpdateUsernames(result.data["roomId"], result.users);
                }
            });
        }, new { roomId = currentRoomId, towerData = towerData });
    }

    private void UpdateUsernames(string targetRoom, List<Dictionary<string, string>> users) {
        Debug.Log("target " + targetRoom + " ||| current " + currentRoomId);

        if (targetRoom == mainRoomId) {
            if (SceneManager.GetActiveScene().name == menuScene) {

                // in menu, refering to main room, safe to update chat users
                if (users.Count > 0) {
                    communicator.ClearUsers();
                    foreach (var u in users) {
                        communicator.AddUser(u["username"]);
                    }
                }

                if (currentRoomId == mainRoomId) {
                    // in menu, refering to main room, and in main room
                    communicator.SetRoomInfo(false, mainRoomId);
                }
                else {
                    // in menu, refering to main room, but not in main room
                    communicator.SetRoomInfo(true, currentRoomId);
                }
            }
        }
        else if (targetRoom == currentRoomId) {
            if (SceneManager.GetActiveScene().name == menuScene) {
                // in menu, but not refering to main room, have to update room info (not chat)
                communicator.SetRoomInfo(true, currentRoomId);
                
                numUsers = users.Count;

                if (users.Count > 0) {
                    communicator.ClearRoomPlayers();
                    foreach (var u in users) {
                        communicator.AddRoomPlayer(u["username"]);
                    }
                }
            }
            else {
                // Not in menu, not refering to main room, safe to update chat users
                if (users.Count > 0) {
                    communicator.ClearUsers();
                    foreach (var u in users) {
                        communicator.AddUser(u["username"]);
                    }
                }
            }
        }
    }
    
    private void EnableDisconnectPopup() {
        if (GameObject.Find("DisconnectPopup") == null) {
            var popup = Instantiate(disconnectPopup, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
            popup.transform.SetParent(GameObject.Find("Canvas").transform, false);
            popup.name = "DisconnectPopup";
        }
        isInputEnabled = false;
    }

    private void DisableDisconnectPopup() {
        var popup = GameObject.Find("DisconnectPopup");
        if (popup != null) {
            Destroy(popup);
        }
        isInputEnabled = true;
    }

    public void LeaveGame() {
        if (SceneManager.GetActiveScene().name == menuScene) {
            Logout();
        }
        else {
            JoinRoom(mainRoomId);
            SceneManager.LoadScene(menuScene);
        }
    }

    public void Logout() {
        safeDisconnecting = true;
        DeleteJsonData();
        DisconnectSocket();
        SceneManager.LoadScene(registerScene);
        ConnectSocket();
    }
    
    private async void CheckSavedPlayer(PlayerData optionalData = null) {
        if (optionalData == null && (isAuth || checkingSaved)) {return;}

        var saved = optionalData != null ? optionalData : LoadJsonData();

        Debug.Log(saved);
        
        if (saved != null) {
            checkingSaved = true;

            var currentSceneName = SceneManager.GetActiveScene().name;
            
            bool requireAuth = SceneAuth(currentSceneName);
            
            GameObject popup = null;
            if (currentSceneName != registerScene && requireAuth) {
                popup = Instantiate(authPopup, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
                popup.transform.SetParent(GameObject.Find("Canvas").transform, false);
                isInputEnabled = false;
            }

            var savedId = saved.id;
            var savedUsername = saved.username; 

            await Auth(
                a_Username: savedUsername, 
                a_Id: savedId, 
                a_Password: null,
                a_Notice: popup != null ? popup.GetComponentInChildren<NoticeText>() : null
            );
            
            isInputEnabled = true;
            
            if (!isAuth && requireAuth) {
                SceneManager.LoadScene(registerScene);
            }
            else if (isAuth && currentSceneName == registerScene) {
                registerStep = RegisterStep.Success;
            }
        }
    }

    private bool SceneAuth(string name) {
        foreach (var s in scenes) {
            if (s.scene.SceneName == name) {
                return s.requireAuth;
            }
        }
        return true;
    }

    public async void Register() {
        if (SceneManager.GetActiveScene().name != registerScene) {return;}
        
        var rg = GameObject.Find("RegisterGroup").GetComponent<RegisterGroup>();

        var usernameField = rg.usernameField;
        var passwordField = rg.passwordField;
        var noticeText = rg.noticeText;
        
        registerStep = RegisterStep.Pending;

        await Register(usernameField.text, passwordField.text, noticeText);
    }

    public void Continue() {
        if (SceneManager.GetActiveScene().name != registerScene) {return;}

        SceneManager.LoadScene(menuScene);
    }

    private async Task Register(string a_Userame, string a_Password, NoticeText a_Notice) {
        a_Notice.SetWait("Registering...");
        
        await Task.Delay(TimeSpan.FromMilliseconds(delayMS));

        if (isAuth) {
            a_Notice.SetError("Sign out first");
            registerStep = RegisterStep.Failure;
            return;
        }

        var usernameInput = a_Userame.Trim();
        var passwordInput = a_Password;

        if (usernameInput.Length < 3 || usernameInput.Length > 12) {
            if (a_Notice != null) a_Notice.SetError("Username must be 3-12 characters");
            registerStep = RegisterStep.Failure;
            return;
        }

        if (passwordInput.Length < 6 || passwordInput.Length > 16) {
            if (a_Notice != null) a_Notice.SetError("Password must be 6-16 characters");
            registerStep = RegisterStep.Failure;
            return;
        }
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(usernameInput, @"^[a-zA-Z0-9_]+$")) {
            if (a_Notice != null) a_Notice.SetError("Invalid username characters");
            registerStep = RegisterStep.Failure;
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(passwordInput, @"^[a-zA-Z0-9_]+$")) {
            if (a_Notice != null) a_Notice.SetError("Invalid password characters");
            registerStep = RegisterStep.Failure;
            return;
        }

        var passwordHash = Encode(passwordInput);

        var registerTask = socket.EmitAsync("register", (response) => {
            WSClient.instance.AddJob(async () => {
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>()
                });

                if (!String.IsNullOrEmpty(result.error)) {
                    a_Notice.SetError(result.error);
                    registerStep = RegisterStep.Failure;
                }
                else if (result.data != null) {
                    var resultId = result.data["id"];
                    var resultUsername = result.data["username"];
                    var resultPasswordHash = result.data["passhash"];

                    if (resultId != null && resultUsername == usernameInput && CheckHash(passwordInput, resultPasswordHash)) {
                        Debug.Log("calling auth from register");
                        await Auth(resultUsername, resultId, passwordInput, a_Notice);
                    }
                    else {
                        a_Notice.SetError("Register error (invalid response)");
                        registerStep = RegisterStep.Failure;
                    }
                }
                else {
                    a_Notice.SetError("Register error (empty response)");
                    registerStep = RegisterStep.Failure;
                }
            });
            
        }, new { username = usernameInput, passhash = passwordHash });

        if (await Task.WhenAny(registerTask, Task.Delay(TimeSpan.FromMilliseconds(timeoutMS))) != registerTask) {
            a_Notice.SetError("Register error (code timeout)");
            registerStep = RegisterStep.Failure;
        }
    }

    private async Task Auth(string a_Username, string a_Id = null, string a_Password = null, NoticeText a_Notice = null, [CallerMemberName] string callerName = "") {
        Debug.Log("auth called");

        if (a_Notice != null) {
            registerStep = RegisterStep.Pending;
            a_Notice.SetWait("Authenticating...");
        }
        
        if (callerName != "CheckSavedPlayer") {
            await Task.Delay(TimeSpan.FromMilliseconds(delayMS));
        }
        
        var passwordHash = a_Password == null ? null : Encode(a_Password);

        var authTask = socket.EmitAsync("auth", (response) => {
            WSClient.instance.AddJob(() => {
                checkingSaved = false;

                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>()
                });

                if (!String.IsNullOrEmpty(result.error)) {
                    if (a_Notice != null) {
                        a_Notice.SetError(result.error);
                        registerStep = RegisterStep.Failure;
                    }
                }
                else if (result.data != null) {
                    var resultId = result.data["id"];
                    var resultUsername = result.data["username"];
                    var resultPasswordHash = result.data["passhash"];
                    
                    if (!String.IsNullOrEmpty(resultId) && resultUsername == a_Username && (a_Password == null || CheckHash(a_Password, resultPasswordHash))) {
                        if (a_Notice != null) a_Notice.SetSuccess("Authenticated");
                        isAuth = true;
                        player = new PlayerData(resultId, resultUsername, "#FFFFFF");
                        SaveJsonData(player);

                        registerStep = RegisterStep.Success;

                        return;
                    }
                    else {
                        if (a_Notice != null) {
                            a_Notice.SetError("Auth error (invalid response)");
                            registerStep = RegisterStep.Failure;
                        }
                    }
                }
                else {
                    if (a_Notice != null) {
                        a_Notice.SetError("Auth error (empty response)");
                        registerStep = RegisterStep.Failure;
                    }
                }
            });
        }, new { id = a_Id, username = a_Username, passhash = passwordHash });

        if (await Task.WhenAny(authTask, Task.Delay(TimeSpan.FromMilliseconds(timeoutMS))) != authTask) {
            if (a_Notice != null) {
                a_Notice.SetError("Auth error (code timeout)");
                registerStep = RegisterStep.Failure;
            }
            checkingSaved = false;
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

    private bool DeleteJsonData() {
        return FileManager.EmptyFile(savePath);
    }

    private void AddJob(Action newJob) {
        jobs.Enqueue(newJob);
    }

    private string Encode(string str) {
        var crypt = new System.Security.Cryptography.SHA256Managed();
        var hash = new System.Text.StringBuilder();
        byte[] crypto = crypt.ComputeHash(Encoding.UTF8.GetBytes(str));
        foreach (byte theByte in crypto)
        {
            hash.Append(theByte.ToString("x2"));
        }
        return hash.ToString();
    }

    private bool CheckHash(string str, string hash) {
        return Encode(str) == hash;
    }
}
