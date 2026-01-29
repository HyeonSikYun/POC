using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoomMonsterSpawner : MonoBehaviour
{
    [Header("방 설정")]
    [Tooltip("스폰할 방의 이름 (PGG에서 생성된 방 이름과 일치해야 함)")]
    public string roomName;

    [Header("스폰 설정")]
    public List<MonsterSpawnInfo> monsterSpawnInfos = new List<MonsterSpawnInfo>();

    [Header("스폰 타이밍")]
    public float initialSpawnDelay = 1f;
    public bool spawnOnRoomEnter = false; // true면 플레이어가 방에 들어올 때 스폰

    [Header("스폰 포인트 설정")]
    public bool useRandomPositions = true;
    public float spawnRadius = 5f;
    public List<Transform> spawnPoints = new List<Transform>(); // 지정된 스폰 포인트들

    private List<GameObject> spawnedMonsters = new List<GameObject>();
    private bool hasSpawned = false;
    public Transform roomTransform; // public으로 변경

    [System.Serializable]
    public class MonsterSpawnInfo
    {
        public string poolTag; // PoolManager에 등록된 태그
        public int count = 1; // 스폰할 개수
        public float spawnInterval = 0.5f; // 각 몬스터 스폰 간격
    }

    private void Start()
    {
        // 방 찾기
        FindRoom();
    }

    public void InitializeSpawner()
    {
        if (!spawnOnRoomEnter && !hasSpawned)
        {
            StartCoroutine(SpawnMonstersWithDelay());
        }
    }

    private void FindRoom()
    {
        // 씬에서 방 이름으로 GameObject 찾기
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            if (obj.name.Contains(roomName))
            {
                roomTransform = obj.transform;
                Debug.Log($"<color=green>방 찾음: {obj.name}</color>");

                // 방의 중심을 이 오브젝트의 위치로 설정
                transform.position = roomTransform.position;
                return;
            }
        }

        Debug.LogWarning($"<color=yellow>방 '{roomName}'을 찾을 수 없습니다!</color>");
    }

    private IEnumerator SpawnMonstersWithDelay()
    {
        yield return new WaitForSeconds(initialSpawnDelay);
        SpawnAllMonsters();
    }

    private void SpawnAllMonsters()
    {
        if (hasSpawned) return;

        StartCoroutine(SpawnMonstersCoroutine());
        hasSpawned = true;
    }

    private IEnumerator SpawnMonstersCoroutine()
    {
        foreach (var spawnInfo in monsterSpawnInfos)
        {
            for (int i = 0; i < spawnInfo.count; i++)
            {
                Vector3 spawnPosition = GetSpawnPosition();
                Quaternion spawnRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                // 1. 일단 몬스터를 풀에서 가져옵니다.
                GameObject monster = PoolManager.Instance.SpawnFromPool(
                    spawnInfo.poolTag,
                    spawnPosition,
                    spawnRotation
                );

                if (monster != null)
                {
                    // 2. [핵심 수정] ZombieAI 컴포넌트를 가져와서 안전하게 Initialize 호출
                    ZombieAI ai = monster.GetComponent<ZombieAI>();
                    if (ai != null)
                    {
                        // 이 함수가 NavMeshAgent를 안전하게 켜줍니다.
                        ai.Initialize(spawnPosition);
                    }

                    spawnedMonsters.Add(monster);
                    Debug.Log($"<color=cyan>몬스터 스폰: {spawnInfo.poolTag} at {roomName}</color>");
                }

                yield return new WaitForSeconds(spawnInfo.spawnInterval);
            }
        }

        Debug.Log($"<color=green>{roomName}에 총 {spawnedMonsters.Count}마리의 몬스터 스폰 완료!</color>");
    }

    private Vector3 GetSpawnPosition()
    {
        if (!useRandomPositions && spawnPoints.Count > 0)
        {
            // 지정된 스폰 포인트 사용
            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Count)];
            return spawnPoint.position;
        }
        else
        {
            // 랜덤 위치 생성 (방 중심 기준)
            Vector2 randomCircle = Random.insideUnitCircle * spawnRadius;
            Vector3 spawnPos = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            return spawnPos;
        }
    }

    public void ClearAllMonsters()
    {
        // 스폰된 모든 몬스터를 풀로 반환
        foreach (GameObject monster in spawnedMonsters)
        {
            if (monster != null && monster.activeInHierarchy)
            {
                // 몬스터가 어느 풀에서 왔는지 찾아서 반환 (단순화를 위해 모든 태그 시도)
                // *주의: 몬스터가 스스로 자신의 풀 태그를 알고 있다면 더 효율적임
                foreach (var spawnInfo in monsterSpawnInfos)
                {
                    // ZombieAI를 끄고 반환해야 안전함 (ZombieAI.ReturnToPool 내부 로직 활용 권장)
                    // 여기서는 PoolManager로 강제 반환 시도
                    PoolManager.Instance.ReturnToPool(spawnInfo.poolTag, monster);
                }
            }
        }

        spawnedMonsters.Clear();
        hasSpawned = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (spawnOnRoomEnter && !hasSpawned && other.CompareTag("Player"))
        {
            Debug.Log($"<color=yellow>플레이어가 {roomName}에 입장!</color>");
            SpawnAllMonsters();
        }
    }

    private void OnDrawGizmos()
    {
        // 스폰 영역 시각화
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, spawnRadius);

        // 스폰 포인트 시각화
        if (spawnPoints.Count > 0)
        {
            Gizmos.color = Color.yellow;
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 0.5f);
                }
            }
        }
    }
}