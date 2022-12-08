using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Team
{
    private Team(string value) { Value = value; TeamColor = Color.white; }
    private Team(string value, Color color) { Value = value; TeamColor = color;}

    public string Value { get; private set; }
    public Color TeamColor { get; private set; }

    public static Team Blue { get { return new Team("Blue", Color.blue); } }
    public static Team Red { get { return new Team("Red", Color.red); } }

    public override string ToString()
    {
        return Value;
    }

    public Color GetColor()
    {
        return TeamColor;
    }
}

[System.Serializable]
public class Tower {
    public TurretScript turret;
    public Team team { get {return turret.team;} }
    public HexRenderer hex;
    public int index { get {return hex.index;} }
    
    public Tower(TurretScript turret, HexRenderer hex) {
        this.turret = turret;
        this.hex = hex;
    }
}

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [System.Serializable]
    public struct InputPauseLevel {
        public bool scroll;
        public bool mouse;
        public bool keyboard;
        public bool camera;

        public InputPauseLevel(bool scroll, bool mouse, bool keyboard, bool camera) {
            this.scroll = scroll;
            this.mouse = mouse;
            this.keyboard = keyboard;
            this.camera = camera;
        }

        public InputPauseLevel(bool enabled) {
            this.scroll = enabled;
            this.mouse = enabled;
            this.keyboard = enabled;
            this.camera = enabled;
        }

        public void Set(bool enabled) {
            this.scroll = enabled;
            this.mouse = enabled;
            this.keyboard = enabled;
            this.camera = enabled;
        }

        public void Toggle() {
            this.scroll = !this.scroll;
            this.mouse = !this.mouse;
            this.keyboard = !this.keyboard;
            this.camera = !this.camera;
        }
    }

    [Header("Pausing")]
    public GameObject pauseMenu;
    public InputPauseLevel pauseGameInput;
    
    [Header("Popups")]
    public GameObject waitingPopupPrefab;

    [Header("Input")]
    public bool isDraggingTower;

    [Header("Tower Placement")]
    private GameObject placementHolder;
    public GameObject towerPrefab;
    private GameObject towerObj;
    public GameObject arrowPrefab;
    private GameObject arrowObj;
    private HexRenderer placeOnHex;
    public Color validPlacementColor;
    public Color invalidPlacementColor;
    public float arrowHover;
    public float maxDist = 40f;

    [Header("Map")]
    public HexGridLayout hexes;
    public int friendlyHexIndex;
    public int enemyHexIndex;
    private HexRenderer friendlyBase;
    private HexRenderer enemyBase;
    public GameObject basePrefab;

    [Header("Team")]
    public Team friendlyTeam;
    public Team enemyTeam;

    [Header("Towers")]
    public List<Tower> towers = new List<Tower>();

    private void Awake() {
        if (instance != null) {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnEnable() {
        // pauseGameInput = new InputPauseLevel(false);
        //EnablePopup(waitingPopupPrefab);

        pauseGameInput = new InputPauseLevel(true);

        friendlyTeam = Team.Blue;
        enemyTeam = Team.Red;
    }

    private void Start() {
        friendlyBase = hexes.GetHex(friendlyHexIndex);
        enemyBase = hexes.GetHex(enemyHexIndex);

        CreateBases();
    }

    private void CreateBases() {
        var friendlyHeightMod = new Vector3(0, hexes.GetHex(friendlyHexIndex).height/2f, 0);
        var friendlyBaseObj = Instantiate(basePrefab, friendlyBase.transform.position + friendlyHeightMod, Quaternion.identity);
        friendlyBaseObj.GetComponent<BaseColorScript>().SetColor(friendlyTeam.GetColor());
        Vector3 newtarget = hexes.GetHex(0).transform.position;
        newtarget.y = friendlyBaseObj.transform.position.y;
        friendlyBaseObj.transform.LookAt(newtarget);
        
        var enemyHeightMod = new Vector3(0, hexes.GetHex(enemyHexIndex).height/2f, 0);
        var enemyBaseObj = Instantiate(basePrefab, enemyBase.transform.position + enemyHeightMod, Quaternion.identity);
        enemyBaseObj.GetComponent<BaseColorScript>().SetColor(enemyTeam.GetColor());
        newtarget = hexes.GetHex(0).transform.position;
        newtarget.y = enemyBaseObj.transform.position.y;
        enemyBaseObj.transform.LookAt(newtarget);
    }

    private void Update()
    {
        if (WSClient.isInputEnabled && pauseGameInput.keyboard) {
            HandleKeyboardInput();
        }
    }

    private bool HasMouseMoved() {
        return (Input.GetAxis("Mouse X") != 0) || (Input.GetAxis("Mouse Y") != 0);
    }

    private void HandleKeyboardInput() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            TogglePauseMenu();
        }
        
        var tempOverride = false;
        if (Input.GetKeyDown(KeyCode.T)) {
            placementHolder = new GameObject("PlacementHolder");

            // Top tower
            towerObj = Instantiate(towerPrefab) as GameObject;
            towerObj.name = "TempTower";
            towerObj.GetComponent<TurretScript>().isStatic = true;
            towerObj.GetComponent<TurretScript>().SetColor(invalidPlacementColor);
            towerObj.transform.SetParent(placementHolder.transform);
            towerObj.transform.localScale = new Vector3(1.05f, 1.05f, 1.05f);

            // Arrow
            arrowObj = Instantiate(arrowPrefab);
            arrowObj.name = "TempArrow";
            arrowObj.GetComponent<Colorizer>().SetColor(invalidPlacementColor);
            arrowObj.transform.SetParent(placementHolder.transform);
            
            tempOverride = true;
            placeOnHex = null;
            isDraggingTower = true;
            pauseGameInput.camera = false;
        }
        
        if (Input.GetKey(KeyCode.T) && (HasMouseMoved() || tempOverride)) {
            var mousePos = GetMouseWorldPosition();
            if (mousePos != null) {
                // keep mousePos within maxDist of friendlyBase
                var dist = Vector3.Distance((Vector3)mousePos, friendlyBase.position);
                if (dist > maxDist) {
                    mousePos = friendlyBase.position + ((Vector3)mousePos - friendlyBase.position).normalized * maxDist;
                }

                if (hexes.WorldPosToHex((mousePos ?? Vector3.zero), out HexRenderer outHex, out int? outIndex)) {
                    placeOnHex = outHex;

                    if (placeOnHex.turret != null || placeOnHex == friendlyBase || placeOnHex == enemyBase || Vector3.Distance(outHex.position, friendlyBase.position) > maxDist) { 
                        towerObj.GetComponent<TurretScript>().SetColor(invalidPlacementColor);
                        arrowObj.GetComponent<Colorizer>().SetColor(invalidPlacementColor);
                    }
                    else {
                        towerObj.GetComponent<TurretScript>().SetColor(validPlacementColor);
                        arrowObj.GetComponent<Colorizer>().SetColor(validPlacementColor);
                    }
                    
                    towerObj.transform.position = outHex.position + new Vector3(0, outHex.height/2f, 0);
                    arrowObj.transform.position = outHex.position + new Vector3(0, outHex.height/2f + arrowHover, 0);
                    
                    var sub = new Vector3(outHex.position.x, 0, outHex.position.z) - new Vector3(friendlyBase.position.x, 0, friendlyBase.position.z);
                    var newRot = Quaternion.LookRotation(sub, Vector3.up);

                    arrowObj.transform.rotation = newRot * Quaternion.Euler(0, 0, 90);

                    towerObj.GetComponent<TurretScript>().baseRotation = newRot;
                    towerObj.GetComponent<TurretScript>().RotateTurret(friendlyBase.position, true);

                    towerObj.SetActive(true);
                    arrowObj.SetActive(true);
                }
            }
            else {
                towerObj.SetActive(false);
                arrowObj.SetActive(false);
            }
        }
        
        if (Input.GetKeyUp(KeyCode.T)) {
            if (towerObj.activeSelf && placeOnHex != null && placeOnHex.turret == null && placeOnHex != friendlyBase && placeOnHex != enemyBase && Vector3.Distance(towerObj.transform.position, friendlyBase.position) <= maxDist) {
                var newTower = Instantiate(towerPrefab, placeOnHex.position + new Vector3(0, placeOnHex.height/2f, 0), Quaternion.identity) as GameObject;
                newTower.name = "Tower";
                var ts = newTower.GetComponent<TurretScript>();
                ts.team = friendlyTeam;
                ts.isStatic = false;
                ts.baseRotation = towerObj.GetComponent<TurretScript>().baseRotation;
                ts.RotateTurret(friendlyBase.position, true);

                placeOnHex.turret = ts;

                //AddTower(ts, placeOnHex);
            }
            
            Destroy(placementHolder);
            placeOnHex = null;
            isDraggingTower = false;
            pauseGameInput.camera = true;
            placementHolder = null;
            towerObj = null;
            arrowObj = null;
        }
    }

    private void UpdateStats() {

    }

    private static Vector3? GetMouseWorldPosition() {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        RaycastHit hit;
        
        if (Physics.Raycast(ray.origin, ray.direction, out hit, Mathf.Infinity, 1 << 8)) {
            Debug.Log("hit");
            return hit.point;
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
