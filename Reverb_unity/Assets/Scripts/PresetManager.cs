using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System; // Math.Round 사용을 위해 추가

public class PresetManager : MonoBehaviour
{
    public GameObject blockPrefab;
    public Transform objectParent;
    public MoveObject mover;

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

    // 이산화(Rounding) 처리를 위한 헬퍼 함수
    private string FormatVec3(Vector3 vec, int posDecimals, int rotDecimals)
    {
        float x = (float)Math.Round(vec.x, posDecimals);
        float y = (float)Math.Round(vec.y, posDecimals);
        float z = (float)Math.Round(vec.z, posDecimals);
        
        // 각도의 경우 rotDecimals가 0이면 정수로 저장됨 (1도 단위)
        if (rotDecimals == 0)
        {
            return $"{(int)Math.Round(vec.x)},{(int)Math.Round(vec.y)},{(int)Math.Round(vec.z)}";
        }
        return $"{x},{y},{z}";
    }

    public void SaveRoom(int index)
    {
        StringBuilder sb = new StringBuilder();

        // 1. 앰프: 좌표 0.1단위(1), 각도 1도단위(0)
        sb.AppendLine($"AMP POS:{FormatVec3(ampAnchor.position, 1, 1)} ROT:{FormatVec3(ampAnchor.eulerAngles, 0, 0)}");
        
        // 2. 채널 앵커: 좌표 0.1단위(1), 각도 1도단위(0)
        sb.AppendLine($"CHA POS:{FormatVec3(channelAnchor.position, 1, 1)} ROT:{FormatVec3(channelAnchor.eulerAngles, 0, 0)}");

        // 3. 일반 오브젝트
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
            if (tag == "Cement") renderer.material = mover.Cement;
            else if (tag == "Fabric") renderer.material = mover.Fabric;
            else if (tag == "Wood") renderer.material = mover.Wood;
            else if (tag == "Glass") renderer.material = mover.Glass;
        }
    }
}