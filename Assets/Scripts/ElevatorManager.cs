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
    [SerializeField] private float fadeSpeed = 2f; // 페이드 속도

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

        // RestArea만 닫혀있고, 나머지는 열린 상태로 시작
        if (currentType != ElevatorType.RestArea) StartCoroutine(OpenDoorsImmediate());
    }

    // ====================================================
    // 1. 출발 시퀀스 (Finish -> RestArea)
    // ====================================================
    IEnumerator DepartSequence()
    {
        isProcessing = true;
        Debug.Log("1. [Finish] 문 닫기 시작");

        // 1. 문 닫기 (완료될 때까지 대기)
        yield return StartCoroutine(CloseDoors());

        // 2. 페이드 아웃 (완료될 때까지 대기 - 화면 암전)
        Debug.Log("2. [Finish] 페이드 아웃 시작");
        yield return StartCoroutine(FadeOut());

        // 3. 이동 (화면이 깜깜해진 상태에서 이동)
        if (currentDestination)
        {
            TeleportPlayer(currentDestination);
            Debug.Log($"3. [Finish] 이동 완료: {currentDestination.name}");
        }
        else
        {
            Debug.LogError("!!! 목적지(RestAreaSpawnPoint)가 없습니다!");
        }

        // 4. RestArea 로직으로 연결 (Finish 엘리베이터의 역할은 끝)
        if (currentType == ElevatorType.Finish)
        {
            // Finish 엘리베이터는 곧 파괴되므로, RestArea 엘리베이터에게 "도착 처리해줘"라고 넘김
            if (RestAreaInstance)
            {
                RestAreaInstance.StartCoroutine(RestAreaInstance.RestAreaArrivalSequence());
            }
        }
        else // Normal 타입이면 그냥 도착 처리
        {
            yield return new WaitForSeconds(0.5f);
            yield return StartCoroutine(FadeIn());
            yield return StartCoroutine(OpenDoors());
            isProcessing = false;
        }
    }

    // ====================================================
    // 2. RestArea 도착 처리 (RestAreaInstance에서 실행됨)
    // ====================================================
    // [RestArea] 플레이어가 도착했을 때 호출됨 (Finish 엘리베이터에 의해 호출됨)
    public IEnumerator RestAreaArrivalSequence()
    {
        isProcessing = true;

        // ================================================================
        // [수정됨] 이동 완료 후 잠시 암전 상태 유지 (1초 대기)
        // 이 시간이 지나야 페이드 인이 시작됩니다.
        // ================================================================
        yield return new WaitForSeconds(1.0f);

        // 1. 페이드 인 (이제 화면이 밝아짐)
        Debug.Log("4. [RestArea] 페이드 인 시작");
        yield return StartCoroutine(FadeIn());

        // 2. 맵 재생성
        Debug.Log("5. [RestArea] 맵 재생성 및 대기");
        if (GameManager.Instance) GameManager.Instance.RegenerateMap();

        // 3. 대기 (RestArea에서 머무는 시간)
        // 위에서 1초를 썼으므로, 총 대기 시간을 맞추고 싶다면 (restAreaWaitTime - 1.0f)를 해도 됨
        yield return new WaitForSeconds(restAreaWaitTime);

        // 4. 새로운 맵의 StartPoint 찾기
        FindNewStartPoint();

        // 5. 문 열기 (나가세요)
        Debug.Log("6. [RestArea] 문 열림");
        yield return StartCoroutine(OpenDoors());

        isProcessing = false; // 플레이어 조작 가능
    }

    // ====================================================
    // 3. RestArea 퇴장 (즉시 이동 - 페이드 없음)
    // ====================================================
    IEnumerator ExitRestAreaSequence()
    {
        isProcessing = true;
        Debug.Log("7. [RestArea] 퇴장 -> 즉시 이동");

        // [요청사항] 페이드 아웃/인 없이 즉시 이동

        // 1. 목적지 확인 (없으면 재검색)
        if (!currentDestination) FindNewStartPoint();

        // 2. 즉시 텔레포트
        if (currentDestination)
        {
            TeleportPlayer(currentDestination);
        }

        // 3. 문은 뒤에서 알아서 닫히게 함 (기다리지 않음)
        StartCoroutine(CloseDoors());

        // 4. 상태 초기화
        isProcessing = false;
        doorsOpen = false;

        yield return null;
    }

    // ====================================================
    // 유틸리티 및 설정
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
        // 못 찾았으면 이름으로 검색
        if (!currentDestination) FindDestination("StartPoint");
    }

    void SetupTriggers()
    {
        if (currentType != ElevatorType.RestArea && doorTriggerObject)
        {
            var dt = GetOrAddTrigger(doorTriggerObject);
            dt.onPlayerEnter = () => { if (!isProcessing && !doorsOpen) StartCoroutine(OpenDoors()); };
            dt.onPlayerExit = () => { if (!isProcessing && doorsOpen && !isPlayerInside) StartCoroutine(CloseDoors()); };
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
                // RestArea에서 나가는 순간 즉시 발동
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
        fadeCanvasGroup.alpha = 1; // 확실하게 1로 고정
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
        fadeCanvasGroup.alpha = 0; // 확실하게 0으로 고정
        fadeCanvasGroup.blocksRaycasts = false;
    }

    public void SetType(ElevatorType type) => currentType = type;
}