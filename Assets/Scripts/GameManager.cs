using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TowerType {
    Base = 0,
    Gun = 1,
}

public enum TeamType {
    Friendly = 0,
    Enemy = 1,
}

public class EasyTower {
    public int index;
    public TowerType type;
    public TeamType team;

    public EasyTower(int index, TowerType type, TeamType team) {
        this.index = index;
        this.type = type;
        this.team = team;
    }
}

public abstract class Tower {
    public GameObject towerObject;
    public GridUnit hexRef;
    public TeamInfo team;
    public TowerType type;

    protected Tower(GameObject towerObject, GridUnit hexRef, TeamInfo team, TowerType type) {
        this.towerObject = towerObject;
        this.hexRef = hexRef;
        this.team = team;
        this.type = type;
    }
}

public class BaseTower : Tower {
    private Transform towerBase;
    
    public BaseTower(GameObject baseObject, GridUnit hexRef, TeamInfo team) : base(baseObject, hexRef, team, TowerType.Base) {
        towerBase = towerObject.transform.Find("TowerBase");
        
        towerObject.transform.position = hexRef.hexRenderer.transform.position;

        towerObject?.GetComponent<Colorizer>().SetAllColor(team.Color);
        
        var originPos = GameManager.instance.map.GetHexFromIndex(0).hexRenderer.transform.position;
        var newRotation = Quaternion.LookRotation(new Vector3(originPos.x, 0, originPos.z) - new Vector3(towerObject.transform.position.x, 0, towerObject.transform.position.z));

        towerObject.transform.rotation = Quaternion.Euler(0, Mathf.Round(newRotation.eulerAngles.y / 60f) * 60f + (GameManager.instance.map.isFlatTopped ? 0f : 30f), 0);
    }
}

public class GunTower : Tower {
    private Transform turretGun;
    private Transform turretBase;

    private Vector3? gunTarget;
    private Vector3? baseTarget;

    private Quaternion gunRotation;
    private Quaternion baseRotation;

    private float rotationSpeed;

    public GunTower(GameObject turretObject, GridUnit hexRef, TeamInfo team) : base(turretObject, hexRef, team, TowerType.Gun) {
        turretGun = towerObject.transform.Find("TurretGun");
        turretBase = towerObject.transform.Find("TurretBase");
        
        towerObject.transform.position = hexRef.hexRenderer.transform.position + Vector3.up * hexRef.hexRenderer.height/2f;

        var originPos = GameManager.instance.map.GetHexFromIndex(GameManager.instance.friendlyBaseIndex).hexRenderer.transform.position;
        var newRotation = Quaternion.LookRotation(new Vector3(towerObject.transform.position.x, 0, towerObject.transform.position.z) - new Vector3(originPos.x, 0, originPos.z));

        towerObject.transform.rotation = Quaternion.Euler(0, Mathf.Round(newRotation.eulerAngles.y / 60f) * 60f + (GameManager.instance.map.isFlatTopped ? 0f : 30f), 0);

        towerObject?.GetComponent<Colorizer>().SetAllColor(team.Color);

        gunTarget = null;
        baseTarget = null;

        gunRotation = turretGun.rotation;
        baseRotation = turretBase.rotation;
    }
    
    public void RotateTurret(Vector3 newAim, bool aimAway = false) {
        gunTarget = new Vector3(newAim.x, 0, newAim.z);
    }

    public void RotateBase(Vector3 newAim, bool aimAway = false) {
        baseTarget = new Vector3(newAim.x, 0, newAim.z);
    }

