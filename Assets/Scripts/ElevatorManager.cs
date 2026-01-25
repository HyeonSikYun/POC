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

    [Header("설정")]
    [SerializeField] private float restAreaWaitTime = 10f;
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private float fadeSpeed = 2f;

    [Header("트리거")]
    [SerializeField] private GameObject doorTriggerObject;
    [SerializeField] private GameObject insideTriggerObject;

    // 내부 변수
    private Vector3 leftDoorClosedPos, leftDoorOpenPos;
    private Vector3 rightDoorClosedPos, rightDoorOpenPos;
    private bool doorsOpen = false;
    private bool isProcessing = false;
    private bool isPlayerInside = false;
    private Transform currentDestination;

    // 잠금 상태 (기본값 false)
    private bool isLocked = false;

    public static ElevatorManager RestAreaInstance;

    void Awake()
    {
        if (currentType == ElevatorType.RestArea) RestAreaInstance = this;
    }

    void Start()
    {
        InitializeDoors();
        FindComponents();
        SetupTriggers();

        // 목적지 초기화
        if (currentType == ElevatorType.Normal) FindDestination("StartPoint");
        else if (currentType == ElevatorType.Finish) FindDestination("RestAreaSpawnPoint");

        // [핵심 수정] 엘리베이터 타입에 따른 초기 문 상태 설정
        if (currentType == ElevatorType.Normal)
        {
            // 일반 엘리베이터(시작방)는 처음부터 열려있음
            StartCoroutine(OpenDoorsImmediate());
        }
        else if (currentType == ElevatorType.Finish)
        {
            // 피니쉬 엘리베이터는 닫혀있고 + 잠긴 상태로 시작
            LockDoor();
        }
        // RestArea는 닫혀있는 상태로 시작 (잠금은 아님, 트리거로 열림)
    }

    // ====================================================
    // 외부 제어 함수 (GameManager에서 호출)
    // ====================================================
    public void LockDoor()
    {
        isLocked = true;
        // 문이 열려있다면 즉시 닫음
        if (doorsOpen) StartCoroutine(CloseDoors());
    }

    public void UnlockDoor()
    {
        Debug.Log("엘리베이터 잠금 해제! 문을 엽니다.");
        isLocked = false;
        // 잠금 해제되면 자동으로 문 열기 (플레이어가 타야 하니까)
        StartCoroutine(OpenDoors());
    }

    // ====================================================
    // 1. 출발 시퀀스 (Finish -> RestArea)
    // ====================================================
    IEnumerator DepartSequence()
    {
        isProcessing = true;
        Debug.Log("1. [Finish] 문 닫기 시작");

        yield return StartCoroutine(CloseDoors());

        Debug.Log("2. [Finish] 페이드 아웃 시작");
        yield return StartCoroutine(FadeOut());

        if (currentDestination)
        {
            TeleportPlayer(currentDestination);
            Debug.Log($"3. [Finish] 이동 완료: {currentDestination.name}");
        }
        else
        {
            Debug.LogError("!!! 목적지(RestAreaSpawnPoint)가 없습니다!");
        }

        if (currentType == ElevatorType.Finish)
        {
            if (RestAreaInstance)
            {
                RestAreaInstance.StartCoroutine(RestAreaInstance.RestAreaArrivalSequence());
            }
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
    // 2. RestArea 도착 처리
    // ====================================================
    public IEnumerator RestAreaArrivalSequence()
    {
        isProcessing = true;
        yield return new WaitForSeconds(1.0f);

        Debug.Log("4. [RestArea] 페이드 인 시작");
        yield return StartCoroutine(FadeIn());

        Debug.Log("5. [RestArea] 맵 재생성 및 대기");
        if (GameManager.Instance) GameManager.Instance.RegenerateMap();

        yield return new WaitForSeconds(restAreaWaitTime);

        FindNewStartPoint();

        Debug.Log("6. [RestArea] 문 열림");
        yield return StartCoroutine(OpenDoors());

        isProcessing = false;
    }

    // ====================================================
    // 3. RestArea 퇴장
    // ====================================================
    IEnumerator ExitRestAreaSequence()
    {
        isProcessing = true;
        Debug.Log("7. [RestArea] 퇴장 -> 즉시 이동");

        if (!currentDestination) FindNewStartPoint();

        if (currentDestination)
        {
            TeleportPlayer(currentDestination);
        }

        StartCoroutine(CloseDoors());

        isProcessing = false;
        doorsOpen = false;

        yield return null;
    }

    // ====================================================
    // 유틸리티
    // ====================================================
    void InitializeDoors()
    {
        if (leftDoor)
        {
            leftDoorClosedPos = leftDoor.localPosition;
            leftDoorOpenPos = leftDoorClosedPos + new Vector3(0, 0, -0.66f);
        }
        if (rightDoor)
        {
            rightDoorClosedPos = rightDoor.localPosition;
            rightDoorOpenPos = rightDoorClosedPos + new Vector3(0, 0, 0.66f);
        }
    }

    void FindComponents()
    {
        if (!playerTransform)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerTransform = p.transform;
        }
        if (!fadeCanvasGroup)
        {
            GameObject canvasObj = GameObject.Find("FadeCanvas");
            if (canvasObj) fadeCanvasGroup = canvasObj.GetComponent<CanvasGroup>();
            if (!fadeCanvasGroup) fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        }
    }

    void FindDestination(string name)
    {
        GameObject go = GameObject.Find(name);
        if (go) currentDestination = go.transform;
    }

    void FindNewStartPoint()
    {
        if (GameManager.Instance)
        {
            Transform sp = GameManager.Instance.GetStartRoomSpawnPoint();
            if (sp) currentDestination = sp;
        }
        if (!currentDestination) FindDestination("StartPoint");
    }

    void SetupTriggers()
    {
        if (currentType != ElevatorType.RestArea && doorTriggerObject)
        {
            var dt = GetOrAddTrigger(doorTriggerObject);
            // [수정] 잠겨있으면(isLocked) 트리거에 닿아도 문이 안 열림
            dt.onPlayerEnter = () => {
                if (!isProcessing && !doorsOpen && !isLocked)
                    StartCoroutine(OpenDoors());
            };
            dt.onPlayerExit = () => {
                if (!isProcessing && doorsOpen && !isPlayerInside)
                    StartCoroutine(CloseDoors());
            };
        }

        if (insideTriggerObject)
        {
            var it = GetOrAddTrigger(insideTriggerObject);
            it.onPlayerEnter = () =>
            {
                isPlayerInside = true;
                if (!isProcessing && doorsOpen && currentType != ElevatorType.RestArea)
                    StartCoroutine(DepartSequence());
            };

            it.onPlayerExit = () =>
            {
                isPlayerInside = false;
                if (!isProcessing && currentType == ElevatorType.RestArea)
                    StartCoroutine(ExitRestAreaSequence());
            };
        }
    }

    ElevatorTrigger GetOrAddTrigger(GameObject obj)
    {
        var t = obj.GetComponent<ElevatorTrigger>();
        if (!t) t = obj.AddComponent<ElevatorTrigger>();
        return t;
    }

    void TeleportPlayer(Transform target)
    {
        if (!playerTransform) return;
        CharacterController cc = playerTransform.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;
        playerTransform.position = target.position;
        playerTransform.rotation = target.rotation;
        if (cc) cc.enabled = true;
    }

    IEnumerator MoveDoors(bool open)
    {
        float t = 0;
        Vector3 lStart = leftDoor ? leftDoor.localPosition : Vector3.zero;
        Vector3 rStart = rightDoor ? rightDoor.localPosition : Vector3.zero;
        Vector3 lEnd = open ? leftDoorOpenPos : leftDoorClosedPos;
        Vector3 rEnd = open ? rightDoorOpenPos : rightDoorClosedPos;

        while (t < 1)
        {
            t += Time.deltaTime * doorSpeed;
            if (leftDoor) leftDoor.localPosition = Vector3.Lerp(lStart, lEnd, t);
            if (rightDoor) rightDoor.localPosition = Vector3.Lerp(rStart, rEnd, t);
            yield return null;
        }
        doorsOpen = open;
    }
    IEnumerator OpenDoors() { return MoveDoors(true); }
    IEnumerator CloseDoors() { return MoveDoors(false); }
    IEnumerator OpenDoorsImmediate()
    {
        if (leftDoor) leftDoor.localPosition = leftDoorOpenPos;
        if (rightDoor) rightDoor.localPosition = rightDoorOpenPos;
        doorsOpen = true;
        yield return null;
    }

    IEnumerator FadeOut()
    {
        if (!fadeCanvasGroup) yield break;
        fadeCanvasGroup.blocksRaycasts = true;
        float t = fadeCanvasGroup.alpha;
        while (t < 1)
        {
            t += Time.deltaTime * fadeSpeed;
            fadeCanvasGroup.alpha = t;
            yield return null;
        }
        fadeCanvasGroup.alpha = 1;
    }

    IEnumerator FadeIn()
    {
        if (!fadeCanvasGroup) yield break;
        float t = fadeCanvasGroup.alpha;
        while (t > 0)
        {
            t -= Time.deltaTime * fadeSpeed;
            fadeCanvasGroup.alpha = t;
            yield return null;
        }
        fadeCanvasGroup.alpha = 0;
        fadeCanvasGroup.blocksRaycasts = false;
    }

    public void SetType(ElevatorType type) => currentType = type;
}