using UnityEngine;
using System.Collections;

public class TutorialElevator : MonoBehaviour
{
    [Header("1. 연결 설정 (필수)")]
    public Generator linkedGenerator; // 씬의 발전기
    public GameObject insideTriggerObject; // 플레이어가 밟을 트리거 오브젝트

    [Tooltip("여기에 레스트룸의 스폰 포인트(Transform)를 직접 넣으세요")]
    public Transform fixedDestination; // [NEW] 직접 할당할 이동 목표 지점

    [Header("2. 문 설정")]
    public Transform leftDoor;
    public Transform rightDoor;
    public float doorOpenDistance = 0.7f;
    public float doorSpeed = 2f;

    [Header("3. 조명/연출")]
    public Light statusLight;
    public CanvasGroup fadeCanvas;
    public Color lockedColor = Color.red;
    public Color unlockedColor = Color.green;

    // 내부 변수
    private bool isLocked = true;
    private bool isMoving = false;
    private Vector3 leftClosedPos, rightClosedPos;

    private void Start()
    {
        // 문 위치 저장
        if (leftDoor) leftClosedPos = leftDoor.localPosition;
        if (rightDoor) rightClosedPos = rightDoor.localPosition;

        // 초기 상태: 잠김 (빨간불)
        if (statusLight) statusLight.color = lockedColor;

        // 트리거 연결 로직
        if (insideTriggerObject != null)
        {
            TutorialTriggerListener listener = insideTriggerObject.GetComponent<TutorialTriggerListener>();
            if (listener == null) listener = insideTriggerObject.AddComponent<TutorialTriggerListener>();
            listener.onPlayerEnter = OnPlayerEnterElevator;

            Collider col = insideTriggerObject.GetComponent<Collider>();
            if (col) col.isTrigger = true;
        }
        else
        {
            Debug.LogError("[Tutorial] 'Inside Trigger Object'가 비어있습니다!");
        }
    }

    private void Update()
    {
        if (isLocked && linkedGenerator != null && linkedGenerator.isActivated)
        {
            UnlockAndOpen();
        }
    }

    public void UnlockAndOpen()
    {
        if (!isLocked) return;

        Debug.Log("[Tutorial] 발전기 가동! 문을 엽니다.");
        isLocked = false;
        if (statusLight) statusLight.color = unlockedColor;

        StartCoroutine(MoveDoors(true));
    }

    public void OnPlayerEnterElevator()
    {
        if (isLocked || isMoving) return;

        Debug.Log("[Tutorial] 플레이어 감지됨! 문 닫고 이동 시작.");
        StartCoroutine(TransportSequence());
    }

    // =========================================================
    // [수정됨] 직접 지정한 위치(fixedDestination)로 이동
    // =========================================================
    private IEnumerator TransportSequence()
    {
        isMoving = true;

        // 1. 문 닫기
        yield return StartCoroutine(MoveDoors(false));

        // 2. 페이드 아웃
        if (fadeCanvas)
        {
            float t = 0;
            while (t < 1) { t += Time.deltaTime * 3f; fadeCanvas.alpha = t; yield return null; }
            fadeCanvas.alpha = 1;
        }

        // 3. 플레이어 이동 (직접 할당한 변수 사용)
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player != null && fixedDestination != null)
        {
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false; // 물리 끄기

            // [핵심] 할당된 위치로 이동
            player.transform.position = fixedDestination.position;
            player.transform.rotation = fixedDestination.rotation;

            if (cc) cc.enabled = true; // 다시 켜기
            Debug.Log($"[Tutorial] 지정된 위치({fixedDestination.name})로 이동 완료.");
        }
        else
        {
            if (fixedDestination == null) Debug.LogError("[Tutorial] 이동할 위치(Fixed Destination)가 할당되지 않았습니다!");
            if (player == null) Debug.LogError("[Tutorial] 플레이어를 찾을 수 없습니다!");
        }

        // 4. 레스트룸 도착 -> -8층 생성 요청
        if (GameManager.Instance != null)
        {
            Debug.Log("[Tutorial] 레스트룸 도착. -8층 생성 요청.");
            GameManager.Instance.LoadNextLevel();
        }

        // 5. 페이드 인
        if (fadeCanvas)
        {
            float t = 1;
            while (t > 0) { t -= Time.deltaTime * 3f; fadeCanvas.alpha = t; yield return null; }
            fadeCanvas.alpha = 0;
        }
    }

    private IEnumerator MoveDoors(bool open)
    {
        float t = 0;
        Vector3 lTarget = open ? leftClosedPos + new Vector3(0, 0, -doorOpenDistance) : leftClosedPos;
        Vector3 rTarget = open ? rightClosedPos + new Vector3(0, 0, doorOpenDistance) : rightClosedPos;
        Vector3 lStart = leftDoor.localPosition;
        Vector3 rStart = rightDoor.localPosition;

        while (t < 1)
        {
            t += Time.deltaTime * doorSpeed;
            if (leftDoor) leftDoor.localPosition = Vector3.Lerp(lStart, lTarget, t);
            if (rightDoor) rightDoor.localPosition = Vector3.Lerp(rStart, rTarget, t);
            yield return null;
        }
    }
}

// 보조 스크립트 (유지)
public class TutorialTriggerListener : MonoBehaviour
{
    public System.Action onPlayerEnter;
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) onPlayerEnter?.Invoke();
    }
}