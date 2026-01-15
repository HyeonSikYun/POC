using UnityEngine;
using System.Collections;

public class ElevatorManager : MonoBehaviour
{
    [Header("문 오브젝트")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;

    [Header("텔레포트 설정")]
    [SerializeField] private Transform teleportDestination;
    [SerializeField] private Transform playerTransform;

    [Header("페이드 설정")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("애니메이션 설정")]
    [SerializeField] private float doorSpeed = 2f;
    [SerializeField] private float fadeSpeed = 1f;

    [Header("트리거 오브젝트 (직접 할당)")]
    [SerializeField] private GameObject doorTriggerObject;
    [SerializeField] private GameObject insideTriggerObject;

    private Vector3 leftDoorClosedPos;
    private Vector3 rightDoorClosedPos;
    private Vector3 leftDoorOpenPos;
    private Vector3 rightDoorOpenPos;

    private bool isPlayerNearby = false;
    private bool isPlayerInside = false;
    private bool isProcessing = false;
    private bool doorsOpen = false;

    private ElevatorTrigger doorTrigger;
    private ElevatorTrigger insideTrigger;
    private Coroutine doorCoroutine;

    void Start()
    {
        // 초기 문 위치 저장
        if (leftDoor != null)
        {
            leftDoorClosedPos = leftDoor.localPosition;
            leftDoorOpenPos = leftDoorClosedPos + new Vector3(0, 0, -0.66f);
            Debug.Log("왼쪽 문 초기화 완료");
        }
        else
        {
            Debug.LogError("왼쪽 문이 할당되지 않았습니다!");
        }

        if (rightDoor != null)
        {
            rightDoorClosedPos = rightDoor.localPosition;
            rightDoorOpenPos = rightDoorClosedPos + new Vector3(0, 0, 0.66f);
            Debug.Log("오른쪽 문 초기화 완료");
        }
        else
        {
            Debug.LogError("오른쪽 문이 할당되지 않았습니다!");
        }

        // 페이드 캔버스 초기화
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0;
            fadeCanvasGroup.blocksRaycasts = false;
        }

        // Player 태그로 플레이어 찾기
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                Debug.Log("플레이어 자동 찾기 완료: " + player.name);
            }
            else
            {
                Debug.LogError("Player 태그를 가진 오브젝트를 찾을 수 없습니다!");
            }
        }

        // 트리거 설정
        SetupTriggers();
    }

    void SetupTriggers()
    {
        if (doorTriggerObject != null)
        {
            doorTrigger = doorTriggerObject.GetComponent<ElevatorTrigger>();
            if (doorTrigger == null)
            {
                doorTrigger = doorTriggerObject.AddComponent<ElevatorTrigger>();
            }
            doorTrigger.onPlayerEnter = OnDoorTriggerEnter;
            doorTrigger.onPlayerExit = OnDoorTriggerExit;
            Debug.Log("Door Trigger 설정 완료");
        }
        else
        {
            Debug.LogError("Door Trigger 오브젝트가 할당되지 않았습니다!");
        }

        if (insideTriggerObject != null)
        {
            insideTrigger = insideTriggerObject.GetComponent<ElevatorTrigger>();
            if (insideTrigger == null)
            {
                insideTrigger = insideTriggerObject.AddComponent<ElevatorTrigger>();
            }
            insideTrigger.onPlayerEnter = OnInsideTriggerEnter;
            insideTrigger.onPlayerExit = OnInsideTriggerExit;
            Debug.Log("Inside Trigger 설정 완료");
        }
        else
        {
            Debug.LogError("Inside Trigger 오브젝트가 할당되지 않았습니다!");
        }
    }

    void OnDoorTriggerEnter()
    {
        Debug.Log("플레이어가 문 앞에 도착");
        isPlayerNearby = true;

        if (!isProcessing && !doorsOpen)
        {
            if (doorCoroutine != null)
                StopCoroutine(doorCoroutine);
            doorCoroutine = StartCoroutine(OpenDoors());
        }
    }

    void OnDoorTriggerExit()
    {
        Debug.Log("플레이어가 문 앞에서 떠남");
        isPlayerNearby = false;

        // 플레이어가 안에 없고 처리 중이 아니면 문 닫기
        if (!isPlayerInside && !isProcessing && doorsOpen)
        {
            if (doorCoroutine != null)
                StopCoroutine(doorCoroutine);
            doorCoroutine = StartCoroutine(CloseDoors());
        }
    }

    void OnInsideTriggerEnter()
    {
        Debug.Log("플레이어가 엘리베이터 안으로 진입");
        isPlayerInside = true;
    }

    void OnInsideTriggerExit()
    {
        Debug.Log("플레이어가 엘리베이터 밖으로 나감");
        isPlayerInside = false;
    }

    void Update()
    {
        // 플레이어가 안에 들어오면 텔레포트 시작
        if (isPlayerInside && !isProcessing && doorsOpen)
        {
            Debug.Log("텔레포트 시퀀스 시작");
            StartCoroutine(TeleportSequence());
        }
    }

    IEnumerator OpenDoors()
    {
        Debug.Log("문 열기 시작");

        float t = 0;
        Vector3 leftStart = leftDoor != null ? leftDoor.localPosition : leftDoorClosedPos;
        Vector3 rightStart = rightDoor != null ? rightDoor.localPosition : rightDoorClosedPos;

        while (t < 1)
        {
            t += Time.deltaTime * doorSpeed;
            if (leftDoor != null)
                leftDoor.localPosition = Vector3.Lerp(leftStart, leftDoorOpenPos, t);
            if (rightDoor != null)
                rightDoor.localPosition = Vector3.Lerp(rightStart, rightDoorOpenPos, t);
            yield return null;
        }

        doorsOpen = true;
        Debug.Log("문 열림 완료");
    }

    IEnumerator CloseDoors()
    {
        Debug.Log("문 닫기 시작");

        float t = 0;
        Vector3 leftStart = leftDoor != null ? leftDoor.localPosition : leftDoorOpenPos;
        Vector3 rightStart = rightDoor != null ? rightDoor.localPosition : rightDoorOpenPos;

        while (t < 1)
        {
            t += Time.deltaTime * doorSpeed;
            if (leftDoor != null)
                leftDoor.localPosition = Vector3.Lerp(leftStart, leftDoorClosedPos, t);
            if (rightDoor != null)
                rightDoor.localPosition = Vector3.Lerp(rightStart, rightDoorClosedPos, t);
            yield return null;
        }

        doorsOpen = false;
        Debug.Log("문 닫힘 완료");
    }

    IEnumerator TeleportSequence()
    {
        isProcessing = true;

        // 1. 문 닫기
        if (doorCoroutine != null)
            StopCoroutine(doorCoroutine);
        yield return StartCoroutine(CloseDoors());

        // 2. 페이드 아웃 (화면 까매짐)
        yield return StartCoroutine(FadeOut());

        // 3. 플레이어 텔레포트
        if (playerTransform != null && teleportDestination != null)
        {
            Debug.Log("플레이어 텔레포트 실행");
            CharacterController cc = playerTransform.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
                playerTransform.position = teleportDestination.position;
                playerTransform.rotation = teleportDestination.rotation;
                cc.enabled = true;
            }
            else
            {
                playerTransform.position = teleportDestination.position;
                playerTransform.rotation = teleportDestination.rotation;
            }
            Debug.Log("텔레포트 완료");
        }
        else
        {
            Debug.LogError("플레이어 또는 목적지가 설정되지 않았습니다!");
        }

        // 4. 3초 대기
        Debug.Log("3초 대기 시작");
        yield return new WaitForSeconds(3f);
        Debug.Log("3초 대기 완료");

        // 5. 페이드 인 (화면 다시 밝아짐)
        yield return StartCoroutine(FadeIn());

        // 6. 상태 초기화
        isPlayerInside = false;
        isPlayerNearby = false;
        isProcessing = false;
        Debug.Log("텔레포트 시퀀스 완료");
    }

    IEnumerator FadeOut()
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("페이드 캔버스가 없어 대기 시간만 적용");
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        Debug.Log("페이드 아웃 시작 (화면 까매짐)");
        fadeCanvasGroup.blocksRaycasts = true;
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * fadeSpeed;
            fadeCanvasGroup.alpha = Mathf.Lerp(0, 1, t);
            yield return null;
        }
        fadeCanvasGroup.alpha = 1;
        Debug.Log("페이드 아웃 완료");
    }

    IEnumerator FadeIn()
    {
        if (fadeCanvasGroup == null)
        {
            Debug.LogWarning("페이드 캔버스가 없어 대기 시간만 적용");
            yield return new WaitForSeconds(0.5f);
            yield break;
        }

        Debug.Log("페이드 인 시작 (화면 밝아짐)");
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * fadeSpeed;
            fadeCanvasGroup.alpha = Mathf.Lerp(1, 0, t);
            yield return null;
        }
        fadeCanvasGroup.alpha = 0;
        fadeCanvasGroup.blocksRaycasts = false;
        Debug.Log("페이드 인 완료");
    }
}

// 별도의 트리거 컴포넌트
public class ElevatorTrigger : MonoBehaviour
{
    public System.Action onPlayerEnter;
    public System.Action onPlayerExit;

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[{gameObject.name}] 트리거 감지: {other.name} (태그: {other.tag})");

        if (other.CompareTag("Player"))
        {
            Debug.Log($"[{gameObject.name}] 플레이어 진입!");
            onPlayerEnter?.Invoke();
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log($"[{gameObject.name}] 플레이어 퇴장!");
            onPlayerExit?.Invoke();
        }
    }
}