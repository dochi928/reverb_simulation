using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System;

public class PresetManager : MonoBehaviour
{
    public GameObject blockPrefab;
    public Transform objectParent;
    public MoveObject mover;
    public RoomController roomController;

    [Header("Core Audio Objects")]
    public Transform ampAnchor;
    public Transform channelAnchor;

    private string presetPath;

    void Awake()
    {
        presetPath = Application.dataPath + "/Presets/";
        if (!Directory.Exists(presetPath)) Directory.CreateDirectory(presetPath);
    }

    void Start()
    {
        Invoke("AutoSaveDefaultRoom", 0.1f);
    }

    void AutoSaveDefaultRoom()
    {
        if (!File.Exists(presetPath + "room_0.txt")) { SaveRoom(0); }
    }

    private string FormatVec3(Vector3 vec, int posDecimals, int rotDecimals)
    {
        float x = (float)Math.Round(vec.x, posDecimals);
        float y = (float)Math.Round(vec.y, posDecimals);
        float z = (float)Math.Round(vec.z, posDecimals);

        if (rotDecimals == 0)
        {
            return $"{(int)Math.Round(vec.x)},{(int)Math.Round(vec.y)},{(int)Math.Round(vec.z)}";
        }
        return $"{x},{y},{z}";
    }

    public void SaveRoom(int index)
    {
        StringBuilder sb = new StringBuilder();

        sb.AppendLine($"AMP POS:{FormatVec3(ampAnchor.position, 1, 1)} ROT:{FormatVec3(ampAnchor.eulerAngles, 0, 0)}");
        sb.AppendLine($"CHA POS:{FormatVec3(channelAnchor.position, 1, 1)} ROT:{FormatVec3(channelAnchor.eulerAngles, 0, 0)}");
        sb.AppendLine($"ROOM W:{Math.Round(roomController.GetWidth(), 1)} D:{Math.Round(roomController.GetDepth(), 1)}");

        foreach (Transform child in objectParent)
        {
            sb.Append($"OBJ TAG:{child.tag} ");
            sb.Append($"POS:{FormatVec3(child.position, 1, 1)} ");
            sb.Append($"ROT:{FormatVec3(child.eulerAngles, 0, 0)} ");
            sb.Append($"SCA:{FormatVec3(child.localScale, 1, 1)}");
            sb.AppendLine();
        }

        File.WriteAllText(presetPath + "room_" + index + ".txt", sb.ToString());
        Debug.Log($"<color=cyan>Room {index} 저장 완료 (이산화 적용)</color>");
    }

    public void LoadRoom(int index)
    {
        foreach (Transform child in objectParent) { Destroy(child.gameObject); }

        string path = presetPath + "room_" + index + ".txt";
        if (!File.Exists(path)) return;

        string[] lines = File.ReadAllLines(path);
        foreach (string line in lines)
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine)) continue;

            string[] parts = trimmedLine.Split(' ');
            string header = parts[0];

            if (header == "AMP")
            {
                ampAnchor.position = ParseVector3(parts[1].Split(':')[1]);
                ampAnchor.eulerAngles = ParseVector3(parts[2].Split(':')[1]);
            }
            else if (header == "CHA")
            {
                channelAnchor.position = ParseVector3(parts[1].Split(':')[1]);
                channelAnchor.eulerAngles = ParseVector3(parts[2].Split(':')[1]);
            }
            else if (header == "ROOM")
            {
                float w = float.Parse(parts[1].Split(':')[1]);
                float d = float.Parse(parts[2].Split(':')[1]);
                roomController.SetRoomSize(w, d);
            }
            else if (header == "OBJ")
            {
                string tag = parts[1].Split(':')[1];
                Vector3 pos = ParseVector3(parts[2].Split(':')[1]);
                Vector3 rot = ParseVector3(parts[3].Split(':')[1]);
                Vector3 sca = ParseVector3(parts[4].Split(':')[1]);

                GameObject newObj = Instantiate(blockPrefab, pos, Quaternion.Euler(rot), objectParent);
                newObj.transform.localScale = sca;
                newObj.tag = tag;
                ApplyMaterialByTag(newObj, tag);
            }
        }
        Debug.Log($"<color=yellow>Room {index} 로드 완료</color>");
    }

    private Vector3 ParseVector3(string data)
    {
        string[] v = data.Split(',');
        return new Vector3(float.Parse(v[0]), float.Parse(v[1]), float.Parse(v[2]));
    }

    private void ApplyMaterialByTag(GameObject obj, string tag)
    {
        MeshRenderer renderer = obj.GetComponent<MeshRenderer>();
        if (renderer != null && mover != null)
        {
            if (tag == "Cement")       renderer.material = mover.Cement;
            else if (tag == "Fabric")  renderer.material = mover.Fabric;
            else if (tag == "Wood")    renderer.material = mover.Wood;
            else if (tag == "Glass")   renderer.material = mover.Glass;
            else if (tag == "Plastic") renderer.material = mover.Plastic;
            else if (tag == "Metal")   renderer.material = mover.Metal;
        }
    }
}