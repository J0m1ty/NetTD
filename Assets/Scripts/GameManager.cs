using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

public enum TowerType {
    Base = 0,
    Gun = 1,
    Miner = 2,
}

public enum TeamType {
    Friendly = 0,
    Enemy = 1,
}

public class EasyTower {
    public int index;
    public TowerType type;
    public TeamType team;
    public float baseRotation;

    public EasyTower(int index, TowerType type, TeamType team, float baseRotation) {
        this.index = index;
        this.type = type;
        this.team = team;
        this.baseRotation = baseRotation;
    }
}

public class PersonalInfo {
    public int money;
    public int life;
    public int productionRate;

    public static int initialProductionRate = 10;
    public static int maxLife = 100;
    public int totalMoney;

    public PersonalInfo(int money, int life, int? productionRate) {
        totalMoney = 0;

        this.money = money;
        this.life = life;
        this.productionRate = productionRate ?? initialProductionRate;

        totalMoney += money;
    }

    public Dictionary<string, string> ToDictionary() {
        return new Dictionary<string, string>() {
            { "money", money.ToString() },
            { "life", life.ToString() },
            { "production", productionRate.ToString() },
        };
    }
}

[System.Serializable]
public class TeamInfo
{
    public TeamInfo(Color color)
    {
        this.Color = color;
    }

    public Color Color { get; }
}

[System.Serializable]
public class Team
{
    public static TeamInfo Friendly = new TeamInfo(Color.blue);
    public static TeamInfo Enemy = new TeamInfo(Color.red);

    public static TeamInfo Other(TeamInfo team)
    {
        return team == Friendly ? Enemy : Friendly;
    }

    public static bool IsFriendly(TeamInfo team)
    {
        return team == Friendly;
    }

