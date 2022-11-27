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
    public float movementTime;
    /*public bool orthographic;
    [ConditionalField("orthographic", false, true)]
    public float orthographicSize;
    [ConditionalField("orthographic", false, false)]
    public float FOV;*/

    [Header("Movement Settings")]
    public MinMaxFloat speedRange;

    [Header("Rotation Settings")]
    public float rotationAmount;

    [Header("Zoom Settings")]
    public Vector3 zoomAmountClose;
    public Vector3 zoomAmountFar;
    public Vector3Range zoomRange;

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

        newPosition = transform.position;
        newRotation = transform.rotation;
        newZoom = cameraChild.transform.localPosition;
    }

    void LateUpdate() {
        HandleMovementInput();
        HandleMouseInput();
        
        cameraChild.transform.LookAt(transform.position);
    }

    public static float InverseLerp(Vector3 a, Vector3 b, Vector3 value)
     {
        Vector3 AB = b - a;
        Vector3 AV = value - a;
        return Vector3.Dot(AV, AB) / Vector3.Dot(AB, AB);
     }

    Vector3 GetZoomAmount() {
        // get the position between zoom ranges
        float zoomPosition = InverseLerp(zoomRange.min, zoomRange.max, newZoom);
        // get the zoom amount based on the position
        return Vector3.Lerp(zoomAmountClose, zoomAmountFar, zoomPosition).normalized;
    }

    void HandleMouseInput() {
        if (Input.mouseScrollDelta.y != 0) {
            newZoom += Input.mouseScrollDelta.y * GetZoomAmount();
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

        if (Input.GetKey(KeyCode.R)) {
            newZoom += GetZoomAmount();
        }
        if (Input.GetKey(KeyCode.F)) {
            newZoom -= GetZoomAmount();
        }
        
        // newZoom = new Vector3(
        //     Mathf.Clamp(newZoom.x, zoomRange.min.x, zoomRange.max.x),
        //     Mathf.Clamp(newZoom.y, zoomRange.min.y, zoomRange.max.y),
        //     Mathf.Clamp(newZoom.z, zoomRange.min.z, zoomRange.max.z)
        // );

        transform.position = Vector3.Lerp(transform.position, newPosition, Time.deltaTime * movementTime);
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, Time.deltaTime * movementTime);
        cameraChild.transform.localPosition = Vector3.Lerp(cameraChild.transform.localPosition, newZoom, Time.deltaTime * movementTime);
    }
}
