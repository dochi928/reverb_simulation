using UnityEngine;
using TMPro; // TextMeshPro 사용을 위해 필수

public class SceneManager : MonoBehaviour
{
    [Header("Core Components")]
    public MoveObject mover;
    public MultiBandSimulator simulator;
    public PresetManager presetManager;
    public GameObject blockPrefab;

    [Header("UI Settings")]
    public TextMeshProUGUI modeText;

    private int currentRoomIndex = 0;

    void Start()
    {
        // 시작 시 초기 UI 상태 업데이트
        UpdateUI();
    }

    void Update()
    {
        // 1. 선택 및 물체 생성
        HandleSelection();
        HandleCreateObject();

        // 2. 조작 (이동, 회전, 스케일)
        HandleMovement();

        // 3. 시뮬레이션 제어 (모드 전환, 실행, 저장)
        HandleModeToggle();
        HandleSimulation();

        // 4. 프리셋 관리 (<, >, P)
        HandlePresetInput();

        // 기타 (재질 변경 등)
        if (Input.GetKeyDown(KeyCode.W)) mover.ChangeMaterial();
    }

    // 마우스 클릭으로 오브젝트 선택
    void HandleSelection()
    {
        if (!Input.GetMouseButtonDown(0)) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit))
        {
            // Channel이나 Amp가 아닌 일반 오브젝트(벽)만 선택하도록 설정 가능
            mover.SetSelectedObject(hit.collider.gameObject);
        }
    }

    // 방향키 및 단축키 조작 로직
    void HandleMovement()
    {
        // 위치 이동
        if (Input.GetKey(KeyCode.UpArrow)) mover.Move(Vector3.forward);
        if (Input.GetKey(KeyCode.DownArrow)) mover.Move(Vector3.back);
        if (Input.GetKey(KeyCode.LeftArrow)) mover.Move(Vector3.left);
        if (Input.GetKey(KeyCode.RightArrow)) mover.Move(Vector3.right);

        // 회전 (A, D)
        if (Input.GetKey(KeyCode.A)) mover.Rotate(-1);
        if (Input.GetKey(KeyCode.D)) mover.Rotate(1);

        // 스케일 (X축: Z/X, Z축: C/V)
        if (Input.GetKey(KeyCode.Z)) mover.ScaleX(1);
        if (Input.GetKey(KeyCode.X)) mover.ScaleX(-1);
        if (Input.GetKey(KeyCode.C)) mover.ScaleZ(1);
        if (Input.GetKey(KeyCode.V)) mover.ScaleZ(-1);

        // 선택 대상 전환 (앰프/채널 직접 선택 모드)
        if (Input.GetKeyDown(KeyCode.Alpha2)) mover.SelectAmp();
        if (Input.GetKeyDown(KeyCode.Alpha3)) mover.SelectChannel();
    }

    // 시뮬레이션 실행 및 모드 전환 (Tab, E, R, Y)
    void HandleModeToggle()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (simulator != null)
            {
                simulator.ToggleMode();
                UpdateUI();
            }
        }
    }

    void HandleSimulation()
    {
        if (simulator == null) return;

        if (Input.GetKeyDown(KeyCode.E)) simulator.RunSimulation();
        if (Input.GetKeyDown(KeyCode.R)) simulator.SaveToFile();
        if (Input.GetKeyDown(KeyCode.Y)) simulator.drawRay = !simulator.drawRay;
    }

    // 프리셋 시스템 연동 (<, >, P)
    void HandlePresetInput()
    {
        if (presetManager == null) return;

        // < (Comma) 키: 이전 프리셋 로드
        if (Input.GetKeyDown(KeyCode.Comma))
        {
            currentRoomIndex = Mathf.Max(0, currentRoomIndex - 1);
            presetManager.LoadRoom(currentRoomIndex);
            UpdateUI();
        }
        // > (Period) 키: 다음 프리셋 로드
        if (Input.GetKeyDown(KeyCode.Period))
        {
            currentRoomIndex++;
            presetManager.LoadRoom(currentRoomIndex);
            UpdateUI();
        }
        // P 키: 현재 룸 저장
        if (Input.GetKeyDown(KeyCode.P))
        {
            if (currentRoomIndex > 0)
            {
                presetManager.SaveRoom(currentRoomIndex);
            }
            else
            {
                Debug.LogWarning("Room 0은 기본 상태이므로 저장할 수 없습니다. 번호를 올려주세요.");
            }
        }
    }

    // 신규 오브젝트 생성 (Q)
    void HandleCreateObject()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            // 핵심: 4번째 인자로 presetManager.objectParent를 넣어줍니다.
            GameObject newObj = Instantiate(blockPrefab, new Vector3(0, 0.5f, 0), Quaternion.identity, presetManager.objectParent);
        
            mover.SetSelectedObject(newObj);
        }
    }

    // 화면 상단 UI 텍스트 갱신
    public void UpdateUI()
    {
        if (modeText != null && simulator != null)
        {
            string modeStr = (simulator.currentMode == SimulationMode.Multi) ? "Multi" : "Single";
            modeText.text = $"Room: <color=#FFD700>{currentRoomIndex}</color> | Mode: <color=#00FFFF>{modeStr}</color>";
        }
    }
}