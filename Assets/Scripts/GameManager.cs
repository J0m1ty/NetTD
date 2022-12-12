using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    public static int maxLife = 100;
    public int productionRate = 1;

    public PersonalInfo(int money, int life) {
        this.money = money;
        this.life = life;
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
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("Camera Reference")]
    public CameraController cameraRig;

    [Header("Pausing")]
    public GameObject pauseMenu;
    
    [Header("Popups")]
    public GameObject waitingPopupPrefab;

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

    [HideInInspector]
    public bool gameActive;

    private void Awake() {
        if (instance != null) {
            Destroy(gameObject);
            return;
        }

        instance = this;

        isBasesSet = false;
    }

    private void OnEnable() {
        map.Generate();

        team = Team.Friendly;

        towers = new List<Tower>();

        playerInfo = new PersonalInfo(10, 100);
    }

    private void Start() {
        InitGame(); // also in WSClient when scene switches
    }

    public void InitGame() {
        EnablePopup(waitingPopupPrefab);
        lockKeyboard = true;
        lockMouse = true;
        gameActive = false;
    }

    public void StartGame() {
        DisablePopup(waitingPopupPrefab);
        lockKeyboard = false;
        lockMouse = false;
        gameActive = true;
    }

    public void StartProduction() {
        StartCoroutine(Production());
    }

    private IEnumerator Production() {
        while (gameActive) {
            yield return new WaitForSeconds(1f);
            playerInfo.money += playerInfo.productionRate;
        }
        yield return null;
    }

    public void SetBases(int friendly, int enemy) {
        friendlyBaseIndex = friendly;
        enemyBaseIndex = enemy;

        friendlyBase = PlaceTower(friendlyBaseIndex, TowerType.Base, Team.Friendly).towerObject;
        enemyBase = PlaceTower(enemyBaseIndex, TowerType.Base, Team.Enemy).towerObject;

        cameraRig.newPosition = new Vector3(friendlyBase.transform.position.x, 0, friendlyBase.transform.position.z);
        cameraRig.newRotation = Quaternion.LookRotation(map.GetHexFromIndex(0).hexRenderer.transform.position - friendlyBase.transform.position);

        isBasesSet = true;
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
        placementHolder.transform.SetParent(map.transform.parent);
        
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
        newTower.transform.SetParent(map.transform.parent);
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

    public void EnablePopup(GameObject popup) {
        var find = GameObject.Find(popup.name);
        if (!find) {
            var newPopup = Instantiate(popup, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;
            newPopup.transform.SetParent(GameObject.Find("Canvas").transform, false);
            newPopup.name = popup.name;
        }
    }

    public void DisablePopup(GameObject popup) {
        var find = GameObject.Find(popup.name);
        if (find) {
            Destroy(find);
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
