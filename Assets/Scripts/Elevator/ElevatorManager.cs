using UnityEngine;
using System.Collections;

public class ElevatorManager : MonoBehaviour
{
    public enum ElevatorType
    {
        Normal,
        Finish,
        RestArea
    }

    [Header("엘리베이터 타입")]
    public ElevatorType currentType = ElevatorType.Normal;

    [Header("오브젝트 할당")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private Transform playerTransform;

    [Header("시각 효과")]
    [SerializeField] private Light statusLight;
    [SerializeField] private Color lockedColor = Color.red;
    [SerializeField] private Color unlockedColor = Color.green;

    [Header("설정")]
    [SerializeField] private float restAreaWaitTime = 10f;
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private float fadeSpeed = 2f;

    [Header("트리거")]
    [SerializeField] private GameObject doorTriggerObject;
    [SerializeField] private GameObject insideTriggerObject;

    [Header("심리스 연출 설정")]
    // [설정] 안 보이게 가릴 레이어들을 선택하세요 (Map, Wall, Default 등)
    public LayerMask hideLayerMask;
    private int originalCullingMask;
    private Camera mainCam;
    private bool isViewLocked = false;

    private Vector3 leftDoorClosedPos, leftDoorOpenPos;
    private Vector3 rightDoorClosedPos, rightDoorOpenPos;
    private bool doorsOpen = false;
    private bool isProcessing = false;
    private bool isPlayerInside = false;
    private Transform currentDestination;
    private bool isLocked = false;

    // 타이머 중복 실행 방지 변수
    private bool isRestTimerStarted = false;

    public static ElevatorManager RestAreaInstance;

    void Awake()
    {
        if (currentType == ElevatorType.RestArea) RestAreaInstance = this;
        // [필수] 위치 계산은 여기서 해야 0,0,0 이동 버그가 없습니다.
        CalculateDoorPositions();
        FindComponents();
    }

    // =========================================================
    // [해결] Start 함수 부활! (단, 조건부 실행)
    // =========================================================
    void Start()
    {
        // "레스트룸 엘리베이터"는 GameManager가 타이밍 맞춰서 Initialize() 해줄 때까지 대기합니다.
        // 하지만 "피니쉬/일반 엘리베이터"는 태어나자마자 스스로 준비해야 트리거가 작동합니다.
        if (currentType != ElevatorType.RestArea)
        {
            Initialize();
        }
    }
    void Update()
    {
        // 만약 시야가 잠겨야 하는 상황(isViewLocked)이라면?
        if (isViewLocked && mainCam != null)
        {
            // 다른 누군가(GameManager 등)가 맵을 보이게 바꿔도, 다시 즉시 숨겨버립니다.
            mainCam.cullingMask &= ~hideLayerMask;
        }
    }
    public void Initialize()
    {
        InitializeRoutine();
    }

    private void InitializeRoutine()
    {
        // 공통 필수 초기화 (카메라 찾기 등)
        mainCam = Camera.main;
        FindComponents();

        // ---------------------------------------------------------
        // A. 레스트룸 엘리베이터 전용 로직 (시야 가리기 포함)
        // ---------------------------------------------------------
        if (currentType == ElevatorType.RestArea)
        {
            // 튜토리얼(-9)이 아닐 때만 실행
            if (GameManager.Instance.currentFloor != -9)
            {
                CloseDoorsImmediate();
                LockDoor();

                if (fadeCanvasGroup != null)
                {
                    // "어? 화면이 어둡네? (피니쉬 엘베 타고 왔구나)" -> 밝혀주자!
                    if (fadeCanvasGroup.alpha > 0.1f)
                    {
                        StartCoroutine(FadeIn());
                    }
                    // 화면이 이미 밝다면(튜토리얼에서 옴)? -> 아무것도 안 함 (자연스럽게 연결)
                }

                // [시야 차단 로직] 레스트룸일 때만 작동!
                // 혹시 GameManager가 놓쳤을 경우를 대비한 이중 안전장치
                if (mainCam != null)
                {
                    isViewLocked = true;
                    mainCam.cullingMask &= ~hideLayerMask;
                }

                StartCoroutine(RestAreaAutoOpenSequence());
            }
            else
            {
                CloseDoorsImmediate();
                // 튜토리얼이면 맵 다 보여주기
                if (mainCam != null) mainCam.cullingMask = -1;
            }
        }
        // ---------------------------------------------------------
        // B. 피니쉬 엘리베이터 전용 로직 (시야 안 건드림)
        // ---------------------------------------------------------
        else if (currentType == ElevatorType.Finish)
        {
            FindDestination("RestAreaSpawnPoint");
            CloseDoorsImmediate();
            LockDoor();

            // 피니쉬 엘리베이터는 무조건 맵이 다 보여야 합니다.
            if (mainCam != null) mainCam.cullingMask = -1;
        }

        // [중요] 주석 해제! 이걸 해야 플레이어가 탔는지 감시를 시작합니다.
        SetupTriggers();
    }

    //void Start()
    //{
    //    CalculateDoorPositions();
    //    FindComponents();

    //    mainCam = Camera.main;
    //    if (mainCam != null) originalCullingMask = mainCam.cullingMask;

    //    // [핵심 수정] 튜토리얼(-9층)이 아닐 때만 "엘리베이터 납치/대기" 로직을 실행합니다.
    //    // 튜토리얼에서는 플레이어가 방 안의 PlayerSpawnPoint에서 걸어서 시작해야 하니까요.
    //    if (currentType == ElevatorType.RestArea)
    //    {
    //        if (GameManager.Instance != null && GameManager.Instance.currentFloor == -9)
    //        {
    //            // [튜토리얼 층인 경우]
    //            // 아무것도 안 함. (플레이어 납치 X, 시야 차단 X)
    //            // 그냥 맵에 배치된 상태 그대로 대기. (나중에 플레이어가 걸어서 타거나 하겠죠)
    //            Debug.Log("[Elevator] 튜토리얼 층입니다. 레스트룸 시퀀스를 건너뜁니다.");
    //        }
    //        else
    //        {
    //            // [본 게임(-8층 이상)인 경우]
    //            // 1. 위치를 StartPoint로 강제 이동 (플레이어 포함)
    //            MoveElevatorToStartPoint();

    //            // 2. 바깥 세상(맵) 안 보이게 가리기
    //            if (mainCam != null)
    //            {
    //                mainCam.cullingMask = originalCullingMask & ~hideLayerMask;
    //            }

    //            // 3. 문 닫고 잠금
    //            CloseDoorsImmediate();
    //            LockDoor();
    //            isRestTimerStarted = false;
    //        }
    //    }
    //    else if (currentType == ElevatorType.Finish)
    //    {
    //        FindDestination("RestAreaSpawnPoint");
    //        CloseDoorsImmediate();
    //        LockDoor();
    //    }

    //    SetupTriggers();
    //}

    // ====================================================
    // 휴식방 10초 대기 시퀀스
    // ====================================================
    IEnumerator RestAreaAutoOpenSequence()
    {
        isProcessing = true;
        UpdateLightColor(true);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(SoundManager.Instance.elevatorAmbience);
        }

        // 10초 대기
        yield return new WaitForSeconds(restAreaWaitTime);

        //if (SoundManager.Instance != null) SoundManager.Instance.StopBGM();

        isViewLocked = false;
        // [중요] 문 열기 직전에 바깥 세상 복구!
        if (mainCam != null)
        {
            mainCam.cullingMask = -1;
        }

        Debug.Log("[RestArea] 대기 완료! 문을 엽니다.");

        if (SoundManager.Instance != null)
        {
            // mainBgm 대신 원하는 다른 배경음이 있다면 그걸로 바꾸셔도 됩니다.
            SoundManager.Instance.PlayBGM(SoundManager.Instance.mainBgm);
        }
        UnlockDoor();
        isProcessing = false;
    }

