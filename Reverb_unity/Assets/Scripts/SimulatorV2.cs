using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public struct SimRay2
{
    public float dist;
    public float[] vol;
    public Vector3 dir;
    public Vector3 pos;

    public SimRay2(Vector3 _pos, Vector3 _dir, float _vol, int _count = 7)
    {
        pos = _pos;
        dir = _dir.normalized;
        dist = 0f;
        vol = new float[_count];
        for (int i = 0; i < _count; i++) vol[i] = _vol;
    }
    public SimRay2(Vector3 _pos, Vector3 _dir, float[] _vol)
    {
        pos = _pos;
        dir = _dir.normalized;
        dist = 0f;
        vol = _vol;
    }
}

public class SimulatorV2 : MonoBehaviour
{
    public Transform ampAnchor;
    public Transform channelL;
    public Transform channelR;

    [Header("Setting")]
    public float VolResolution = 1000000f;
    public float TimeResolution = 640000f;

    [Header("Raycast setting")]
    public float rayAng = 120f;
    public int raycount = 1200;
    public float maxdist = 340f;
    public float dist0 = 1f;

    [Header("Interference (7 Octave Bands)")]
    public float[] eqFrequencies = { 100f, 200f, 400f, 800f, 1600f, 3200f, 6400f };

    [Header("Reflection (Per Band)")]
    public float[] cement_r  = { 0.99f, 0.99f, 0.98f, 0.98f, 0.98f, 0.97f, 0.95f };
    public float[] fabric_r  = { 0.95f, 0.85f, 0.70f, 0.55f, 0.40f, 0.30f, 0.20f };
    public float[] wood_r    = { 0.85f, 0.90f, 0.95f, 0.95f, 0.92f, 0.90f, 0.85f };
    public float[] glass_r   = { 0.95f, 0.97f, 0.98f, 0.98f, 0.95f, 0.92f, 0.90f };
    public float[] plastic_r = { 0.80f, 0.82f, 0.83f, 0.80f, 0.72f, 0.60f, 0.45f };
    public float[] metal_r   = { 0.97f, 0.97f, 0.96f, 0.96f, 0.95f, 0.94f, 0.93f };

    [Header("Air Absorption (Per Band)")] public float[] airAbsBands;
    float calAlpha(int f)
    {
        float c = 1.84e-11f;
        float o2 = 0.2703f / (1.9585e9f + f * f);
        float n2 = 4.574e-4f / (1.5709e5f + f * f);
        return (c + o2 + n2) * f * f;
    }

    Dictionary<string, Vector2[]> phaseBuffer = new Dictionary<string, Vector2[]>();
    Dictionary<string, float> distBuffer = new Dictionary<string, float>();
    Dictionary<string, int> countBuffer = new Dictionary<string, int>();
    Queue<SimRay2> rayQueue = new Queue<SimRay2>();
    string path;

    void Start()
    {
        path = Application.dataPath + "/Results/reverb_result_v2.txt";
        int f0 = 100;
        airAbsBands = new float[eqFrequencies.Length];
        for (int i = 0; i < eqFrequencies.Length; i++)
        {
            airAbsBands[i] = calAlpha(f0);
            f0 *= 2;
        }
    }

    public void RunSimulation()
    {
        phaseBuffer.Clear();
        distBuffer.Clear();
        countBuffer.Clear();
        rayQueue.Clear();

        Vector3 origin = ampAnchor.position;
        float start = -rayAng / 2f;
        float step = rayAng / Mathf.Max(1, raycount - 1);

        for (int i = 0; i < raycount; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(start + step * i, Vector3.up) * ampAnchor.forward;
            rayQueue.Enqueue(new SimRay2(origin, dir, VolResolution, eqFrequencies.Length));
        }

        while (rayQueue.Count > 0) CastRay(rayQueue.Dequeue());
        Debug.Log("Simulation V2 Complete!");
    }

    float[] GetBandCoeffs(string tag)
    {
        if (tag == "Cement")  return cement_r;
        if (tag == "Fabric")  return fabric_r;
        if (tag == "Wood")    return wood_r;
        if (tag == "Glass")   return glass_r;
        if (tag == "Plastic") return plastic_r;
        if (tag == "Metal")   return metal_r;
        return new float[] { 1, 1, 1, 1, 1, 1, 1 };
    }
    
