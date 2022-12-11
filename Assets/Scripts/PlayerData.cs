using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string id;
    public string username;
    public string usernameColor;

    public PlayerData(string a_Id, string a_Name, string a_UsernameColor)
    {
        this.id = a_Id;
        this.username = a_Name;
        this.usernameColor = a_UsernameColor;
    }

    public PlayerData(string json) {
        FromJSON(json);
    }

    public string ToJSON()
    {
        return JsonUtility.ToJson(this);
    }

    public void FromJSON(string a_JSON)
    {
        JsonUtility.FromJsonOverwrite(a_JSON, this);
    }
}
