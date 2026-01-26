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
    }

    void Start()
    {
        CalculateDoorPositions();
        FindComponents();
        SetupTriggers();

        if (currentType == ElevatorType.Finish) FindDestination("RestAreaSpawnPoint");

        // 초기 상태 설정
        if (currentType == ElevatorType.Finish)
        {
            CloseDoorsImmediate();
            LockDoor();
        }
        else if (currentType == ElevatorType.RestArea)
        {
            CloseDoorsImmediate();
            LockDoor();
            isRestTimerStarted = false;
        }
    }

    // ====================================================
    // 휴식방 10초 대기 시퀀스
    // ====================================================
    IEnumerator RestAreaAutoOpenSequence()
    {
        isRestTimerStarted = true;
        isProcessing = true;

        UpdateLightColor(true);

        yield return new WaitForSeconds(0.5f);
        if (fadeCanvasGroup) StartCoroutine(FadeIn());

        Debug.Log($"[RestArea] 플레이어 확인됨. 맵 생성 대기 중... ({restAreaWaitTime}초)");

        yield return new WaitForSeconds(restAreaWaitTime);

        Debug.Log("[RestArea] 대기 끝! 문을 엽니다.");
        UnlockDoor();
        isProcessing = false;
    }

    // ====================================================
    // 트리거 설정 (순간이동 감지 강화)
    // ====================================================
    void SetupTriggers()
    {
        // 1. 외부 문 열기
        if (doorTriggerObject)
        {
            var dt = GetOrAddTrigger(doorTriggerObject);
            dt.onPlayerEnter = () => {
                if (currentType != ElevatorType.RestArea && !isProcessing && !doorsOpen && !isLocked)
                    StartCoroutine(OpenDoors());
            };
            dt.onPlayerExit = () => {
                if (!isProcessing && doorsOpen && !isPlayerInside) StartCoroutine(CloseDoors());
            };
        }

        // 2. 내부 탑승 (Enter + Stay 모두 사용)
        if (insideTriggerObject)
        {
            var it = GetOrAddTrigger(insideTriggerObject);

            // 공통 감지 로직 함수
            System.Action onPlayerDetected = () =>
            {
                isPlayerInside = true;

                // (A) 일반/피니쉬: 타면 출발
                if (!isProcessing && doorsOpen && currentType != ElevatorType.RestArea)
                {
                    StartCoroutine(DepartSequence());
                }

                // (B) 레스트룸: 플레이어 감지 시 타이머 시작
                // !isRestTimerStarted 체크 덕분에 계속 머물러도 한 번만 실행됨
                if (currentType == ElevatorType.RestArea && !isRestTimerStarted)
                {
                    StartCoroutine(RestAreaAutoOpenSequence());
                }
            };

            // Enter와 Stay 둘 다 연결 (순간이동 시 Enter가 안 먹힐 때 Stay가 잡아줌)
            it.onPlayerEnter = onPlayerDetected;
            it.onPlayerStay = onPlayerDetected;

            it.onPlayerExit = () => {
                isPlayerInside = false;
                if (!isProcessing && currentType == ElevatorType.RestArea && doorsOpen)
                {
                    StartCoroutine(ExitRestAreaSequence());
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
    IEnumerator OpenDoors() { return MoveDoors(true); }
    IEnumerator CloseDoors() { return MoveDoors(false); }
    IEnumerator FadeOut() { if (!fadeCanvasGroup) yield break; fadeCanvasGroup.blocksRaycasts = true; float t = fadeCanvasGroup.alpha; while (t < 1) { t += Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 1; }
    IEnumerator FadeIn() { if (!fadeCanvasGroup) yield break; float t = fadeCanvasGroup.alpha; while (t > 0) { t -= Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 0; fadeCanvasGroup.blocksRaycasts = false; }
    public void SetType(ElevatorType type) => currentType = type;
}