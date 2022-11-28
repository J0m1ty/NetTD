using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData
{
    public string id;
    public string name;

    public PlayerData(string a_Id, string a_Name)
    {
        this.id = a_Id;
        this.name = a_Name;
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
