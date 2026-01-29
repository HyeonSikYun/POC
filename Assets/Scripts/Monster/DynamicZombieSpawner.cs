using System.Collections;
using System.Collections.Generic; // List 사용을 위해
using UnityEngine;
using UnityEngine.AI;

public class DynamicZombieSpawner : MonoBehaviour
{
    [Header("스폰 설정")]
    public List<string> zombiePoolTags;
    public int targetZombieCount = 15;
    public float checkInterval = 3.0f;

    [Header("거리 설정")]
    public float minDistance = 15f;
    public float maxDistance = 30f;

    [Header("끼임 방지 설정 [추가됨]")]
    public LayerMask obstacleLayer; // 벽(Wall) 레이어를 선택하세요
    public float zombieRadius = 0.8f; // 좀비의 뚱뚱한 정도 (반지름)

    private Transform playerTransform;
    private Camera mainCamera;
    private bool isSpawning = false;

    public void StartSpawning()
    {
        isSpawning = true;
        mainCamera = Camera.main;
        StartCoroutine(SpawnRoutine());
        Debug.Log("<color=green>>>> 다이내믹 좀비 리스폰 시스템 가동</color>");
    }

    public void StopSpawning()
    {
        isSpawning = false;
        StopAllCoroutines();
    }

    private IEnumerator SpawnRoutine()
    {
        while (isSpawning)
        {
            yield return new WaitForSeconds(checkInterval);

            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
                else continue;
            }

            ZombieAI[] activeZombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
            int currentCount = 0;
            foreach (var z in activeZombies)
            {
                if (!z.isDead && z.gameObject.activeInHierarchy) currentCount++;
            }

            if (currentCount < targetZombieCount)
            {
                int spawnAmount = Mathf.Min(2, targetZombieCount - currentCount);
                for (int i = 0; i < spawnAmount; i++)
                {
                    TrySpawnZombie();
                    yield return new WaitForSeconds(0.1f);
                }
            }
        }
    }

    private void TrySpawnZombie()
    {
        if (zombiePoolTags.Count == 0) return;

        // [수정] 한 번에 못 찾을 수도 있으니 10번 정도 시도 (while문 대신 for문으로 안전장치)
        for (int i = 0; i < 10; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minDistance, maxDistance);
            Vector3 spawnPos = playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y) * distance;

            NavMeshHit hit;
            // 1. NavMesh 위인지 확인
            if (NavMesh.SamplePosition(spawnPos, out hit, 2.0f, NavMesh.AllAreas))
            {
                Vector3 finalPos = hit.position;

                // 2. 화면 밖인지 확인
                if (!IsVisibleToCamera(finalPos))
                {
                    // 3. [핵심 추가] 그 자리에 벽이 있는지 물리 체크!
                    // Physics.CheckSphere: 해당 위치에 가상의 공을 그려서 충돌체가 있는지 검사
                    if (!Physics.CheckSphere(finalPos, zombieRadius, obstacleLayer))
                    {
                        // 벽이 없다면(false) 소환 진행!
                        string selectedTag = zombiePoolTags[Random.Range(0, zombiePoolTags.Count)];

                        GameObject monster = PoolManager.Instance.SpawnFromPool(
                            selectedTag,
                            finalPos,
                            Quaternion.LookRotation(playerTransform.position - finalPos)
                        );

                        if (monster != null)
                        {
                            ZombieAI ai = monster.GetComponent<ZombieAI>();
                            if (ai != null) ai.Initialize(finalPos);
                        }

                        return; // 성공했으니 함수 종료
                    }
                    // else: 벽에 닿았으면 loop 돌면서 다시 위치 찾음
                }
            }
        }
    }

    private bool IsVisibleToCamera(Vector3 position)
    {
        if (mainCamera == null) return false;
        Vector3 viewPos = mainCamera.WorldToViewportPoint(position);
        return (viewPos.x > -0.2f && viewPos.x < 1.2f &&
                viewPos.y > -0.2f && viewPos.y < 1.2f &&
                viewPos.z > 0);
    }

    // [추가] 에디터에서 체크 범위 눈으로 확인하기
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        // 플레이어가 있다면 플레이어 주변에 스폰 가능 범위 표시 (도넛 모양은 못 그리지만 대략적으로)
        if (playerTransform != null)
        {
            Gizmos.DrawWireSphere(playerTransform.position, minDistance);
            Gizmos.DrawWireSphere(playerTransform.position, maxDistance);
        }
    }
}