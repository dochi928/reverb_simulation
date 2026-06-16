using UnityEngine;
using TMPro; // TextMeshPro 사용을 위해 필수

public enum SimEngineMode { V2, Multi, Single }

public class SceneManager : MonoBehaviour
{
    [Header("Core Components")]
    public MoveObject mover;
    public MultiBandSimulator simulator;
    public SimulatorV2 simulatorV2;
    public PresetManager presetManager;
    public GameObject blockPrefab;

    [Header("UI Settings")]
    public TextMeshProUGUI modeText;

    private int currentRoomIndex = 0;
    private SimEngineMode currentEngineMode = SimEngineMode.V2;

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
        if (Input.GetKeyDown(KeyCode.Alpha4)) mover.SelectRoom();
    }

    // 시뮬레이션 엔진/모드 전환 (Tab): V2 -> Multi -> Single -> V2 ...
    void HandleModeToggle()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            switch (currentEngineMode)
            {
                case SimEngineMode.V2:
                    currentEngineMode = SimEngineMode.Multi;
                    // MultiBandSimulator를 Multi 모드로 명시
                    if (simulator != null && simulator.currentMode != SimulationMode.Multi)
                        simulator.ToggleMode();
                    break;

                case SimEngineMode.Multi:
                    currentEngineMode = SimEngineMode.Single;
                    // MultiBandSimulator를 Single 모드로 전환
                    if (simulator != null && simulator.currentMode != SimulationMode.Single)
                        simulator.ToggleMode();
                    break;

                case SimEngineMode.Single:
                    currentEngineMode = SimEngineMode.V2;
                    break;
            }
            UpdateUI();
        }
    }

    void HandleSimulation()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            switch (currentEngineMode)
            {
                case SimEngineMode.V2:
                    if (simulatorV2 != null) simulatorV2.RunSimulation();
                    break;
                case SimEngineMode.Multi:
                case SimEngineMode.Single:
                    if (simulator != null) simulator.RunSimulation();
                    break;
            }
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            switch (currentEngineMode)
            {
                case SimEngineMode.V2:
                    if (simulatorV2 != null) simulatorV2.SaveToFile();
                    break;
                case SimEngineMode.Multi:
                case SimEngineMode.Single:
                    if (simulator != null) simulator.SaveToFile();
                    break;
            }
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (simulator != null) simulator.drawRay = !simulator.drawRay;
        }
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
        if (modeText != null)
        {
            string modeStr;
            switch (currentEngineMode)
            {
                case SimEngineMode.V2:     modeStr = "V2";     break;
                case SimEngineMode.Multi:  modeStr = "Multi";  break;
                case SimEngineMode.Single: modeStr = "Single"; break;
                default:                   modeStr = "?";      break;
            }
            modeText.text = $"Room: <color=#FFD700>{currentRoomIndex}</color> | Mode: <color=#00FFFF>{modeStr}</color>";
        }
    }
}