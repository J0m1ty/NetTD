using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine;

[Serializable]
public class RegisterGroup : MonoBehaviour {
    public TMP_InputField usernameField;
    public TMP_InputField passwordField;
    public GameObject fieldGroup;
    public Button submitButton;
    public Button continueButton;
    public NoticeText noticeText;
}