    public void Rotate() {
        if (gunTarget != null) {
            var at = new Vector3(gunTarget.Value.x, 0, gunTarget.Value.z);
            var from = new Vector3(turretGun.position.x, 0, turretGun.position.z);

            var direction = at - from;

            if (direction == Vector3.zero) return;

            gunRotation = Quaternion.Slerp(gunRotation, Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed);
            turretGun.localRotation = gunRotation * Quaternion.Euler(-90, 0, 0);
        }

        if (baseTarget != null) {
            var at = new Vector3(baseTarget.Value.x, 0, baseTarget.Value.z);
            var from = new Vector3(turretBase.position.x, 0, turretBase.position.z);

            var direction = at - from;

            if (direction == Vector3.zero) return;

            baseRotation = Quaternion.Slerp(baseRotation, Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed);
            baseRotation = Quaternion.Euler(0, Mathf.Round(baseRotation.eulerAngles.y / 60f) * 60f + (GameManager.instance.map.isFlatTopped ? 0f : 30f), 0);
            turretBase.localRotation = baseRotation * Quaternion.Euler(-90, 0, 0);
        }
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

    [Header("Pausing")]
    public GameObject pauseMenu;
    
    [Header("Popups")]
    public GameObject waitingPopupPrefab;

    // input
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

    [Header("Tower Info")]
    public List<Tower> towers;
    public GameObject baseTowerPrefab;
    public GameObject gunTowerPrefab;
    
    public GameObject GetPrefabFromType(TowerType type)
    {
        switch (type)
        {
            case TowerType.Base:
                return baseTowerPrefab;
            case TowerType.Gun:
                return gunTowerPrefab;
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
        map.Generate();

        team = Team.Friendly;

        towers = new List<Tower>();
    }

    private void Start() {
        EnablePopup(waitingPopupPrefab);
    }

    public void SetBases(int friendly, int enemy) {
        friendlyBaseIndex = friendly;
        enemyBaseIndex = enemy;

        friendlyBase = PlaceTower(friendlyBaseIndex, TowerType.Base, Team.Friendly);
        enemyBase = PlaceTower(enemyBaseIndex, TowerType.Base, Team.Enemy);
    }

    private void Update() {
        foreach (var tower in towers) {
            var gunTower = tower as GunTower;

            if (gunTower != null) {
                gunTower.Rotate();
            }
        }

        if (WSClient.isInputEnabled) {
            HandleKeyboardInput();
        }
    }

    private void HandleKeyboardInput() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            TogglePauseMenu();
        }

        if (Input.GetKeyDown(KeyCode.T)) {
            CreatePlacementObjects();
        }

        if (Input.GetKey(KeyCode.T)) {
            if (placementHolder == null) return;

            var hex = GetHexFromWorldPosition() as GridUnit;
            
            if (hex != null) {
                placementHolder.SetActive(true);
                placementHolder.transform.position = hex.hexRenderer.transform.position;

                if (CheckAvailability(hex)) {
                    if (Vector3.Distance(friendlyBase.transform.position, hex.hexRenderer.transform.position) > maxDist) {
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

                    var newRotation = Quaternion.LookRotation(new Vector3(placementHolder.transform.position.x, 0, placementHolder.transform.position.z) - new Vector3(friendlyBase.transform.position.x, 0, friendlyBase.transform.position.z));
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
    }

    private void CreatePlacementObjects() {
        if (placementHolder != null) return;
        
        placementHolder = new GameObject("Placement Holder", typeof(Colorizer));
        placementHolder.transform.SetParent(map.transform.parent);
        
        towerObj = Instantiate(gunTowerPrefab, placementHolder.transform.position, Quaternion.identity, placementHolder.transform);
        towerObj.GetComponent<Colorizer>().SetAllColor(validPlacementColor);

        arrowObj = Instantiate(arrowPrefab, placementHolder.transform.position + new Vector3(0, arrowHoverDist, 0), Quaternion.identity, placementHolder.transform);
        arrowObj.GetComponent<Colorizer>().SetAllColor(validPlacementColor);

        placementHolder.SetActive(false);
        successHex = null;
    }

    public void UpdateTowers(List<EasyTower> setTowers, bool force) {
        foreach (var tower in setTowers) {
            PlaceTower(tower.index, tower.type, tower.team == TeamType.Friendly ? Team.Friendly : Team.Enemy, force);
        }

        towers = map.towers.ToList();
    }

    public void EmitTowers() {
        var towers = new List<EasyTower>();

        foreach (var tower in map.towers) {
            towers.Add(new EasyTower(tower.hexRef.index, tower.type, tower.team == Team.Friendly ? TeamType.Friendly : TeamType.Enemy));
        }

        WSClient.instance?.EmitTowers(towers);
    }

    private void DestroyPlacementObjects() {
        if (placementHolder == null) return;

        Destroy(placementHolder);
        placementHolder = null;
        successHex = null;
    }

    private bool CheckAvailability(GridUnit hex) {
        if (hex.tower != null || hex.index == friendlyBaseIndex || hex.index == enemyBaseIndex) {
            placementHolder?.GetComponent<Colorizer>().SetAllColor(invalidPlacementColor);
            return false;
        }

        placementHolder?.GetComponent<Colorizer>().SetAllColor(validPlacementColor);
        return true;
    }

    private GameObject PlaceTower(int index, TowerType type, TeamInfo team, bool force = false) {
        return PlaceTower(map.GetHexFromIndex(index), type, team, force);
    }

    private GameObject PlaceTower(GridUnit hex, TowerType type, TeamInfo team, bool force = false) {
        if ((hex.tower != null && !force) || team == null) return null;
        
        var newTower = Instantiate(GetPrefabFromType(type), hex.hexRenderer.transform.position, Quaternion.identity) as GameObject;
        newTower.transform.SetParent(map.transform.parent);
        switch (type) {
            case TowerType.Base:
                hex.tower = new BaseTower(newTower, hex, team);
                break;
            case TowerType.Gun:
                hex.tower = new GunTower(newTower, hex, team);
                break;
            default:
                return null;
        }
        
        towers.Add(hex.tower);
        return newTower;
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
        WSClient.instance.LeaveGame();
    }
}
