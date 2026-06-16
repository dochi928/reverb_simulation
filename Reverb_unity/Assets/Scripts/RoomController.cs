using UnityEngine;

public class RoomController : MonoBehaviour
{
    [Header("Room Object")]
    public Transform floor;
    public Transform wall_L;  // Wall_1 (왼쪽)
    public Transform wall_R;  // Wall_2 (오른쪽)
    public Transform wall_U;  // Wall_3 (위)
    public Transform wall_D;  // Wall_4 (아래)

    [Header("Room Size")]
    public float roomWidth = 10f;   // 내부 너비 (X축)
    public float roomDepth = 10f;   // 내부 깊이 (Z축)
    public float wallThicknessLR = 0.5f;  // 좌우 벽 두께
    public float wallThicknessUD = 0.5f;  // 상하 벽 두께

    public float minSize = 2f;

    private float baseRoomWidth;
    private float baseRoomDepth;
    private float initialFloorScaleX;
    private float initialFloorScaleZ;

    void Awake()
    {
        baseRoomWidth = roomWidth;
        baseRoomDepth = roomDepth;

        initialFloorScaleX = floor.localScale.x;
        initialFloorScaleZ = floor.localScale.z;

        // 시작 시 Inspector 값 기준으로 정규화
        ApplyRoomSize();
    }

    public void ResizeRoom(float dWidth, float dDepth)
    {
        roomWidth = Mathf.Max(minSize, roomWidth + dWidth);
        roomDepth = Mathf.Max(minSize, roomDepth + dDepth);
        ApplyRoomSize();
    }

    public void SetRoomSize(float width, float depth)
    {
        roomWidth = Mathf.Max(minSize, width);
        roomDepth = Mathf.Max(minSize, depth);
        ApplyRoomSize();
    }

    private void ApplyRoomSize()
    {
        float halfW = roomWidth / 2f;
        float halfD = roomDepth / 2f;

        // Floor
        floor.localScale = new Vector3(
            initialFloorScaleX * (roomWidth / baseRoomWidth),
            floor.localScale.y,
            initialFloorScaleZ * (roomDepth / baseRoomDepth)
        );

        // 좌우 벽: Z = 내부 깊이 + 상하 벽 두께*2 (코너 포함)
        float wallLR_Z = roomDepth + wallThicknessUD * 2f;
        wall_L.localScale = new Vector3(wallThicknessLR, wall_L.localScale.y, wallLR_Z);
        wall_R.localScale = new Vector3(wallThicknessLR, wall_R.localScale.y, wallLR_Z);
        wall_L.position = new Vector3(-halfW - wallThicknessLR / 2f, wall_L.position.y, 0);
        wall_R.position = new Vector3( halfW + wallThicknessLR / 2f, wall_R.position.y, 0);

        // 상하 벽: X = 내부 너비만 (좌우 벽이 코너 담당)
        wall_U.localScale = new Vector3(roomWidth, wall_U.localScale.y, wallThicknessUD);
        wall_D.localScale = new Vector3(roomWidth, wall_D.localScale.y, wallThicknessUD);
        wall_U.position = new Vector3(0, wall_U.position.y,  halfD + wallThicknessUD / 2f);
        wall_D.position = new Vector3(0, wall_D.position.y, -halfD - wallThicknessUD / 2f);
    }

    public float GetWidth() => roomWidth;
    public float GetDepth() => roomDepth;
}