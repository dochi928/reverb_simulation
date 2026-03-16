using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public struct SimRay
{
    public float dist;
    public Vector3 position;
    public float volume;
    public Vector3 dir;

    public SimRay(Vector3 _position, float _volume, float _angle, float _dist = 0f)
    {
        dist = _dist;
        position = _position;
        volume = _volume;
        dir = new Vector3(Mathf.Cos(_angle), 0, Mathf.Sin(_angle));
    }

    public SimRay(Vector3 _position, float _volume, Vector3 _dir, float _dist = 0f)
    {
        dist = _dist;
        position = _position;
        volume = _volume;
        dir = _dir.normalized;
    }
}

public class Simulator : MonoBehaviour
{
    public Transform ampAnchor;
    public Transform channel;
    
    [Header("Raycast setting")]
    public float rayAng = 120f;
    public int raycount = 1200;
    public float maxdist = 340f;
    public int diffusion_num = 5;
    public bool drawRay = true;

    Queue<SimRay> rayQueue = new Queue<SimRay>();

    [Header("Effect setting")]
    public float Diffusion = 10f;
    public float PreDelay = 0.02f;

    [Header("Interference (Guitar EQ Bands)")]
    // 기타 음색의 핵심이 되는 7개 옥타브 대역
    public float[] eqFrequencies = { 100f, 200f, 400f, 800f, 1600f, 3200f, 6400f };

    [Header("Energy option")]
    public bool energyConservation = true;

    [Header("Reflection rate")]
    public float cement_r = 0.97f;
    public float fabric_r = 0.65f;
    public float wood_r = 0.85f;
    public float glass_r = 0.95f;

    [Header("Diffusion Main Volume rate")]
    public float cement_d = 0.9f;
    public float fabric_d = 0.4f;
    public float wood_d = 0.8f;
    public float glass_d = 0.95f;

    [Header("Physics setting")]
    public float airAbs = 0.002f;
    public float decayDist = 2f;

    // 위상 간섭을 계산하기 위해 Vector2(실수부, 허수부) 구조 사용
    Dictionary<string, Vector2> phaseBuffer = new Dictionary<string, Vector2>();
    Dictionary<string, float> distBuffer = new Dictionary<string, float>();
    Dictionary<string, int> countBuffer = new Dictionary<string, int>();

    string path;

    void Start()
    {
        path = Application.dataPath + "/Results/reverb_result.txt";
    }

    public void RunSimulation()
    {
        phaseBuffer.Clear();
        distBuffer.Clear();
        countBuffer.Clear();
        rayQueue.Clear();

        Debug.Log("Simulation started (Multi-Frequency Phase Summation)");

        Vector3 origin = ampAnchor.position;
        float start = -rayAng / 2f;
        float step = rayAng / Mathf.Max(1, (raycount - 1));

        for (int i = 0; i < raycount; i++)
        {
            float ang = start + step * i;
            Vector3 dir = Quaternion.AngleAxis(ang, Vector3.up) * ampAnchor.forward;
            SimRay r = new SimRay(origin, 1f, dir);
            rayQueue.Enqueue(r);
        }

        while (rayQueue.Count > 0)
        {
            SimRay r = rayQueue.Dequeue();
            CastRay(r);
        }

        Debug.Log("Simulation Complete");
    }