    void MoveElevatorToStartPoint()
    {
        if (GameManager.Instance == null) return;
        Transform targetSpot = GameManager.Instance.GetStartRoomSpawnPoint();

        if (targetSpot != null)
        {
            transform.position = targetSpot.position;
            transform.rotation = targetSpot.rotation;

            // 플레이어도 같이 있다면 위치 보정 (혹시 모르니)
            if (playerTransform != null)
            {
                // 플레이어를 엘리베이터 내부 중심으로 이동 (살짝 위로)
                playerTransform.position = transform.position + Vector3.up * 0.1f;
                playerTransform.rotation = transform.rotation;
                Physics.SyncTransforms(); // 물리 위치 즉시 갱신
            }
        }
    }

    // ====================================================
    // 트리거 설정 (순간이동 감지 강화)
    // ====================================================
    void SetupTriggers()
    {
        // 1. 외부 문 열기 버튼/영역
        if (doorTriggerObject)
        {
            var dt = GetOrAddTrigger(doorTriggerObject);
            dt.onPlayerEnter = () => {
                // 레스트룸이거나 이미 처리중이면 무시
                if (currentType != ElevatorType.RestArea && !isProcessing && !doorsOpen && !isLocked)
                    StartCoroutine(OpenDoors());
            };
            dt.onPlayerExit = () => {
                if (!isProcessing && doorsOpen && !isPlayerInside) StartCoroutine(CloseDoors());
            };
        }

        // 2. 내부 탑승 감지
        if (insideTriggerObject)
        {
            var it = GetOrAddTrigger(insideTriggerObject);

            System.Action onPlayerDetected = () =>
            {
                isPlayerInside = true;

                // (A) 피니쉬/일반 엘리베이터: 타면 다음 층으로 이동 출발
                if (!isProcessing && doorsOpen && currentType != ElevatorType.RestArea)
                {
                    StartCoroutine(DepartSequence());
                }

                // (B) 레스트룸: 플레이어가 안에 있어도 아무것도 안 함 
                // (이미 Start에서 AutoSequence를 돌렸기 때문)
            };

            it.onPlayerEnter = onPlayerDetected;
            it.onPlayerStay = onPlayerDetected;

            it.onPlayerExit = () => {
                isPlayerInside = false;

                // 레스트룸에서 밖으로 나가면? -> 문 닫기
                if (!isProcessing && currentType == ElevatorType.RestArea && doorsOpen)
                {
                    StartCoroutine(CloseDoors());
                }
            };
        }
    }



