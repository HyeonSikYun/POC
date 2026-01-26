using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FIMSpace.Generating;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("커서 설정")]
    public Texture2D crosshairTexture;
    private Vector2 cursorHotspot;

    [Header("PGG 설정")]
    public BuildPlannerExecutor buildPlanner;

    [Header("몬스터 스포너")]
    public AutoRoomSpawnerSetup autoSpawnerSetup;

    [Header("NavMesh")]
    public RuntimeNavMeshBaker navMeshBaker;

    [Header("오브젝트 프리팹")]
    public GameObject finishRoomElevatorPrefab;
    public GameObject generatorPrefab;
    public LayerMask wallLayer;
    private GameObject currentFinishElevator;

    [Header("게임 상태")]
    public int currentFloor = -9; // [수정] -9층(튜토리얼)부터 시작
    public bool isMapGenerated = false;
    private int requiredGenerators = 0;
    private int activatedGenerators = 0;

    // UI 변수들
    public int bioSamples = 0;
    public int upgradeCost = 10;
    public bool isUpgradeMenuOpen = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        if (autoSpawnerSetup == null) autoSpawnerSetup = GetComponent<AutoRoomSpawnerSetup>();
        if (navMeshBaker == null) navMeshBaker = GetComponent<RuntimeNavMeshBaker>();
    }

    private void Start()
    {
        if (crosshairTexture != null) cursorHotspot = new Vector2(crosshairTexture.width / 2, crosshairTexture.height / 2);
        SetCursorType(true);
        if (UIManager.Instance != null) { UIManager.Instance.UpdateFloor(currentFloor); UIManager.Instance.UpdateBioSample(bioSamples); }

        // [핵심] 게임 시작 시점(-9층)에는 맵 생성을 하지 않음!
        Debug.Log($"=== 게임 시작 (현재 층: {currentFloor}F / 튜토리얼) ===");
        Debug.Log(">>> 맵 생성을 건너뜁니다. 튜토리얼 방 오브젝트를 사용하세요.");

        // 튜토리얼용 초기화 (발전기 개수 파악 등)
        isMapGenerated = true;
        PlaceGenerators();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ToggleUpgradeMenu();
    }

    // =================================================================
    // [핵심] 다음 층 로딩 함수 (레스트룸 도착 시 호출됨)
    // =================================================================
    public void LoadNextLevel()
    {
        StartCoroutine(LoadLevelSequence());
    }

    private IEnumerator LoadLevelSequence()
    {
        // 1. 층수 증가 (-9 -> -8)
        currentFloor++;
        Debug.Log($"=== {currentFloor}층 로딩 시작 ===");

        if (UIManager.Instance != null) UIManager.Instance.UpdateFloor(currentFloor);

        // 2. 기존 맵 정리 (튜토리얼 발전기는 남김)
        if (buildPlanner != null) buildPlanner.ClearGenerated();
        CleanupObjectsForNextLevel();

        yield return new WaitForSeconds(0.5f); // 정리 안정화

        // 3. 맵 생성 시작 (플레이어는 레스트룸에서 10초 대기 중)
        Debug.Log("[GameManager] 맵 생성 중...");
        if (buildPlanner != null)
        {
            buildPlanner.Generate();
            yield return new WaitForSeconds(2.0f); // 생성 완료 대기
        }
        isMapGenerated = true;

        // 4. NavMesh 및 오브젝트 배치
        if (navMeshBaker != null) navMeshBaker.BakeNavMesh();
        yield return new WaitForFixedUpdate();

        PlaceFinishRoomElevator();
        PlaceGenerators();

        if (autoSpawnerSetup != null) autoSpawnerSetup.SetupSpawners();

        Debug.Log($"=== {currentFloor}층 로딩 완료 (레스트룸 문이 열리면 진입) ===");
    }

    // =================================================================
    // 발전기 배치 로직 (층별 난이도 수정됨)
    // =================================================================
    private void PlaceGenerators()
    {
        activatedGenerators = 0;

        // CASE 1: -9층 (튜토리얼)
        if (currentFloor == -9)
        {
            Generator[] existingGenerators = FindObjectsByType<Generator>(FindObjectsSortMode.None);
            requiredGenerators = existingGenerators.Length > 0 ? existingGenerators.Length : 1;
            Debug.Log("[튜토리얼] 기존 발전기 등록 완료.");
            return;
        }

        // CASE 2: -8층 ~ -5층 (난이도 하: 1개)
        if (currentFloor <= -5)
        {
            requiredGenerators = 1;
            SpawnGeneratorOnWall("KeyRoom");
            Debug.Log($"[{currentFloor}층] 난이도 하: 발전기 1개 (KeyRoom)");
        }
        // CASE 3: -4층 이상 (난이도 상: 2개)
        else
        {
            requiredGenerators = 2;
            SpawnGeneratorOnWall("KeyRoom");
            if (!SpawnGeneratorOnWall("BossRoom")) SpawnGeneratorOnWall("KeyRoom");
            Debug.Log($"[{currentFloor}층] 난이도 상: 발전기 2개 (KeyRoom + BossRoom)");
        }

        // 피니쉬 엘리베이터 잠금
        if (currentFinishElevator != null)
        {
            ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
            if (em != null) em.LockDoor();
        }
    }

    // =================================================================
    // 정리 함수 (튜토리얼 발전기 보존)
    // =================================================================
    // GameManager.cs 의 CleanupObjectsForNextLevel 함수를 이걸로 덮어쓰세요.

    private void CleanupObjectsForNextLevel()
    {
        // 1. 오브젝트 풀 정리
        if (PoolManager.Instance != null) PoolManager.Instance.ReturnAllActiveObjects();

        // 2. 피니쉬 엘리베이터 삭제
        if (currentFinishElevator != null) Destroy(currentFinishElevator);

        // 3. 발전기 삭제 (튜토리얼 발전기는 제외)
        Generator[] generators = FindObjectsByType<Generator>(FindObjectsSortMode.None);
        foreach (var gen in generators)
        {
            if (!gen.isTutorialGenerator) Destroy(gen.gameObject);
        }

        // 4. 몬스터 스포너 정리
        RoomMonsterSpawner[] spawners = FindObjectsByType<RoomMonsterSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners) { spawner.ClearAllMonsters(); Destroy(spawner.gameObject); }

        // 5. [추가됨] 바이오캡슐(BioSample) 싹 다 삭제
        BioSample[] capsules = FindObjectsByType<BioSample>(FindObjectsSortMode.None);
        foreach (var cap in capsules)
        {
            Destroy(cap.gameObject);
        }

        Debug.Log("[GameManager] 맵 오브젝트 청소 완료 (바이오캡슐 포함).");
    }

    // =================================================================
    // 유틸리티 (기존 유지)
    // =================================================================
    public void OnGeneratorActivated()
    {
        if (currentFloor == -9) return; // 튜토리얼은 무시

        activatedGenerators++;
        Debug.Log($"발전기 가동! ({activatedGenerators}/{requiredGenerators})");

        if (activatedGenerators >= requiredGenerators)
        {
            if (currentFinishElevator != null)
            {
                ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
                if (em != null) em.UnlockDoor();
            }
        }
    }

    // (기존 코드와 동일한 유틸리티 함수들: PlaceFinishRoomElevator, SpawnGeneratorOnWall 등...)
    // 아래는 코드를 줄이기 위해 생략하지 않고 넣어드립니다.

    private void PlaceFinishRoomElevator()
    {
        currentFinishElevator = null;
        if (finishRoomElevatorPrefab == null) return;
        GameObject finishRoom = FindObjectByNameContains("FinishRoom");
        if (finishRoom == null)
        {
            GameObject[] finishes = GameObject.FindGameObjectsWithTag("Finish");
            if (finishes.Length > 0) finishRoom = finishes[0];
        }
        if (finishRoom != null)
        {
            Transform spawnPoint = finishRoom.transform.Find("ElevatorSpawnPoint");
            Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : finishRoom.transform.position;
            currentFinishElevator = Instantiate(finishRoomElevatorPrefab, spawnPos, Quaternion.identity);
            currentFinishElevator.name = "FinishRoomElevator";
            ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
            if (em != null) { em.SetType(ElevatorManager.ElevatorType.Finish); em.LockDoor(); }
        }
    }

    private GameObject FindObjectByNameContains(string partialName)
    {
        GameObject[] allGo = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allGo) { if (go.name.Contains(partialName)) return go; }
        return null;
    }

    private bool SpawnGeneratorOnWall(string roomNamePartial)
    {
        GameObject targetRoom = FindObjectByNameContains(roomNamePartial);
        if (targetRoom == null) return false;

        Vector3 center = targetRoom.transform.position + Vector3.up * 1.5f;
        Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
        ShuffleArray(directions);

        foreach (Vector3 dir in directions)
        {
            RaycastHit hit;
            if (Physics.Raycast(center, dir, out hit, 20f, wallLayer))
            {
                Instantiate(generatorPrefab, hit.point + (hit.normal * 0.5f), Quaternion.LookRotation(hit.normal));
                return true;
            }
        }
        Debug.LogWarning($"[Generator] {targetRoom.name} 벽 찾기 실패");
        return false;
    }

    private void ShuffleArray<T>(T[] array) { for (int i = array.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); T temp = array[i]; array[i] = array[j]; array[j] = temp; } }
    private void SetCursorType(bool isGameCursor) { if (isGameCursor && crosshairTexture != null) Cursor.SetCursor(crosshairTexture, cursorHotspot, CursorMode.Auto); else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
    private void ToggleUpgradeMenu() { isUpgradeMenuOpen = !isUpgradeMenuOpen; if (UIManager.Instance != null) UIManager.Instance.ShowUpgradePanel(isUpgradeMenuOpen); if (isUpgradeMenuOpen) { Time.timeScale = 0f; SetCursorType(false); } else { Time.timeScale = 1f; SetCursorType(true); } }
    public void AddBioSample(int amount) { bioSamples += amount; if (UIManager.Instance != null) UIManager.Instance.UpdateBioSample(bioSamples); }
    public void UpgradeStat(string type) { /* 기존 내용 */ }
    public void RegenerateMap() { LoadNextLevel(); } // 엘리베이터 매니저 호환용
    public Transform GetStartRoomSpawnPoint()
    {
        GameObject startRoom = FindObjectByNameContains("StartRoom");
        if (startRoom == null) startRoom = GameObject.FindGameObjectWithTag("Respawn");
        if (startRoom != null) { Transform spawnPoint = startRoom.transform.Find("PlayerSpawnPoint"); return spawnPoint != null ? spawnPoint : startRoom.transform; }
        return null;
    }
}