    public void CastRay(SimRay r)
    {
        RaycastHit hit;
        if (Physics.Raycast(r.position, r.dir, out hit, maxdist))
        {
            float dist = hit.distance;
            Vector3 reflect = Vector3.Reflect(r.dir, hit.normal);
            Vector3 hitpoint = hit.point;

            if (drawRay)
                Debug.DrawRay(r.position, r.dir * dist, Color.green, 5f);

            for (int i = 0; i < diffusion_num + 1; i++)
            {
                float diff = (i != 0) ? Random.Range(-Diffusion, Diffusion) : 0f;
                Vector3 newDir = Quaternion.AngleAxis(diff, Vector3.up) * reflect;
                float volumescale = 0f;

                if (hit.collider.CompareTag("Channel_L"))
                {
                    saveParams(r.dist + dist, calVol(r.volume, r.dist + dist), "L");
                    return;
                }
                if (hit.collider.CompareTag("Channel_R"))
                {
                    saveParams(r.dist + dist, calVol(r.volume, r.dist + dist), "R");
                    return;
                }

                if (hit.collider.CompareTag("Cement")) { volumescale = (i == 0) ? cement_d : (1 - cement_d) / diffusion_num; volumescale *= cement_r; }
                else if (hit.collider.CompareTag("Fabric")) { volumescale = (i == 0) ? fabric_d : (1 - fabric_d) / diffusion_num; volumescale *= fabric_r; }
                else if (hit.collider.CompareTag("Wood")) { volumescale = (i == 0) ? wood_d : (1 - wood_d) / diffusion_num; volumescale *= wood_r; }
                else if (hit.collider.CompareTag("Glass")) { volumescale = (i == 0) ? glass_d : (1 - glass_d) / diffusion_num; volumescale *= glass_r; }

                if (!energyConservation && i == 0) volumescale = 1f;

                float newVol = calVol(volumescale * r.volume, r.dist + dist);
                if (newVol > 0.001f)
                {
                    SimRay newr = new SimRay(hitpoint, newVol, newDir, r.dist + dist);
                    rayQueue.Enqueue(newr);
                }
            }
        }
    }

    public float calVol(float volume, float dist)
    {
        float geometric = decayDist / (dist + decayDist);
        float air = Mathf.Exp(-airAbs * dist);
        return volume * geometric * air;
    }

    public void saveParams(float dist, float volume, string lr)
    {
        float time = dist / 340f;
        time += PreDelay;
        int timeMS = Mathf.RoundToInt(time * 1000f);
        string key = lr + "_" + timeMS;

        // --- 핵심: 7개 주파수에 대한 복소 위상 벡터 합산 ---
        Vector2 combinedPhaseVector = Vector2.zero;

        foreach (float freq in eqFrequencies)
        {
            float wavelength = 340f / freq;
            float phase = (dist % wavelength) / wavelength * 2f * Mathf.PI;
            
            // 각 주파수의 위상을 벡터로 변환하여 누적
            combinedPhaseVector.x += volume * Mathf.Cos(phase);
            combinedPhaseVector.y += volume * Mathf.Sin(phase);
        }
        
        // 7개 대역의 평균 위상 벡터를 저장
        combinedPhaseVector /= eqFrequencies.Length;

        if (!phaseBuffer.ContainsKey(key))
        {
            phaseBuffer[key] = combinedPhaseVector;
            distBuffer[key] = dist;
            countBuffer[key] = 1;
        }
        else
        {
            phaseBuffer[key] += combinedPhaseVector;
            distBuffer[key] += dist;
            countBuffer[key] += 1;
        }
    }

    public void SaveToFile()
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);

        StreamWriter writer = new StreamWriter(path, false);

        if (ampAnchor != null && channel != null)
        {
            float directDist = Vector3.Distance(ampAnchor.position, channel.position);
            int directTimeMS = Mathf.RoundToInt((directDist / 340f + PreDelay) * 1000f);
            writer.WriteLine("DIRECT_INFO 0 " + directTimeMS + " 0 " + directDist.ToString("F4"));
        }

        var sortedList = phaseBuffer.OrderBy(pair => {
            string[] split = pair.Key.Split('_');
            return split[0] + int.Parse(split[1]).ToString("D8"); 
        });

        foreach (var pair in sortedList)
        {
            string[] split = pair.Key.Split('_');
            string lr = split[0];
            string time = split[1];

            // 벡터의 최종 길이를 구하여 간섭 결과가 반영된 볼륨 산출
            float finalVolume = pair.Value.magnitude;
            int volPercent = Mathf.Clamp(Mathf.RoundToInt(finalVolume * 1000f), 0, 1000);

            if (volPercent <= 0) continue;

            float avgDist = distBuffer[pair.Key] / countBuffer[pair.Key];
            writer.WriteLine(lr + " " + time + " " + volPercent + " " + avgDist.ToString("F4"));
        }

        writer.Close();
        Debug.Log("Result saved with Guitar EQ Band Interference");
    }
}