    // ... (기본 함수들: CloseDoorsImmediate, LockDoor, UnlockDoor 등등 기존 유지) ...

    private void CloseDoorsImmediate()
    {
        doorsOpen = false;
        if (leftDoor) leftDoor.localPosition = leftDoorClosedPos;
        if (rightDoor) rightDoor.localPosition = rightDoorClosedPos;
    }

    public void LockDoor() { isLocked = true; UpdateLightColor(true); if (doorsOpen) StartCoroutine(CloseDoors()); }
    public void UnlockDoor() { isLocked = false; UpdateLightColor(false); StartCoroutine(OpenDoors()); }
    private void UpdateLightColor(bool locked) { if (statusLight != null) statusLight.color = locked ? lockedColor : unlockedColor; }

    IEnumerator DepartSequence()
    {
        isProcessing = true;
        yield return StartCoroutine(CloseDoors());
        yield return StartCoroutine(FadeOut());

        if (currentDestination) TeleportPlayer(currentDestination);

        if (currentType == ElevatorType.Finish)
        {
            // 다음 맵 생성 요청
            if (GameManager.Instance) GameManager.Instance.LoadNextLevel();
        }
        else
        {
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(FadeIn());
            yield return StartCoroutine(OpenDoors());
            isProcessing = false;
        }
    }

