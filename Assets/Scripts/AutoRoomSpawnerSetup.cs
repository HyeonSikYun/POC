using System.Collections.Generic;
using UnityEngine;

public class AutoRoomSpawnerSetup : MonoBehaviour
{
    [System.Serializable]
    public class RoomSpawnConfig
    {
        [Tooltip("방 이름에 포함될 키워드 (예: Boss, Corridor, Room)")]
        public string roomNameKeyword;

        [Tooltip("이 방에서 스폰할 몬스터 정보")]
        public List<RoomMonsterSpawner.MonsterSpawnInfo> monsterSpawnInfos = new List<RoomMonsterSpawner.MonsterSpawnInfo>();

        [Tooltip("스폰 반경")]
        public float spawnRadius = 5f;

        [Tooltip("초기 스폰 딜레이")]
        public float initialSpawnDelay = 1f;

        [Tooltip("플레이어가 방에 들어올 때 스폰")]
        public bool spawnOnRoomEnter = false;
    }

    [Header("방별 몬스터 설정")]
    public List<RoomSpawnConfig> roomConfigs = new List<RoomSpawnConfig>();

    [Header("방 감지 설정")]
    [Tooltip("방으로 인식할 오브젝트의 태그 (비워두면 이름으로만 검색)")]
    public string roomTag = "";

    [Tooltip("방 검색 대기 시간 (맵 생성 직후)")]
    public float searchDelay = 0.5f;

    public void SetupSpawners()
    {
        Invoke(nameof(FindAndSetupRooms), searchDelay);
    }

    private void FindAndSetupRooms()
    {
        Debug.Log("생성된 방들을 검색 중...");

        // 모든 GameObject 검색
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        int setupCount = 0;

        foreach (GameObject obj in allObjects)
        {
            // 태그가 지정되어 있으면 태그로 필터링
            if (!string.IsNullOrEmpty(roomTag) && !obj.CompareTag(roomTag))
                continue;

            // 각 방 설정과 매칭
            foreach (var config in roomConfigs)
            {
                if (obj.name.Contains(config.roomNameKeyword))
                {
                    SetupSpawnerForRoom(obj, config);
                    setupCount++;
                    break; // 한 방에 하나의 설정만 적용
                }
            }
        }

        Debug.Log($"<color=green>총 {setupCount}개의 방에 몬스터 스포너 설정 완료!</color>");
    }

    private void SetupSpawnerForRoom(GameObject roomObject, RoomSpawnConfig config)
    {
        // 이미 스포너가 있는지 확인
        RoomMonsterSpawner existingSpawner = roomObject.GetComponent<RoomMonsterSpawner>();
        if (existingSpawner != null)
        {
            Debug.Log($"<color=yellow>{roomObject.name}에 이미 스포너가 있습니다. 스킵.</color>");
            return;
        }

        // 스포너 추가
        RoomMonsterSpawner spawner = roomObject.AddComponent<RoomMonsterSpawner>();

        // 설정 적용
        spawner.roomName = roomObject.name;
        spawner.monsterSpawnInfos = new List<RoomMonsterSpawner.MonsterSpawnInfo>(config.monsterSpawnInfos);
        spawner.spawnRadius = config.spawnRadius;
        spawner.initialSpawnDelay = config.initialSpawnDelay;
        spawner.spawnOnRoomEnter = config.spawnOnRoomEnter;
        spawner.useRandomPositions = true;

        // 스포너 초기화 (이미 방이 존재하므로)
        spawner.roomTransform = roomObject.transform;
        spawner.InitializeSpawner();

        Debug.Log($"<color=cyan>{roomObject.name}에 몬스터 스포너 설정 완료!</color>");
    }

    // 테스트용: Inspector에서 수동 실행
    [ContextMenu("Test Setup Spawners")]
    public void TestSetup()
    {
        SetupSpawners();
    }
}