    void saveParams(float dist, float[] volumes, string lr)
    {
        int timeuS = Mathf.RoundToInt((dist / 340f) * TimeResolution);
        string key = lr + "_" + timeuS;
        if (!phaseBuffer.ContainsKey(key))
        {
            phaseBuffer[key] = new Vector2[eqFrequencies.Length];
            distBuffer[key] = 0; countBuffer[key] = 0;
        }
        for (int i = 0; i < eqFrequencies.Length; i++)
        {
            float wavelength = 340f / eqFrequencies[i];
            float phase = (dist % wavelength) / wavelength * 2f * Mathf.PI;
            phaseBuffer[key][i].x += volumes[i] * Mathf.Cos(phase);
            phaseBuffer[key][i].y += volumes[i] * Mathf.Sin(phase);
        }
        distBuffer[key] += dist; countBuffer[key]++;
    }

    float GetSurfaceDist(Transform target)
    {
        Vector3 origin = ampAnchor.position;
        Vector3 toTarget = target.position - origin;
        RaycastHit h;
        if (Physics.Raycast(origin, toTarget, out h, toTarget.magnitude))
        {
            if (h.collider.CompareTag("Channel_L") || h.collider.CompareTag("Channel_R"))
                return h.distance;
        }
        return toTarget.magnitude;
    }

    public void SaveToFile()
    {
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            float dDistL = GetSurfaceDist(channelL);
            float dDistR = GetSurfaceDist(channelR);
            writer.WriteLine($"DIRECT_INFO_L 0 {Mathf.RoundToInt((dDistL / 340f) * TimeResolution)} 0 {dDistL:F6}");
            writer.WriteLine($"DIRECT_INFO_R 0 {Mathf.RoundToInt((dDistR / 340f) * TimeResolution)} 0 {dDistR:F6}");
            writer.WriteLine($"MODE: Multi");
            writer.WriteLine($"Time Resolution : {TimeResolution}");
            writer.WriteLine($"Volume Resolution : {VolResolution}");

            var sortedKeys = phaseBuffer.Keys.OrderBy(k => k.Split('_')[0] + int.Parse(k.Split('_')[1]).ToString("D8")).ToList();

            for (int i = 0; i < eqFrequencies.Length; i++)
            {
                writer.WriteLine($"{eqFrequencies[i]}Hz");
                foreach (string key in sortedKeys)
                {
                    string[] s = key.Split('_');
                    int vol = Mathf.Clamp(Mathf.RoundToInt(phaseBuffer[key][i].magnitude / raycount), 0, (int)VolResolution);
                    if (vol < 1) continue;
                    writer.WriteLine($"{s[0]} {s[1]} {vol} {(distBuffer[key] / countBuffer[key]):F6}");
                }
                writer.WriteLine("");
            }
        }
        Debug.Log("File Saved: V2");
    }

    void CheckLOS(SimRay2 r2, Transform target, string lr)
    {
        Vector3 toTarget = target.position - r2.pos;
        RaycastHit h;
        if (Physics.Raycast(r2.pos, toTarget, out h, toTarget.magnitude))
        {
            if (h.collider.CompareTag("Channel_L") || h.collider.CompareTag("Channel_R"))
            {
                float vDist = h.distance + r2.dist;
                float[] nV = (float[])r2.vol.Clone();
                float vDecay = dist0 / (dist0 + Mathf.Sqrt(h.distance));
                for (int i = 0; i < eqFrequencies.Length; i++)
                {
                    float d = Mathf.Exp(-airAbsBands[i] * h.distance);
                    nV[i] *= d * vDecay;
                }
                saveParams(vDist, nV, lr);
            }
        }
    }

    void CastRay(SimRay2 r2)
    {
        CheckLOS(r2, channelL, "L");
        CheckLOS(r2, channelR, "R");

        RaycastHit hit;
        if (Physics.Raycast(r2.pos, r2.dir, out hit, maxdist))
        {
            float stepDist = hit.distance;
            if (hit.collider.CompareTag("Channel_L") || hit.collider.CompareTag("Channel_R")) return;

            float[] coeffs = GetBandCoeffs(hit.collider.tag);
            Vector3 reflect = Vector3.Reflect(r2.dir, hit.normal);
            float[] nextVol = new float[eqFrequencies.Length];
            bool isAlive = false;
            float gDecay = dist0 + (dist0 + Mathf.Sqrt(stepDist));

            for (int i = 0; i < eqFrequencies.Length; i++)
            {
                if (r2.vol[i] < 1f) continue;
                float decay = Mathf.Exp(-airAbsBands[i] * stepDist);
                nextVol[i] = r2.vol[i] * coeffs[i] * decay * gDecay;
                if (nextVol[i] > 1) isAlive = true;
                else nextVol[i] = 0f;
            }

            if (isAlive)
            {
                SimRay2 nextRay = new SimRay2(hit.point, reflect, nextVol);
                nextRay.dist = r2.dist + stepDist;
                rayQueue.Enqueue(nextRay);
            }
        }
    }
}