    // ====================================================
    // [중요] 레스트룸 나가는 시퀀스 수정
    // ====================================================
    IEnumerator ExitRestAreaSequence()
    {
        isProcessing = true;

        FindNewStartPoint();
        if (currentDestination) TeleportPlayer(currentDestination);

        StartCoroutine(CloseDoors()); // 문 닫고

        // [핵심 수정] 다음 층에서 다시 왔을 때 타이머가 돌도록 변수 초기화!
        isRestTimerStarted = false;
        LockDoor(); // 다시 잠금 상태(빨간불)로 변경

        isProcessing = false;
        doorsOpen = false;
        yield return null;
    }

    // 유틸리티
    void CalculateDoorPositions() { if (leftDoor) { leftDoorClosedPos = leftDoor.localPosition; leftDoorOpenPos = leftDoorClosedPos + new Vector3(0, 0, -0.66f); } if (rightDoor) { rightDoorClosedPos = rightDoor.localPosition; rightDoorOpenPos = rightDoorClosedPos + new Vector3(0, 0, 0.66f); } }
    void FindComponents() { if (!playerTransform) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p) playerTransform = p.transform; } if (!fadeCanvasGroup) { GameObject c = GameObject.Find("FadeCanvas"); if (c) fadeCanvasGroup = c.GetComponent<CanvasGroup>(); if (!fadeCanvasGroup) fadeCanvasGroup = GetComponentInChildren<CanvasGroup>(); } if (!statusLight) statusLight = GetComponentInChildren<Light>(); }
    void FindDestination(string name) { GameObject go = GameObject.Find(name); if (go) currentDestination = go.transform; }
    void FindNewStartPoint() { if (GameManager.Instance) { Transform sp = GameManager.Instance.GetStartRoomSpawnPoint(); if (sp) currentDestination = sp; } if (!currentDestination) FindDestination("StartPoint"); }

    // [수정됨] Stay 지원 트리거 가져오기
    ElevatorTrigger GetOrAddTrigger(GameObject obj) { var t = obj.GetComponent<ElevatorTrigger>(); if (!t) t = obj.AddComponent<ElevatorTrigger>(); return t; }

    void TeleportPlayer(Transform target) { if (!playerTransform) return; CharacterController cc = playerTransform.GetComponent<CharacterController>(); if (cc) cc.enabled = false; playerTransform.position = target.position; playerTransform.rotation = target.rotation; if (cc) cc.enabled = true; }
    IEnumerator MoveDoors(bool open) { float t = 0; Vector3 lStart = leftDoor ? leftDoor.localPosition : Vector3.zero; Vector3 rStart = rightDoor ? rightDoor.localPosition : Vector3.zero; Vector3 lEnd = open ? leftDoorOpenPos : leftDoorClosedPos; Vector3 rEnd = open ? rightDoorOpenPos : rightDoorClosedPos; while (t < 1) { t += Time.deltaTime * doorSpeed; if (leftDoor) leftDoor.localPosition = Vector3.Lerp(lStart, lEnd, t); if (rightDoor) rightDoor.localPosition = Vector3.Lerp(rStart, rEnd, t); yield return null; } doorsOpen = open; }
    IEnumerator OpenDoors() 
    {
        SoundManager.Instance.PlaySFX(SoundManager.Instance.elevatorDing);
        return MoveDoors(true); 
    }
    IEnumerator CloseDoors() { return MoveDoors(false); }
    IEnumerator FadeOut() { if (!fadeCanvasGroup) yield break; fadeCanvasGroup.blocksRaycasts = true; float t = fadeCanvasGroup.alpha; while (t < 1) { t += Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 1; }
    IEnumerator FadeIn() { if (!fadeCanvasGroup) yield break; float t = fadeCanvasGroup.alpha; while (t > 0) { t -= Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 0; fadeCanvasGroup.blocksRaycasts = false; }
    public void SetType(ElevatorType type) => currentType = type;
}