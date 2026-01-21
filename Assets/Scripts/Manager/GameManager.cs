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

    [Header("엘리베이터 설정")]
    public GameObject finishRoomElevatorPrefab;
    private GameObject currentFinishElevator;

    [Header("콜라이더 교체 옵션")]
    [Tooltip("MeshCollider를 BoxCollider로 교체하여 NavMesh 문제 해결")]
    public bool replaceMeshColliders = true;

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

        if (autoSpawnerSetup == null) autoSpawnerSetup = GetComponent<AutoRoomSpawnerSetup>();
        if (navMeshBaker == null) navMeshBaker = GetComponent<RuntimeNavMeshBaker>();
    }

    private void Start()
    {
        StartCoroutine(InitializeGame());
    }

    private IEnumerator InitializeGame()
    {
        Debug.Log("=== 게임 초기화 시작 ===");

        // 1. PGG 맵 생성
        yield return StartCoroutine(GenerateMap());

        // 2. FinishRoom에 엘리베이터 배치
        PlaceFinishRoomElevator();

        // 3. NavMesh 베이크
        if (navMeshBaker != null)
        {
            Debug.Log("NavMesh 베이킹 중...");
            navMeshBaker.BakeNavMesh();
            // 베이킹 안정성을 위해 약간 대기
            yield return new WaitForSeconds(navMeshBaker.bakeDelay + 0.5f);
            Debug.Log("NavMesh 베이킹 완료!");
        }

        // 4. 몬스터 스폰
        if (isMapGenerated && autoSpawnerSetup != null)
        {
            autoSpawnerSetup.SetupSpawners();
        }

        Debug.Log("=== 스테이지 준비 완료 ===");
    }

    private IEnumerator GenerateMap()
    {
        if (buildPlanner == null)
        {
            Debug.LogError("BuildPlanner가 할당되지 않았습니다!");
            yield break;
        }

        Debug.Log("맵 오브젝트 생성 중...");
        buildPlanner.Generate();

        // 생성 완료 대기 (PGG 내부 로직에 따라 시간 조절 필요할 수 있음)
        yield return new WaitForSeconds(1f);

        // NavMesh 충돌체 문제 해결
        if (replaceMeshColliders)
        {
            ReplaceProblematicColliders();
        }

        isMapGenerated = true;
        Debug.Log("맵 생성 로직 종료");
    }

    private void PlaceFinishRoomElevator()
    {
        if (finishRoomElevatorPrefab == null)
        {
            Debug.LogWarning("FinishRoom 엘리베이터 프리팹이 없습니다!");
            return;
        }

        // FinishRoom 찾기 (PGG로 생성된 방의 이름 확인 필요)
        // 보통 "FinishRoom" 혹은 생성 규칙에 따라 이름이 붙습니다.
        // 만약 못 찾는다면 태그나 BuildPlanner의 결과를 통해 찾아야 합니다.
        GameObject finishRoom = GameObject.Find("FinishRoom");

        // 못 찾았을 경우 대비 (태그로 찾기 시도)
        if (finishRoom == null)
        {
            GameObject[] finishes = GameObject.FindGameObjectsWithTag("Finish");
            if (finishes.Length > 0) finishRoom = finishes[0];
        }

        if (finishRoom == null)
        {
            Debug.LogError("FinishRoom을 찾을 수 없습니다! 방 이름이나 태그를 확인하세요.");
            return;
        }

        // 스폰 위치 결정
        Transform spawnPoint = finishRoom.transform.Find("ElevatorSpawnPoint");
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : finishRoom.transform.position;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : finishRoom.transform.rotation;

        // 엘리베이터 생성
        currentFinishElevator = Instantiate(finishRoomElevatorPrefab, spawnPosition, spawnRotation);
        currentFinishElevator.name = "FinishRoomElevator";

        // 맵이 파괴될 때 같이 파괴되도록 부모 설정 (선택 사항)
        // currentFinishElevator.transform.SetParent(finishRoom.transform); 

        // [중요] ElevatorManager 설정 수정됨
        ElevatorManager elevatorManager = currentFinishElevator.GetComponent<ElevatorManager>();
        if (elevatorManager != null)
        {
            // 수정된 부분: SetAsFinishRoomElevator() -> SetType(...)
            elevatorManager.SetType(ElevatorManager.ElevatorType.Finish);
            Debug.Log("FinishRoom 엘리베이터 설정 완료 (Type: Finish)");
        }
        else
        {
            Debug.LogError("엘리베이터 프리팹에 ElevatorManager 스크립트가 없습니다!");
        }
    }

    // RestArea 엘리베이터가 호출하는 함수
    public void RegenerateMap()
    {
        StartCoroutine(RegenerateSequence());
    }

    private IEnumerator RegenerateSequence()
    {
        Debug.Log("=== 맵 재생성 프로세스 시작 ===");

        isMapGenerated = false;

        // 1. 기존 Finish 엘리베이터 제거 (이미 파괴되었을 수도 있음)
        if (currentFinishElevator != null)
        {
            Destroy(currentFinishElevator);
        }

        // 2. 몬스터 제거
        RoomMonsterSpawner[] spawners = FindObjectsByType<RoomMonsterSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            spawner.ClearAllMonsters();
            Destroy(spawner.gameObject);
        }

        // 3. NavMesh 초기화
        if (navMeshBaker != null)
        {
            navMeshBaker.ClearNavMesh();
        }

        // 4. 기존 맵 오브젝트 제거
        if (buildPlanner != null)
        {
            buildPlanner.ClearGenerated();
        }

        // 청소를 위해 잠시 대기
        yield return null;

        // 5. 새 게임 초기화 (맵 생성 -> 엘리베이터 배치 -> 네비 -> 몬스터)
        yield return StartCoroutine(InitializeGame());
    }

    // StartRoom의 플레이어 스폰 위치 반환 (필요 시 사용)
    public Transform GetStartRoomSpawnPoint()
    {
        // 1. 이름으로 찾기
        GameObject startRoom = GameObject.Find("StartRoom");

        // 2. 태그로 찾기 (보완)
        if (startRoom == null)
        {
            GameObject tagObj = GameObject.FindGameObjectWithTag("Respawn");
            if (tagObj != null) return tagObj.transform;
        }

        if (startRoom != null)
        {
            Transform spawnPoint = startRoom.transform.Find("PlayerSpawnPoint");
            return spawnPoint != null ? spawnPoint : startRoom.transform;
        }

        Debug.LogError("StartRoom을 찾을 수 없습니다!");
        return null;
    }

    private void ReplaceProblematicColliders()
    {
        // (기존 코드와 동일)
        MeshCollider[] meshColliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        foreach (MeshCollider mc in meshColliders)
        {
            if (mc.sharedMesh != null && !mc.sharedMesh.isReadable)
            {
                GameObject obj = mc.gameObject;
                bool isTrigger = mc.isTrigger;
                PhysicsMaterial physicMaterial = mc.sharedMaterial;
                Bounds bounds = mc.bounds;

                DestroyImmediate(mc);

                BoxCollider bc = obj.AddComponent<BoxCollider>();
                bc.center = obj.transform.InverseTransformPoint(bounds.center);
                bc.size = bounds.size;
                bc.isTrigger = isTrigger;
                bc.material = physicMaterial;
            }
        }
    }
}