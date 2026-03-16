using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum SimulationMode { Single, Multi }

public struct SimRayBand
{
    public float dist;
    public Vector3 position;
    public float[] bandVolumes;
    public Vector3 dir;

    public SimRayBand(Vector3 _pos, float _baseVol, Vector3 _dir, int bandCount, float _dist = 0f)
    {
        dist = _dist;
        position = _pos;
        dir = _dir.normalized;
        bandVolumes = new float[bandCount];
        for (int i = 0; i < bandCount; i++) bandVolumes[i] = _baseVol;
    }
}

public class MultiBandSimulator : MonoBehaviour
{
    public Transform ampAnchor;
    public Transform channel;
    public SimulationMode currentMode = SimulationMode.Multi;

    [Header("Raycast setting")]
    public float rayAng = 120f;
    public int raycount = 1200;
    public float maxdist = 340f;
    public int diffusion_num = 5;
    public bool drawRay = true;

    [Header("Interference (7 Octave Bands)")]
    public float[] eqFrequencies = { 100f, 200f, 400f, 800f, 1600f, 3200f, 6400f };

    [Header("Reflection (Per Band)")]
    public float[] cement_r = { 0.99f, 0.99f, 0.98f, 0.98f, 0.98f, 0.97f, 0.95f };
    public float[] fabric_r = { 0.95f, 0.85f, 0.70f, 0.55f, 0.40f, 0.30f, 0.20f };
    public float[] wood_r   = { 0.85f, 0.90f, 0.95f, 0.95f, 0.92f, 0.90f, 0.85f };
    public float[] glass_r  = { 0.95f, 0.97f, 0.98f, 0.98f, 0.95f, 0.92f, 0.90f };

    [Header("Air Absorption (Per Band)")]
    public float[] airAbsBands = { 0.0001f, 0.0002f, 0.0005f, 0.0012f, 0.003f, 0.01f, 0.03f };

    [Header("Physics setting")]
    public float PreDelay = 0.02f;
    public float decayDist = 2f;

    Dictionary<string, Vector2[]> phaseBuffer = new Dictionary<string, Vector2[]>();
    Dictionary<string, float> distBuffer = new Dictionary<string, float>();
    Dictionary<string, int> countBuffer = new Dictionary<string, int>();
    Queue<SimRayBand> rayQueue = new Queue<SimRayBand>();
    string path;

    void Start() { path = Application.dataPath + "/Results/reverb_result.txt"; }

    public void ToggleMode() { currentMode = (currentMode == SimulationMode.Multi) ? SimulationMode.Single : SimulationMode.Multi; }

    public void RunSimulation()
    {
        phaseBuffer.Clear(); distBuffer.Clear(); countBuffer.Clear(); rayQueue.Clear();
        Vector3 origin = ampAnchor.position;
        float start = -rayAng / 2f;
        float step = rayAng / Mathf.Max(1, (raycount - 1));

        for (int i = 0; i < raycount; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(start + step * i, Vector3.up) * ampAnchor.forward;
            rayQueue.Enqueue(new SimRayBand(origin, 1f, dir, eqFrequencies.Length));
        }

        while (rayQueue.Count > 0) CastRay(rayQueue.Dequeue());
        Debug.Log(currentMode + " Simulation Complete!");
    }

    void CastRay(SimRayBand r)
    {
        RaycastHit hit;
        if (Physics.Raycast(r.position, r.dir, out hit, maxdist))
        {
            float stepDist = hit.distance;
            float totalDist = r.dist + stepDist;
            if (hit.collider.CompareTag("Channel_L") || hit.collider.CompareTag("Channel_R"))
            {
                saveParams(totalDist, r.bandVolumes, hit.collider.tag.EndsWith("L") ? "L" : "R");
                return;
            }

            float[] coeffs = GetBandCoeffs(hit.collider.tag);
            Vector3 reflect = Vector3.Reflect(r.dir, hit.normal);

            for (int i = 0; i < diffusion_num + 1; i++)
            {
                float diff = (i != 0) ? Random.Range(-10f, 10f) : 0f;
                Vector3 newDir = Quaternion.AngleAxis(diff, Vector3.up) * reflect;
                float[] nextVolumes = new float[eqFrequencies.Length];
                bool isAlive = false;

                for (int b = 0; b < eqFrequencies.Length; b++)
                {
                    // Single 모드일 경우 모든 대역에 800Hz(Index 3) 기준의 공기 흡수와 반사율 적용
                    int targetIdx = (currentMode == SimulationMode.Single) ? 3 : b;
                    float envDecay = (decayDist / (totalDist + decayDist)) * Mathf.Exp(-airAbsBands[targetIdx] * stepDist);
                    float diffRate = (i == 0) ? 0.8f : 0.2f / diffusion_num;
                    nextVolumes[b] = r.bandVolumes[b] * coeffs[targetIdx] * envDecay * diffRate;
                    if (nextVolumes[b] > 0.0005f) isAlive = true;
                }

                if (isAlive)
                {
                    SimRayBand nextRay = new SimRayBand(hit.point, 0f, newDir, eqFrequencies.Length, totalDist);
                    nextRay.bandVolumes = nextVolumes;
                    rayQueue.Enqueue(nextRay);
                }
            }
        }
    }

    float[] GetBandCoeffs(string tag)
    {
        if (tag == "Cement") return cement_r;
        if (tag == "Fabric") return fabric_r;
        if (tag == "Wood") return wood_r;
        if (tag == "Glass") return glass_r;
        return new float[] { 1, 1, 1, 1, 1, 1, 1 };
    }

    void saveParams(float dist, float[] volumes, string lr)
    {
        int timeMS = Mathf.RoundToInt((dist / 340f + PreDelay) * 1000f);
        string key = lr + "_" + timeMS;
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

    public void SaveToFile()
    {
        using (StreamWriter writer = new StreamWriter(path, false))
        {
            float dDist = Vector3.Distance(ampAnchor.position, channel.position);
            writer.WriteLine($"DIRECT_INFO 0 {Mathf.RoundToInt((dDist/340f+PreDelay)*1000f)} 0 {dDist:F4}");
            writer.WriteLine($"MODE: {currentMode}");

            var sortedKeys = phaseBuffer.Keys.OrderBy(k => k.Split('_')[0] + int.Parse(k.Split('_')[1]).ToString("D8")).ToList();

            if (currentMode == SimulationMode.Multi)
            {
                for (int i = 0; i < eqFrequencies.Length; i++)
                {
                    writer.WriteLine($"{eqFrequencies[i]}Hz");
                    foreach (string key in sortedKeys)
                    {
                        string[] s = key.Split('_');
                        int vol = Mathf.Clamp(Mathf.RoundToInt(phaseBuffer[key][i].magnitude * 1000f), 0, 1000);
                        writer.WriteLine($"{s[0]} {s[1]} {vol} {(distBuffer[key]/countBuffer[key]):F4}");
                    }
                    writer.WriteLine("");
                }
            }
            else // Single 모드: 800Hz(대표 주파수) 데이터만 저장
            {
                writer.WriteLine("800Hz_Standard");
                foreach (string key in sortedKeys)
                {
                    string[] s = key.Split('_');
                    int vol = Mathf.Clamp(Mathf.RoundToInt(phaseBuffer[key][3].magnitude * 1000f), 0, 1000);
                    writer.WriteLine($"{s[0]} {s[1]} {vol} {(distBuffer[key]/countBuffer[key]):F4}");
                }
            }
        }
        Debug.Log("File Saved: " + currentMode);
    }
}