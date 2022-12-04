using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string id;
    public string username;
    public string color;

    public PlayerData(string a_Id, string a_Name, string a_Color)
    {
        this.id = a_Id;
        this.username = a_Name;
        this.color = a_Color;
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
