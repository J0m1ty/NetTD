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

    [Header("Auth Settings")]
    public GameObject authPopup; // if not in a register scene, show popup
    public static bool isInputEnabled = true; // singleton to disable input while auth
    private bool isAuth; // is the player authenticated
    private bool checkingSaved; // is async memory being checked

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
    private PlayerData player;  // player data

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
    
    void Awake() {
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
        
        socket.OnConnected += (sender, e) => {
            WSClient.instance.AddJob(() => {
                CheckSavedPlayer();
            });
        };
    }

    void ConnectSocket() {
        var uri = new System.Uri(url);

        socket = new SocketIOUnity(uri, new SocketIOOptions {
            Query = new Dictionary<string, string> {
                { "token", "UNITY" }
            }
        });

        socket.Connect();
    }

    void Update() {
        while (jobs.Count > 0) {
            jobs.Dequeue().Invoke();
        }
    }

    void OnApplicationQuit() {
        socket.Disconnect();
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
            a_Notice.SetError("Register error (timeout)");
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
                        player = new PlayerData(resultId, resultUsername);
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
                a_Notice.SetError("Auth error (timeout)");
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