    public static bool IsEnemy(TeamInfo team)
    {
        return team == Enemy;
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Quality")]
    public float quality;

    [Header("Camera Reference")]
    public CameraController cameraRig;

    [Header("Pausing")]
    public GameObject pauseMenu;
    
    [Header("Popups")]
    public GameObject loadingPopupPrefab;
    public GameObject waitingPopupPrefab;
    public GameObject gameOverPopupPrefab;
    private GameObject[] popups;

    [Header("Input")]
    public bool lockKeyboard;
    public bool lockMouse;
    private GridUnit successHex;

    [Header("Tower Placement")]
    public Color validPlacementColor;
    public Color invalidPlacementColor;
    public GameObject arrowPrefab;
    public float arrowHoverDist;
    public float maxDist;

    private GameObject towerObj;
    private GameObject placementHolder;
    private GameObject arrowObj;

    [Header("Map Info")]
    public HexGridLayout map;

    [Header("Team Info")]
    private TeamInfo team;
    public int friendlyBaseIndex;
    public int enemyBaseIndex;
    private GameObject friendlyBase;
    private GameObject enemyBase;
    private bool isBasesSet;

    [Header("Player Info")]
    public TMPro.TextMeshProUGUI playerHealth;
    public TMPro.TextMeshProUGUI playerMoney;
    public PersonalInfo playerInfo;
    public PersonalInfo enemyInfo;
    [HideInInspector]
    public bool gameActive;

    [Header("Tower Info")]
    public GameObject baseTowerPrefab;
    public GameObject gunTowerPrefab;
    public GameObject minerTowerPrefab;
    public List<Tower> towers;
    
    public GameObject GetPrefabFromType(TowerType type)
    {
        switch (type)
        {
            case TowerType.Base:
                return baseTowerPrefab;
            case TowerType.Gun:
                return gunTowerPrefab;
            case TowerType.Miner:
                return minerTowerPrefab;
            default:
                return null;
        }
    }

    private void Awake() {
        if (instance != null) {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable() {
        isBasesSet = false;

        popups = new GameObject[] { loadingPopupPrefab, waitingPopupPrefab, gameOverPopupPrefab };

        team = Team.Friendly;

        towers = new List<Tower>();

        playerInfo = new PersonalInfo(10, 100, null);
        
        StartCoroutine(GameSequence());
    }

    private IEnumerator GameSequence() {
        EnablePopup(loadingPopupPrefab);

        yield return new WaitForSeconds(0.1f);

        map.Generate();

        yield return new WaitForSeconds(1f);
        
        InitGame();

        WSClient.instance?.IsReady();
    }

    public void InitGame() {
        DisablePopups();
        EnablePopup(waitingPopupPrefab);
        lockKeyboard = true;
        lockMouse = true;
        gameActive = false;
    }

    public void StartGame() {
        DisablePopups();
        lockKeyboard = false;
        lockMouse = false;
        gameActive = true;

        StartProduction();
    }

    public void EndGame(bool win, string method) {
        DisablePopups();
        var gameOver = EnablePopup(gameOverPopupPrefab);
        lockKeyboard = true;
        lockMouse = true;
        gameActive = false;

        StopAllCoroutines();
    
        gameOver.GetComponent<GameOverGroup>().SetResult(win: win, method: method, money: playerInfo.totalMoney, life: playerInfo.life);
    }

    public void StartProduction() {
        StartCoroutine(Production());
    }

    private IEnumerator Production() {
        while (gameActive) {
            yield return new WaitForSeconds(1f);
            playerInfo.money += playerInfo.productionRate;
            playerInfo.totalMoney += playerInfo.productionRate;
            EmitInfo();
        }
        yield return null;
    }

    public void SetBases(int friendly, int enemy) {
        friendlyBaseIndex = friendly;
        enemyBaseIndex = enemy;

        friendlyBase = PlaceTower(friendlyBaseIndex, TowerType.Base, Team.Friendly).towerObject;
        enemyBase = PlaceTower(enemyBaseIndex, TowerType.Base, Team.Enemy).towerObject;

        cameraRig.newPosition = new Vector3(friendlyBase.transform.position.x, 0, friendlyBase.transform.position.z);
        cameraRig.newRotation = Quaternion.LookRotation(map.GetHexFromIndex(enemyBaseIndex).hexRenderer.transform.position - friendlyBase.transform.position);

        isBasesSet = true;
    }

    public void Simplify(Transform parent, float? overrideQuality = null) {
        var meshFilters = parent.GetComponentsInChildren<MeshFilter>();
        foreach (MeshFilter meshFilter in meshFilters)
        {
            SimplifyMeshFilter(meshFilter, overrideQuality);
        }
    }

    private void SimplifyMeshFilter(MeshFilter meshFilter, float? overrideQuality = null) {
        if (quality >= 1) {return;}

        Mesh sourceMesh = meshFilter.sharedMesh;
        if (sourceMesh == null) {return;}
            
        var meshSimplifier = new UnityMeshSimplifier.MeshSimplifier();
        meshSimplifier.Initialize(sourceMesh);
        
        meshSimplifier.SimplifyMesh((overrideQuality ?? quality));
        
        meshFilter.sharedMesh = meshSimplifier.ToMesh();
    }

    private void Update() {
        foreach (var tower in towers) {
            var gunTower = tower as GunTower;

            if (gunTower != null) {
                gunTower.Rotate();
            }
        }

        if (WSClient.isInputEnabled && !lockKeyboard) {
            HandleKeyboardInput();
        }

        if (playerHealth != null) {
            playerHealth.text = $"Health: <color=#933042>{(playerInfo.life / PersonalInfo.maxLife * 100f).ToString()}%</color>";
        }

        if (playerMoney != null) {
            playerMoney.text = $"Credits: <color=#E0BA06>{(playerInfo.money).ToString()}¢</color>";
        }
    }

    private void HandleKeyboardInput() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            TogglePauseMenu();
        }

        if (Input.GetKeyDown(KeyCode.T)) {
            DestroyPlacementObjects();
            CreatePlacementObjects(gunTowerPrefab);
        }
        
        if (Input.GetKeyDown(KeyCode.G)) {
            DestroyPlacementObjects();
            CreatePlacementObjects(minerTowerPrefab);
        }

        if (Input.GetKey(KeyCode.T) || Input.GetKey(KeyCode.G)) {
            if (placementHolder == null) return;

            var hex = GetHexFromWorldPosition() as GridUnit;
            
            if (hex != null) {
                placementHolder.SetActive(true);
                placementHolder.transform.position = hex.hexRenderer.transform.position;

                if (CheckAvailability(hex)) {
                    if (isBasesSet && Vector3.Distance(friendlyBase.transform.position, hex.hexRenderer.transform.position) > maxDist) {
                        placementHolder.GetComponent<Colorizer>().SetAllColor(invalidPlacementColor);
                        successHex = null;
                    }
                    else {
                        successHex = hex;
                    }

                    towerObj.SetActive(true);

                    var height = Vector3.up * hex.hexRenderer.height/2f;
                    towerObj.transform.localPosition = height;
                    arrowObj.transform.localPosition = height + Vector3.up * arrowHoverDist;

                    var newRotation = isBasesSet ? Quaternion.LookRotation(new Vector3(placementHolder.transform.position.x, 0, placementHolder.transform.position.z) - new Vector3(friendlyBase.transform.position.x, 0, friendlyBase.transform.position.z)) : Quaternion.identity;
                    towerObj.transform.localRotation = Quaternion.Euler(0, Mathf.Round(newRotation.eulerAngles.y / 60f) * 60f + (map.isFlatTopped ? 0f : 30f), 0);
                    arrowObj.transform.localRotation = towerObj.transform.localRotation * Quaternion.Euler(0, 0, 90);
                }
                else {
                    towerObj.SetActive(false);
                    arrowObj.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    successHex = null;
                }
            }
            else {
                placementHolder.SetActive(false);
                successHex = null;
            }
        }

        if (Input.GetKeyUp(KeyCode.T)) {
            if (successHex != null) {
                PlaceTower(successHex, TowerType.Gun, team);
                EmitTowers();
            }

            DestroyPlacementObjects();
        }

        if (Input.GetKeyUp(KeyCode.G)) {
            if (successHex != null) {
                var placed = PlaceTower(successHex, TowerType.Miner, team);
                EmitTowers();
            }

            DestroyPlacementObjects();
        }
    }

    private void CreatePlacementObjects(GameObject prefab) {
        if (placementHolder != null) return;
        
        placementHolder = new GameObject("Placement Holder", typeof(Colorizer));
        placementHolder.transform.SetParent(map.transform.parent.Find("Towers"));
        
        towerObj = Instantiate(prefab, placementHolder.transform.position, Quaternion.identity, placementHolder.transform);
        towerObj.GetComponent<Colorizer>().SetAllColor(validPlacementColor);

        arrowObj = Instantiate(arrowPrefab, placementHolder.transform.position + new Vector3(0, arrowHoverDist, 0), Quaternion.identity, placementHolder.transform);
        arrowObj.GetComponent<Colorizer>().SetAllColor(validPlacementColor);

        placementHolder.SetActive(false);
        successHex = null;
    }

    private void DestroyPlacementObjects() {
        if (placementHolder == null) return;

        Destroy(placementHolder);
        placementHolder = null;
        successHex = null;
    }

    public void UpdateTowers(List<EasyTower> setTowers, bool force) {
        foreach (var tower in setTowers) {
            var placed = PlaceTower(tower.index, tower.type, tower.team == TeamType.Friendly ? Team.Friendly : Team.Enemy, force);
            placed.SetRotation(tower.baseRotation);
        }

        towers = map.towers.ToList();
    }

    public void EmitTowers() {
        var towers = new List<EasyTower>();

        foreach (var tower in map.towers) {
            towers.Add(new EasyTower(tower.hexRef.index, tower.type, tower.team == Team.Friendly ? TeamType.Friendly : TeamType.Enemy, tower.GetRotation()));
        }

        WSClient.instance?.EmitTowers(towers);
    }

    public void UpdateInfo(PersonalInfo friendly, PersonalInfo enemy) {
        playerInfo = friendly;
        enemyInfo = enemy;
    }

    public void EmitInfo() {
        WSClient.instance?.EmitInfo(playerInfo);
    }

    private bool CheckAvailability(GridUnit hex) {
        if (hex.tower != null || hex.index == friendlyBaseIndex || hex.index == enemyBaseIndex) {
            placementHolder?.GetComponent<Colorizer>().SetAllColor(invalidPlacementColor);
            return false;
        }

        placementHolder?.GetComponent<Colorizer>().SetAllColor(validPlacementColor);
        return true;
    }

    private Tower PlaceTower(int index, TowerType type, TeamInfo team, bool force = false) {
        return PlaceTower(map.GetHexFromIndex(index), type, team, force);
    }

    private Tower PlaceTower(GridUnit hex, TowerType type, TeamInfo team, bool force = false) {
        if ((hex.tower != null && !force) || team == null) return null;
        
        var newTower = Instantiate(GetPrefabFromType(type), hex.hexRenderer.transform.position, Quaternion.identity) as GameObject;
        newTower.transform.SetParent(map.transform.parent.Find("Towers"));
        switch (type) {
            case TowerType.Base:
                hex.tower = new BaseTower(newTower, hex, team);
                break;
            case TowerType.Gun:
                hex.tower = new GunTower(newTower, hex, team, true);
                break;
            case TowerType.Miner:
                hex.tower = new MinerTower(newTower, hex, team, true);
                break;
            default:
                return null;
        }

        Simplify(hex.tower.towerObject.transform, type == TowerType.Base ? quality/2f : null);
        
        towers.Add(hex.tower);
        return hex.tower;
    }

    public static GridUnit GetHexFromWorldPosition() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        RaycastHit hit;
        
        if (Physics.Raycast(ray.origin, ray.direction, out hit, Mathf.Infinity, 1 << 8)) {
            return hit.transform.GetComponent<HexRenderer>().gridRef;
        }
        return null;
    }

    public GameObject EnablePopup(GameObject popup) {
        var find = GameObject.Find(popup.name);
        if (!find) {
            var newPopup = Instantiate(popup, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
            newPopup.transform.SetParent(GameObject.Find("Canvas").transform, false);
            newPopup.name = popup.name;
            return newPopup;
        }
        return find;
    }

    public void DisablePopup(GameObject popup) {
        var find = GameObject.Find(popup.name);
        if (find) {
            Destroy(find);
        }
    }

    public void DisablePopups() {
        foreach (var popup in popups) {
            var find = GameObject.Find(popup.name);
            if (find) {
                Destroy(find);
            }
        }
    }

    public void TogglePauseMenu() {
        pauseMenu.SetActive(!pauseMenu.activeSelf);
    }

    public void Leave() {
        WSClient.instance?.LeaveGame();
    }

    public void EnableKeyboard() {
        lockKeyboard = false;
        Debug.Log("test enable");
    }

    public void DisableKeyboard() {
        lockKeyboard = true;
        Debug.Log("test disable");
    }
}
