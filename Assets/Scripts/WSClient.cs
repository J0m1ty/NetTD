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
    private string currentRoomId = ""; // current room id

    // Scene references
    [Header("Scene Info")]
    public SceneWrapper[] scenes;
    private string registerScene;
    private string menuScene;

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
        Menu
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

        isAuth = false;
        checkingSaved = false;
        registerStep = RegisterStep.None;
        
        foreach (var s in scenes) {
            if (s.type == SceneType.Auth) {
                registerScene = s.scene.SceneName;
            } else if (s.type == SceneType.Menu) {
                menuScene = s.scene.SceneName;
            }
        }

        /** message event **/
        OnMessageEvent();
        
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
            });
        };

        socket.OnReconnected += (sender, e) => {
            WSClient.instance.AddJob(() => {
                DisableDisconnectPopup();
            });
        };
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
    
    // Methods
    public async void HostRoom() {
        Debug.Log("HostRoom");
        await socket.EmitAsync("hostRoom", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("host room " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>()
                });

                // error : string
                // data : {roomId: string}

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("host err: " + result.error);
                } else {
                    Debug.Log("host data: " + result.data);
                }
            });
        });
    }

    public async void JoinRoom(string id) {
        Debug.Log("JoinRoom");

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
                    Debug.Log("join:data " + result.data.ToString());
                }
            });
        }, new { roomId = inputId });
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

        await socket.EmitAsync("message", new { roomId = roomId, message = message });
    }

    private void OnMessageEvent() {
        socket.On("message", (response) => {
            WSClient.instance.AddJob(() => {
                Debug.Log("message: " + response.ToString());
                var result = JsonConvert.DeserializeAnonymousType(response.GetValue(0).ToString(), new {
                    error = string.Empty,
                    data = new Dictionary<string, string>()
                });

                // error : string
                // data : {username: string, message: string, roomId: string, timestamp: string}

                if (result?.data["roomId"] == "MAIN" && SceneManager.GetActiveScene().name != menuScene) {
                    return;
                }

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("message err " + result.error);
                } else {
                    communicator.WriteMessage(result.data["username"], result.data["message"]);
                }
            });
        });
    }

    private void OnUsersEvent() {
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

                if (!string.IsNullOrEmpty(result.error)) {
                    Debug.Log("users err " + result.error);
                } else {
                    Debug.Log("users data " + result.data.ToString());
                }
            });
        });
    }

    private void UpdateUsers() {
        
    }

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

    public void Logout() {
        safeDisconnecting = true;
        DeleteJsonData();
        DisconnectSocket();
        SceneManager.LoadScene(registerScene);
        ConnectSocket();
    }
    
    async private void CheckSavedPlayer() {
        if (isAuth || checkingSaved) {return;}

        var saved = LoadJsonData();
        
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
            var savedColor = saved.color;

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

        if (isAuth || player != null) {
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
