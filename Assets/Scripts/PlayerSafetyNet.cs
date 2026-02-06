using UnityEngine;
using UnityEngine.AI;

public class PlayerSafetyNet : MonoBehaviour
{
    [Header("설정")]
    [Tooltip("이 높이보다 아래로 떨어지면 구조합니다.")]
    public float minHeight = -20f;

    [Tooltip("구조 시 검색할 반경 (너무 멀리 튕겨 나갔을 때를 대비)")]
    public float rescueSearchRadius = 100f;

    [Tooltip("가장 가까운 땅을 못 찾았을 때 돌아올 비상 위치 (Start 지점 등)")]
    public Vector3 emergencySpawnPoint;

    private CharacterController characterController;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        // 게임 시작 위치를 비상 복귀 지점으로 저장
        emergencySpawnPoint = transform.position;
    }

    private void LateUpdate()
    {
        // 1. 낙사 체크 (맵 아래로 하염없이 떨어지는 경우)
        if (transform.position.y < minHeight)
        {
            RescuePlayer();
        }
    }

    // 2. 벽 뚫림 체크 (간혹 물리 연산 튕김으로 벽 속에 갇혔을 때)
    // 1초에 한 번씩만 체크 (성능 절약)
    private float timer = 0;
    private void Update()
    {
        timer += Time.deltaTime;
        if (timer > 1.0f)
        {
            timer = 0;
            CheckIfStuckInVoid();
        }
    }

    private void CheckIfStuckInVoid()
    {
        // 현재 위치에서 반경 1m 내에 NavMesh(갈 수 있는 땅)가 있는가?
        // 1.0f는 여유값입니다. (점프 중일 수도 있으니 너무 짧으면 안 됨)
        if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            // NavMesh 위가 아니다! (벽 속이거나 맵 밖임)
            // 단, 점프 중일 수 있으니 Y축이 바닥보다 훨씬 높으면 봐줌 (로직 추가 가능)

            // 여기서는 심플하게 "NavMesh에서 너무 멀어지면 구조"로 처리
            // 필요시 주석 해제하여 사용
            // RescuePlayer(); 
        }
    }

    public void RescuePlayer()
    {
        Debug.LogWarning("?? 플레이어 맵 이탈 감지! 구조 시스템 가동!");

        Vector3 targetPos = emergencySpawnPoint; // 기본은 비상 위치

        // 현재 내 위치에서 가장 가까운 NavMesh(정상적인 땅) 위치를 찾음
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, rescueSearchRadius, NavMesh.AllAreas))
        {
            targetPos = hit.position; // 가장 가까운 땅을 찾음!
            targetPos.y += 1.0f; // 땅에 파묻히지 않게 살짝 위로
        }

        // 이동 실행
        Teleport(targetPos);
    }

    private void Teleport(Vector3 pos)
    {
        // [중요] CharacterController가 켜져 있으면 transform.position 변경이 무시됩니다.
        // 반드시 껐다가 -> 옮기고 -> 켜야 합니다.
        if (characterController != null) characterController.enabled = false;

        transform.position = pos;

        if (characterController != null) characterController.enabled = true;
    }
}