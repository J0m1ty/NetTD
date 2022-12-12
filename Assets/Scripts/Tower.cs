using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public Gradient GenerateTeamGradient() {
        var newGradient = new Gradient();
        
        var colorKey = new GradientColorKey[2];
        colorKey[0].color = team.Color;
        colorKey[0].time = 0.0f;
        colorKey[1].color = Color.black;
        colorKey[1].time = 1.0f;

        newGradient.colorKeys = colorKey;

        return newGradient;
    }

    public Color GetColorByDist(GridUnit targetHex, Gradient colors, float range) {
        var dist = hexRef.Distance(targetHex);
        var color = colors.Evaluate(Mathf.Clamp01(dist / range));
        return color;
    }

    public abstract float GetRotation();

    public abstract void SetRotation(float newRotation);
}

public class BaseTower : Tower {
    private Transform towerBase;
    
    public BaseTower(GameObject baseObject, GridUnit hexRef, TeamInfo team) : base(baseObject, hexRef, team, TowerType.Base) {
        towerBase = towerObject.transform;
        
        towerObject.transform.position = hexRef.hexRenderer.transform.position + Vector3.up * hexRef.hexRenderer.height/2f;

        towerObject?.GetComponent<Colorizer>().SetAllColor(team.Color);
        
        var originPos = GameManager.instance.map.GetHexFromIndex(0).hexRenderer.transform.position;
        var newRotation = Quaternion.LookRotation(new Vector3(originPos.x, 0, originPos.z) - new Vector3(towerObject.transform.position.x, 0, towerObject.transform.position.z));

        towerObject.transform.rotation = Quaternion.Euler(0, Mathf.Round(newRotation.eulerAngles.y / 60f) * 60f + (GameManager.instance.map.isFlatTopped ? 0f : 30f), 0);
    }

    public override float GetRotation()
    {
        return towerBase.rotation.eulerAngles.y;
    }

    public override void SetRotation(float newRotation)
    {
        towerBase.rotation = Quaternion.Euler(0, newRotation, 0);
    }
}

public class GunTower : Tower {
    private Transform turretGun;
    private Transform turretBase;

    private Vector3? gunTarget;

    private Quaternion gunRotation;
    private Quaternion baseRotation;

    private float rotationSpeed;

    public GunTower(GameObject turretObject, GridUnit hexRef, TeamInfo team, bool colorByDist = false) : base(turretObject, hexRef, team, TowerType.Gun) {
        turretGun = towerObject.transform.Find("TurretGun");
        turretBase = towerObject.transform.Find("TurretBase");
        
        towerObject.transform.position = hexRef.hexRenderer.transform.position + Vector3.up * hexRef.hexRenderer.height/2f;

        var originHex = GameManager.instance.map.GetHexFromIndex(Team.IsFriendly(team) ? GameManager.instance.friendlyBaseIndex : GameManager.instance.enemyBaseIndex);

        towerObject?.GetComponent<Colorizer>().SetAllColor(colorByDist ? GetColorByDist(originHex, GenerateTeamGradient(), GameManager.instance.maxDist) : team.Color);

        var originPos = originHex.hexRenderer.transform.position;
        var newRotation = Quaternion.LookRotation(new Vector3(towerObject.transform.position.x, 0, towerObject.transform.position.z) - new Vector3(originPos.x, 0, originPos.z));

        baseRotation = Quaternion.Euler(0, Mathf.Round(newRotation.eulerAngles.y / 60f) * 60f + (GameManager.instance.map.isFlatTopped ? 0f : 30f), 0);
        gunRotation = baseRotation;

        turretBase.rotation = baseRotation * Quaternion.Euler(-90, 0, 0);
        turretGun.rotation = gunRotation * Quaternion.Euler(-90, 0, 0);

        gunTarget = null;
    }
    
    public void RotateTurret(Vector3 newAim, bool aimAway = false) {
        gunTarget = new Vector3(newAim.x, 0, newAim.z);
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
    }

    public override float GetRotation() {
        return turretBase.localRotation.eulerAngles.y;
    }

    public override void SetRotation(float newRotation) {
        baseRotation = Quaternion.Euler(0, newRotation, 0);
        turretBase.localRotation = baseRotation  * Quaternion.Euler(-90, 0, 0);
        
        gunRotation = baseRotation;
        turretGun.localRotation = gunRotation * Quaternion.Euler(-90, 0, 0);
    }
}

public class MinerTower : Tower {
    private Transform towerBase;
    
    public MinerTower(GameObject baseObject, GridUnit hexRef, TeamInfo team, bool colorByDist = false) : base(baseObject, hexRef, team, TowerType.Miner) {
        towerBase = towerObject.transform;
        
        towerObject.transform.position = hexRef.hexRenderer.transform.position + Vector3.up * hexRef.hexRenderer.height/2f;

        var originHex = GameManager.instance.map.GetHexFromIndex(Team.IsFriendly(team) ? GameManager.instance.friendlyBaseIndex : GameManager.instance.enemyBaseIndex);
        towerObject?.GetComponent<Colorizer>().SetAllColor(colorByDist ? GetColorByDist(originHex, GenerateTeamGradient(), GameManager.instance.maxDist) : team.Color);
        
        var originPos = originHex.hexRenderer.transform.position;
        var newRotation = Quaternion.LookRotation(new Vector3(originPos.x, 0, originPos.z) - new Vector3(towerObject.transform.position.x, 0, towerObject.transform.position.z));

        towerObject.transform.rotation = Quaternion.Euler(0, Mathf.Round(newRotation.eulerAngles.y / 60f) * 60f + (GameManager.instance.map.isFlatTopped ? 0f : 30f), 0);
    }

    public override float GetRotation() {
        return towerBase.rotation.eulerAngles.y;
    }

    public override void SetRotation(float newRotation)
    {
        towerBase.rotation = Quaternion.Euler(0, newRotation, 0);

    }
}