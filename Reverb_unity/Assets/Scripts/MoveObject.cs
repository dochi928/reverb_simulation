using UnityEngine;

public class MoveObject : MonoBehaviour
{
    public Transform ampAnchor;
    public Transform channel;
    GameObject selectedObject;

    public Material Cement;
    public Material Wood;
    public Material Fabric;
    public Material Glass;

    enum SelectMode { Object, Amp, Channel }
    enum MaterialType { Cement, Wood, Fabric, Glass }

    SelectMode currentMode = SelectMode.Object;

    public float moveStep = 0.05f;
    public float rotateStep = 1f;
    public float scaleStep = 0.1f;

    public void SetSelectedObject(GameObject obj)
    {
        selectedObject = obj;
        currentMode = SelectMode.Object;
    }

    public void SelectAmp()
    {
        selectedObject = null;
        currentMode = SelectMode.Amp;
    }

    public void SelectChannel()
    {
        selectedObject = null;
        currentMode = SelectMode.Channel;
    }

    public void Move(Vector3 dir)
    {
        switch (currentMode)
        {
            case SelectMode.Amp: ampAnchor.position += dir * moveStep; break;
            case SelectMode.Channel: channel.position += dir * moveStep; break;
            case SelectMode.Object:
                if (selectedObject != null) selectedObject.transform.position += dir * moveStep;
                break;
        }
    }

    public void Rotate(float dir)
    {
        switch (currentMode)
        {
            case SelectMode.Amp: ampAnchor.Rotate(Vector3.up, dir * rotateStep); break;
            case SelectMode.Channel:
                channel.RotateAround(channel.position, Vector3.up, dir * rotateStep);
                break;
            case SelectMode.Object:
                if (selectedObject != null) selectedObject.transform.Rotate(Vector3.up, dir * rotateStep);
                break;
        }
    }

    // --- X축 스케일링 분화 ---
    public void ScaleX(float dir)
    {
        if (currentMode != SelectMode.Object || selectedObject == null) return;
        Vector3 scale = selectedObject.transform.localScale;
        scale.x = Mathf.Max(0.2f, scale.x + (dir * scaleStep));
        selectedObject.transform.localScale = scale;
    }

    // --- Z축 스케일링 분화 ---
    public void ScaleZ(float dir)
    {
        if (currentMode != SelectMode.Object || selectedObject == null) return;
        Vector3 scale = selectedObject.transform.localScale;
        scale.z = Mathf.Max(0.2f, scale.z + (dir * scaleStep));
        selectedObject.transform.localScale = scale;
    }

    public void ChangeMaterial()
    {
        if (currentMode != SelectMode.Object || selectedObject == null) return;
        Renderer r = selectedObject.GetComponent<Renderer>();
        if (r == null) return;

        MaterialType currentType = GetMaterialType(selectedObject.tag);
        MaterialType nextType = (MaterialType)(((int)currentType + 1) % 4);
        ApplyMaterial(nextType, selectedObject, r);
    }

    MaterialType GetMaterialType(string tag)
    {
        switch (tag)
        {
            case "Wood": return MaterialType.Wood;
            case "Fabric": return MaterialType.Fabric;
            case "Glass": return MaterialType.Glass;
            default: return MaterialType.Cement;
        }
    }

    void ApplyMaterial(MaterialType type, GameObject obj, Renderer r)
    {
        switch (type)
        {
            case MaterialType.Cement: obj.tag = "Cement"; r.material = Cement; break;
            case MaterialType.Wood: obj.tag = "Wood"; r.material = Wood; break;
            case MaterialType.Fabric: obj.tag = "Fabric"; r.material = Fabric; break;
            case MaterialType.Glass: obj.tag = "Glass"; r.material = Glass; break;
        }
    }
}