using System.Collections;
using UnityEngine;
using FIMSpace.Generating;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("PGG 설정")]
    public BuildPlannerExecutor buildPlanner;

    [Header("몬스터 스포너 설정")]
    public AutoRoomSpawnerSetup autoSpawnerSetup;

    [Header("NavMesh 설정")]
    public RuntimeNavMeshBaker navMeshBaker;

    [Header("게임 상태")]
    public bool isMapGenerated = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // AutoRoomSpawnerSetup 자동 할당
        if (autoSpawnerSetup == null)
        {
            autoSpawnerSetup = GetComponent<AutoRoomSpawnerSetup>();
        }

        // NavMeshBaker 자동 할당
        if (navMeshBaker == null)
        {
            navMeshBaker = GetComponent<RuntimeNavMeshBaker>();
        }
    }

    private void Start()
    {
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        Debug.Log("게임 초기화 시작...");

        // PGG 맵 생성
        yield return StartCoroutine(GenerateMap());

        // NavMesh 베이크 (몬스터 스폰 전에 완료)
        if (navMeshBaker != null)
        {
            Debug.Log("NavMesh 베이킹 대기 중...");
            navMeshBaker.BakeNavMesh();
            yield return new WaitForSeconds(navMeshBaker.bakeDelay + 0.5f);
            Debug.Log("NavMesh 베이킹 완료!");
        }

        // NavMesh 베이크 완료 후 몬스터 스폰
        if (isMapGenerated && autoSpawnerSetup != null)
        {
            autoSpawnerSetup.SetupSpawners();
        }

        Debug.Log("게임 초기화 완료!");
    }

    private IEnumerator GenerateMap()
    {
        if (buildPlanner == null)
        {
            Debug.LogError("BuildPlanner가 할당되지 않았습니다!");
            yield break;
        }

        Debug.Log("맵 생성 중...");

        // BuildPlanner의 Generate Objects 실행
        buildPlanner.Generate();

        // 생성 완료까지 대기
        yield return new WaitForSeconds(1f);

        isMapGenerated = true;
        Debug.Log("맵 생성 완료!");
    }

    public void RegenerateMap()
    {
        isMapGenerated = false;

        // NavMesh 제거
        if (navMeshBaker != null)
        {
            navMeshBaker.ClearNavMesh();
        }

        // 기존 맵 제거
        if (buildPlanner != null)
        {
            buildPlanner.ClearGenerated();
        }

        // 모든 활성 몬스터 제거
        RoomMonsterSpawner[] spawners = FindObjectsByType<RoomMonsterSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            spawner.ClearAllMonsters();
            Destroy(spawner.gameObject); // 기존 스포너 제거
        }

        // 새 맵 생성
        StartCoroutine(InitializeGame());
    }
}