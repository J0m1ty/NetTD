using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MyBox;

[System.Serializable]
public struct Vector3Range
{
    public Vector3 min;
    public Vector3 max;

    public Vector3Range(Vector3 min, Vector3 max)
    {
        this.min = min;
        this.max = max;
    }
}

public class CameraController : MonoBehaviour
{
    public GameObject cameraChild;

    [Header("General Settings")]
    public bool allowZoomKeys;
    public float movementTime;
    public bool isOrthographic;
    [ConditionalField("isOrthographic", false, true)]
    public float OrthoFOV;
    [ConditionalField("isOrthographic", false, true)]
    public float distanceOffset;
    [ConditionalField("isOrthographic", false, false)]
    public float FOV;

    [Header("Movement Settings")]
    public MinMaxFloat speedRange;

    [Header("Rotation Settings")]
    public float rotationAmount;

    [Header("Zoom Settings")]
    public Vector3 zoomAmount;
    public MinMaxFloat zoomRange;
    public Vector2 angleOffset;

    private float movementSpeed;
    private Vector3 newPosition;
    private Quaternion newRotation;
    private Vector3 newZoom;

    private Vector3 dragStartPosition;
    private Vector3 dragCurrentPosition;
    private Vector3 rotateStartPosition;
    private Vector3 rotateCurrentPosition;

    void Start() {
        cameraChild = transform.GetChild(0).gameObject;

        cameraChild.GetComponent<Camera>().orthographic = isOrthographic;
        if (isOrthographic) {
            cameraChild.GetComponent<Camera>().orthographicSize = OrthoFOV;
        } else {
            cameraChild.GetComponent<Camera>().fieldOfView = FOV;
            distanceOffset = 0;
        }

        newPosition = transform.position;
        newRotation = transform.rotation;
        newZoom = cameraChild.transform.localPosition + (zoomAmount * -distanceOffset); 
    }

    void LateUpdate() {
        cameraChild.transform.LookAt(transform.position);

        if (WSClient.isInputEnabled) {
            HandleMovementInput();
            HandleMouseInput();
        }
    }

    void HandleMouseInput() {
        if (Input.mouseScrollDelta.y != 0) {
            newZoom += Input.mouseScrollDelta.y * zoomAmount;
        }

        if (Input.GetMouseButtonDown(0)) {
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            float entry;

            if (plane.Raycast(ray, out entry)) {
                dragStartPosition = ray.GetPoint(entry);
            }
        }

        if (Input.GetMouseButton(0)) {
            Plane plane = new Plane(Vector3.up, Vector3.zero);
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            float entry;

            if (plane.Raycast(ray, out entry)) {
                dragCurrentPosition = ray.GetPoint(entry);
                newPosition = transform.position + dragStartPosition - dragCurrentPosition;
            }
        }

        if (Input.GetMouseButtonDown(2)) {
            rotateStartPosition = Input.mousePosition;
        }
        if (Input.GetMouseButton(2)) {
            rotateCurrentPosition = Input.mousePosition;

            Vector3 difference = rotateStartPosition - rotateCurrentPosition;
            rotateStartPosition = rotateCurrentPosition;

            newRotation *= Quaternion.Euler(Vector3.up * (-difference.x / 5f));
        }
    }

    void HandleMovementInput() {
        if (Input.GetKey(KeyCode.LeftShift)) {
            movementSpeed = speedRange.Max;
        } else {
            movementSpeed = speedRange.Min;
        }

        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) {
            newPosition += transform.forward * movementSpeed;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) {
            newPosition += transform.forward * -movementSpeed;
        }
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) {
            newPosition += transform.right * -movementSpeed;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) {
            newPosition += transform.right * movementSpeed;
        }

        if (Input.GetKey(KeyCode.Q)) {
            newRotation *= Quaternion.Euler(Vector3.up * rotationAmount);
        }
        if (Input.GetKey(KeyCode.E)) {
            newRotation *= Quaternion.Euler(Vector3.up * -rotationAmount);
        }

        if (allowZoomKeys) {
            if (Input.GetKey(KeyCode.R)) {
                newZoom += zoomAmount;
            }
            if (Input.GetKey(KeyCode.F)) {
                newZoom -= zoomAmount;
            }
        }
        
        newZoom = newZoom.normalized * Mathf.Clamp(newZoom.magnitude, zoomRange.Min + distanceOffset, zoomRange.Max + distanceOffset);

        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * movementTime);
        cameraChild.transform.localPosition = Vector3.Lerp(cameraChild.transform.localPosition, newZoom, Time.deltaTime * movementTime);

        float offset = Mathf.Lerp(angleOffset[0], angleOffset[1], Mathf.InverseLerp(zoomRange.Min, zoomRange.Max, cameraChild.transform.localPosition.magnitude - distanceOffset));
        cameraChild.transform.rotation *= Quaternion.AngleAxis(offset, Vector3.right);
    }
}
