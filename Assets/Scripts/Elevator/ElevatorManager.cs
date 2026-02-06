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

    [Header("흔들림 효과 (이동 연출)")]
    [SerializeField] private float shakeIntensity = 0.05f; // 흔들림 강도
    // [추가됨] 흔들리는 시간 (예: 0.5초 ~ 1.5초 동안 흔들림)
    [SerializeField] private float shakeDurationMin = 0.5f;
    [SerializeField] private float shakeDurationMax = 1.5f;
    // [추가됨] 멈춰있는 시간 (예: 1.0초 ~ 3.0초 동안 조용함)
    [SerializeField] private float idleDurationMin = 1.0f;
    [SerializeField] private float idleDurationMax = 3.0f;

    [Header("트리거")]
    [SerializeField] private GameObject doorTriggerObject;
    [SerializeField] private GameObject insideTriggerObject;

    [Header("상승 연출")]
    [SerializeField] private ParticleSystem speedLineEffect;

    [Header("심리스 연출 설정")]
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

    private bool isRestTimerStarted = false;

    public static ElevatorManager RestAreaInstance;

    void Awake()
    {
        if (currentType == ElevatorType.RestArea) RestAreaInstance = this;
        CalculateDoorPositions();
        FindComponents();
    }

    void Start()
    {
        mainCam = Camera.main;
        if (mainCam != null)
        {
            originalCullingMask = mainCam.cullingMask;
        }

        if (currentType != ElevatorType.RestArea)
        {
            Initialize();
        }
    }

    void Update()
    {
        if (isViewLocked && mainCam != null)
        {
            mainCam.cullingMask &= ~hideLayerMask;
        }
    }

    public void Initialize()
    {
        InitializeRoutine();
    }

    private void InitializeRoutine()
    {
        mainCam = Camera.main;
        FindComponents();

        if (currentType == ElevatorType.RestArea)
        {
            bool shouldHideMap = (GameManager.Instance.currentFloor != -9) || GameManager.Instance.isRetry;

            if (shouldHideMap)
            {
                CloseDoorsImmediate();
                LockDoor();

                if (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.1f)
                {
                    StartCoroutine(FadeIn());
                }

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
                if (mainCam != null)
                {
                    isViewLocked = false;
                    mainCam.cullingMask = -1;
                }
            }
        }
        else if (currentType == ElevatorType.Finish)
        {
            FindDestination("RestAreaSpawnPoint");
            CloseDoorsImmediate();
            LockDoor();

            if (mainCam != null)
            {
                isViewLocked = false;
                mainCam.cullingMask = -1;
            }
        }

        SetupTriggers();
    }

    // ====================================================
    // [수정됨] 간헐적 흔들림 (덜커덩... 조용... 덜커덩)
    // ====================================================
    IEnumerator RestAreaAutoOpenSequence()
    {
        isProcessing = true;
        UpdateLightColor(true);

        if (speedLineEffect != null) speedLineEffect.Play();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(SoundManager.Instance.elevatorAmbience);
        }

        Vector3 originalPosition = transform.position;
        float totalTimer = 0f;

        // 상태 변수: 지금 흔들리는 중인가?
        bool isShaking = false;
        // 다음 상태로 바뀔 때까지 남은 시간
        float nextStateTimer = 0f;

        // 전체 대기 시간(restAreaWaitTime) 동안 반복
        while (totalTimer < restAreaWaitTime)
        {
            float dt = Time.deltaTime;
            totalTimer += dt;
            nextStateTimer -= dt;

            // 1. 상태 전환 타이밍이 되었나요?
            if (nextStateTimer <= 0f)
            {
                // 상태 뒤집기 (흔들림 <-> 멈춤)
                isShaking = !isShaking;

                if (isShaking)
                {
                    // 흔들림 시작! (지속 시간 랜덤 설정)
                    nextStateTimer = Random.Range(shakeDurationMin, shakeDurationMax);

                    // (선택 사항) 덜커덩 소리를 여기서 재생하면 더 리얼합니다.
                    // SoundManager.Instance.PlaySFX(rattleSound); 
                }
                else
                {
                    // 멈춤 시작! (지속 시간 랜덤 설정)
                    nextStateTimer = Random.Range(idleDurationMin, idleDurationMax);
                    // 멈출 때는 위치를 즉시 원상복구
                    transform.position = originalPosition;
                }
            }

            // 2. 현재 상태에 따른 행동
            if (isShaking)
            {
                // 덜덜 떨기
                transform.position = originalPosition + (Random.insideUnitSphere * shakeIntensity);
            }
            else
            {
                // 가만히 있기 (혹시 모르니 위치 고정)
                transform.position = originalPosition;
            }

            yield return null;
        }
        if (speedLineEffect != null) speedLineEffect.Stop();
        // 끝났으면 위치 완벽 복구
        transform.position = originalPosition;

        // 맵 보여주기
        isViewLocked = false;
        if (mainCam != null)
        {
            mainCam.cullingMask = -1;
        }

        Debug.Log("[RestArea] 이동 완료! 문을 엽니다.");

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.PlayBGM(SoundManager.Instance.mainBgm);
        }

        UnlockDoor();
        isProcessing = false;
    }

    // ====================================================
    // 나머지 함수들 (유지)
    // ====================================================
    void SetupTriggers()
    {
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

        if (insideTriggerObject)
        {
            var it = GetOrAddTrigger(insideTriggerObject);
            System.Action onPlayerDetected = () =>
            {
                isPlayerInside = true;
                if (!isProcessing && doorsOpen && currentType != ElevatorType.RestArea)
                {
                    StartCoroutine(DepartSequence());
                }
            };
            it.onPlayerEnter = onPlayerDetected;
            it.onPlayerStay = onPlayerDetected;
            it.onPlayerExit = () => {
                isPlayerInside = false;
                if (!isProcessing && currentType == ElevatorType.RestArea && doorsOpen)
                {
                    StartCoroutine(CloseDoors());
                }
            };
        }
    }

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
        SoundManager.Instance.StopBGM();
        if (currentDestination) TeleportPlayer(currentDestination);

        if (currentType == ElevatorType.Finish)
        {
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

    IEnumerator ExitRestAreaSequence()
    {
        isProcessing = true;
        FindNewStartPoint();
        if (currentDestination) TeleportPlayer(currentDestination);
        StartCoroutine(CloseDoors());
        isRestTimerStarted = false;
        LockDoor();
        isProcessing = false;
        doorsOpen = false;
        yield return null;
    }

    void CalculateDoorPositions() { if (leftDoor) { leftDoorClosedPos = leftDoor.localPosition; leftDoorOpenPos = leftDoorClosedPos + new Vector3(0, 0, -0.66f); } if (rightDoor) { rightDoorClosedPos = rightDoor.localPosition; rightDoorOpenPos = rightDoorClosedPos + new Vector3(0, 0, 0.66f); } }
    void FindComponents() { if (!playerTransform) { GameObject p = GameObject.FindGameObjectWithTag("Player"); if (p) playerTransform = p.transform; } if (!fadeCanvasGroup) { GameObject c = GameObject.Find("FadeCanvas"); if (c) fadeCanvasGroup = c.GetComponent<CanvasGroup>(); if (!fadeCanvasGroup) fadeCanvasGroup = GetComponentInChildren<CanvasGroup>(); } if (!statusLight) statusLight = GetComponentInChildren<Light>(); }
    void FindDestination(string name) { GameObject go = GameObject.Find(name); if (go) currentDestination = go.transform; }
    void FindNewStartPoint() { if (GameManager.Instance) { Transform sp = GameManager.Instance.GetStartRoomSpawnPoint(); if (sp) currentDestination = sp; } if (!currentDestination) FindDestination("StartPoint"); }
    ElevatorTrigger GetOrAddTrigger(GameObject obj) { var t = obj.GetComponent<ElevatorTrigger>(); if (!t) t = obj.AddComponent<ElevatorTrigger>(); return t; }
    void TeleportPlayer(Transform target) { if (!playerTransform) return; CharacterController cc = playerTransform.GetComponent<CharacterController>(); if (cc) cc.enabled = false; playerTransform.position = target.position; playerTransform.rotation = target.rotation; if (cc) cc.enabled = true; }
    IEnumerator MoveDoors(bool open) { float t = 0; Vector3 lStart = leftDoor ? leftDoor.localPosition : Vector3.zero; Vector3 rStart = rightDoor ? rightDoor.localPosition : Vector3.zero; Vector3 lEnd = open ? leftDoorOpenPos : leftDoorClosedPos; Vector3 rEnd = open ? rightDoorOpenPos : rightDoorClosedPos; while (t < 1) { t += Time.deltaTime * doorSpeed; if (leftDoor) leftDoor.localPosition = Vector3.Lerp(lStart, lEnd, t); if (rightDoor) rightDoor.localPosition = Vector3.Lerp(rStart, rEnd, t); yield return null; } doorsOpen = open; }
    IEnumerator OpenDoors() { SoundManager.Instance.PlaySFX(SoundManager.Instance.elevatorDing); return MoveDoors(true); }
    IEnumerator CloseDoors() { return MoveDoors(false); }
    IEnumerator FadeOut() { if (!fadeCanvasGroup) yield break; fadeCanvasGroup.blocksRaycasts = true; float t = fadeCanvasGroup.alpha; while (t < 1) { t += Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 1; }
    IEnumerator FadeIn() { if (!fadeCanvasGroup) yield break; float t = fadeCanvasGroup.alpha; while (t > 0) { t -= Time.deltaTime * fadeSpeed; fadeCanvasGroup.alpha = t; yield return null; } fadeCanvasGroup.alpha = 0; fadeCanvasGroup.blocksRaycasts = false; }
    public void SetType(ElevatorType type) => currentType = type;
}