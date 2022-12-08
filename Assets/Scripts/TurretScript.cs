using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TurretScript : MonoBehaviour
{
    public Transform turretGun;
    public Transform turretBase;

    public float turretOffset;

    public Quaternion baseRotation = Quaternion.Euler(0, 0, 0);

    public float rotationSpeed = 10f;
    public Vector3 aimAt;
    public Quaternion turretRot;
    public bool reverse;

    private Team _team;
    public Team team { get {return _team;} set {
        _team = value;
        SetColor(_team.GetColor());
    }}

    public bool isStatic;
    
    public void RotateTurret(Vector3 newAim, bool aimAway = false)
    {
        aimAt = newAim;
        reverse = aimAway;
    }

    public void SetColor(Color newColor) {
        foreach (Transform child in transform) {
            var cr = child.GetComponent<Renderer>();

            Material turretMaterial = new Material(Shader.Find("Standard"));
            turretMaterial.CopyPropertiesFromMaterial(cr.material);
            turretMaterial.color = newColor;
            cr.material = turretMaterial;
        }
    }

    void Start()
    {
        turretRot = baseRotation;
    }

    void Update()
    {
        var at = new Vector3(aimAt.x, 0, aimAt.z);
        var from = new Vector3(turretGun.position.x, 0, turretGun.position.z);

        var direction = reverse ? from - at : at - from;

        if (direction == Vector3.zero) return;

        turretRot = Quaternion.Slerp(turretRot, Quaternion.LookRotation(direction), Time.deltaTime * rotationSpeed);
        turretGun.localRotation = turretRot * Quaternion.Euler(-90, 0, 0);
        turretBase.localRotation = baseRotation * Quaternion.Euler(-90, 0, 0);

        turretGun.localPosition = turretBase.transform.localPosition + new Vector3(0, turretOffset, 0);
    }
}
