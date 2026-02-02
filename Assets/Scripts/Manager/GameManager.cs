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
    public int currentSeed;       // 현재 적용된 시드 (인스펙터에서 확인용)
    public bool useRandomSeed = true; // 체크하면 랜덤, 끄면 currentSeed 값 사용

    private HashSet<int> bannedSeeds = new HashSet<int>()
    {
        -5089
    };

    [Header("몬스터 스포너")]
    public AutoRoomSpawnerSetup autoSpawnerSetup;
    public DynamicZombieSpawner dynamicSpawner;

    [Header("NavMesh")]
    public RuntimeNavMeshBaker navMeshBaker;

    [Header("오브젝트 프리팹")]
    public GameObject finishRoomElevatorPrefab;
    public GameObject generatorPrefab;
    public LayerMask wallLayer;
    private GameObject currentFinishElevator;

    [Header("게임 상태")]
    public int currentFloor = -9;
    public bool isMapGenerated = false;
    private int requiredGenerators = 0;
    private int activatedGenerators = 0;

    [Header("시야 차단 설정")]
    public LayerMask hideLayerMask;

    [Header("전역 강화 스탯 (기본값 1.0 = 100%)")]
    public float globalDamageMultiplier = 1.0f;    // 전체 공격력 배율
    public float globalAmmoMultiplier = 1.0f;      // 전체 탄약 배율
    public float globalMoveSpeedMultiplier = 1.0f; // 이동 속도 배율

    [Header("강화 비용 설정")]
    public int costHeal = 10;      // 고정
    public int costDamage = 10;    // 증가함
    public int costAmmo = 10;      // 증가함
    public int costSpeed = 10;     // 증가함

    // UI 변수들
    public int bioSamples = 8;
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

        Debug.Log($"=== 게임 시작 (현재 층: {currentFloor}F / 튜토리얼) ===");
        Debug.Log(">>> 맵 생성을 건너뜁니다. 튜토리얼 방 오브젝트를 사용하세요.");

        isMapGenerated = true;
        PlaceGenerators();
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame) ToggleUpgradeMenu();
    }

    public void LoadNextLevel()
    {
        StartCoroutine(LoadLevelSequence());
    }

    public int GenerateValidSeed()
    {
        // 1. 만약 랜덤이 아니라면(테스트용), 현재 설정된 값 그대로 반환
        if (!useRandomSeed) return currentSeed;

        int newSeed;
        int maxAttempts = 100; // 무한루프 방지용 안전장치
        int attempts = 0;

        do
        {
            // 2. 무작위 시드 생성 (int 범위 전체)
            newSeed = Random.Range(int.MinValue, int.MaxValue);
            attempts++;

            // 혹시라도 100번 넘게 뽑았는데 다 꽝이면 그냥 멈춤 (그럴 확률은 거의 0%)
            if (attempts > maxAttempts)
            {
                Debug.LogWarning("유효한 시드를 찾는 데 실패했습니다. 강제로 진행합니다.");
                break;
            }

        } while (bannedSeeds.Contains(newSeed)); // [핵심] 블랙리스트에 있으면 다시 뽑음!

        return newSeed;
    }

    private IEnumerator LoadLevelSequence()
    {
        currentFloor++;

        Camera mainCam = Camera.main;
        if (mainCam != null && currentFloor != -9) // 튜토리얼 아닐 때만
        {
            // 현재 마스크에서 숨길 레이어를 뺍니다 (Map, Wall 등 숨김)
            mainCam.cullingMask &= ~hideLayerMask;
            Debug.Log("[GameManager] 로딩 시작: 맵 레이어를 미리 숨겼습니다.");
        }

        currentSeed = GenerateValidSeed();
        UnityEngine.Random.InitState(currentSeed); // 유니티 랜덤 엔진에 시드 주입
        Debug.Log($"=== [맵 생성] 적용된 시드: {currentSeed} ===");
        Debug.Log($"=== {currentFloor}층 로딩 시작 ===");

        if (UIManager.Instance != null) UIManager.Instance.UpdateFloor(currentFloor);

        if (dynamicSpawner != null) dynamicSpawner.StopSpawning();

        if (buildPlanner != null) buildPlanner.ClearGenerated();
        CleanupObjectsForNextLevel();

        yield return new WaitForSeconds(0.5f);

        Debug.Log("[GameManager] 맵 생성 중...");
        if (buildPlanner != null)
        {
            buildPlanner.Seed = currentSeed;
            buildPlanner.Generate();
            yield return new WaitForSeconds(2.0f);
        }
        isMapGenerated = true;

        if (navMeshBaker != null) navMeshBaker.BakeNavMesh();
        yield return new WaitForFixedUpdate();

        PlaceFinishRoomElevator();
        PlaceGenerators();

        ElevatorManager[] elevators = FindObjectsOfType<ElevatorManager>();
        foreach (var elev in elevators)
        {
            if (elev.currentType == ElevatorManager.ElevatorType.RestArea)
            {
                Debug.Log("[GameManager] 맵 생성 완료 -> RestArea 엘리베이터 초기화 명령!");
                elev.Initialize();
                // ↑ 이 함수가 실행되면서 문이 잠기고, 시야 차단(Culling Mask)이 적용됩니다.
            }
        }

        if (autoSpawnerSetup != null) autoSpawnerSetup.SetupSpawners();

        if (dynamicSpawner != null)
        {
            // 튜토리얼 층(-9)이 아닐 때만 작동
            if (currentFloor != -9)
            {
                dynamicSpawner.StartSpawning();
            }
        }

        Debug.Log($"=== {currentFloor}층 로딩 완료 ===");
    }

    private void PlaceGenerators()
    {
        activatedGenerators = 0;

        // CASE 1: -9층 (튜토리얼)
        if (currentFloor == -9)
        {
            Generator[] existingGenerators = FindObjectsByType<Generator>(FindObjectsSortMode.None);
            requiredGenerators = existingGenerators.Length > 0 ? existingGenerators.Length : 1;
        }
        // CASE 2: -8층 ~ -5층 (난이도 하: 1개)
        else if (currentFloor <= -5)
        {
            requiredGenerators = 1;
            SpawnGeneratorOnWall("KeyRoom");
        }
        // CASE 3: -4층 이상 (난이도 상: 2개)
        else
        {
            requiredGenerators = 2;
            SpawnGeneratorOnWall("KeyRoom");
            if (!SpawnGeneratorOnWall("BossRoom")) SpawnGeneratorOnWall("KeyRoom");
        }

        // [추가됨] 배치 직후 UI 갱신 (예: 0/2)
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateGeneratorCount(activatedGenerators, requiredGenerators);

        if (currentFinishElevator != null)
        {
            ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
            if (em != null) em.LockDoor();
        }
    }

    private void CleanupObjectsForNextLevel()
    {
        if (PoolManager.Instance != null) PoolManager.Instance.ReturnAllActiveObjects();
        if (currentFinishElevator != null) Destroy(currentFinishElevator);

        Generator[] generators = FindObjectsByType<Generator>(FindObjectsSortMode.None);
        foreach (var gen in generators)
        {
            if (!gen.isTutorialGenerator) Destroy(gen.gameObject);
        }

        RoomMonsterSpawner[] spawners = FindObjectsByType<RoomMonsterSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners) { spawner.ClearAllMonsters(); Destroy(spawner.gameObject); }

        BioSample[] capsules = FindObjectsByType<BioSample>(FindObjectsSortMode.None);
        foreach (var cap in capsules) { Destroy(cap.gameObject); }
    }

    public void OnGeneratorActivated()
    {
        if (currentFloor == -9) return;

        activatedGenerators++;
        Debug.Log($"발전기 가동! ({activatedGenerators}/{requiredGenerators})");

        // [추가됨] 작동 시 UI 갱신 (예: 1/2 -> 2/2)
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateGeneratorCount(activatedGenerators, requiredGenerators);

        if (activatedGenerators >= requiredGenerators)
        {
            if (currentFinishElevator != null)
            {
                ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
                if (em != null) em.UnlockDoor();
            }
        }
    }

    public float GetZombieHP_Multiplier()
    {
        // -8층부터 좀비가 나오니까, -8층을 기준(0단계)으로 잡습니다.
        int startFloor = -8;

        // 현재 층이 -9층(튜토리얼)이면 강화 없음 (1.0배)
        if (currentFloor < startFloor) return 1.0f;

        // 진행도: -8층=0, -7층=1, -6층=2 ...
        int levelProgress = currentFloor - startFloor;

        // 배율 공식: 1.0 + (층수 * 0.2) 
        // 예: -8층(1.0배), -7층(1.2배), -6층(1.4배)...
        float multiplier = 1.0f + (levelProgress * 0.2f);

        return multiplier;
    }

    // --- 유틸리티 및 기존 유지 함수들 ---
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
        return false;
    }

    private void ShuffleArray<T>(T[] array) { for (int i = array.Length - 1; i > 0; i--) { int j = Random.Range(0, i + 1); T temp = array[i]; array[i] = array[j]; array[j] = temp; } }
    private void SetCursorType(bool isGameCursor) { if (isGameCursor && crosshairTexture != null) Cursor.SetCursor(crosshairTexture, cursorHotspot, CursorMode.Auto); else Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); Cursor.visible = true; Cursor.lockState = CursorLockMode.None; }
    private void ToggleUpgradeMenu()
    {
        isUpgradeMenuOpen = !isUpgradeMenuOpen;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowUpgradePanel(isUpgradeMenuOpen);

            // [추가] 메뉴를 열 때마다 가격 텍스트가 최신인지 확실히 갱신
            if (isUpgradeMenuOpen)
            {
                UIManager.Instance.UpdateUpgradePrices(costHeal, costDamage, costAmmo, costSpeed);
            }
        }

        if (isUpgradeMenuOpen) { Time.timeScale = 0f; SetCursorType(false); }
        else { Time.timeScale = 1f; SetCursorType(true); }
    }
    public void AddBioSample(int amount) { bioSamples += amount; if (UIManager.Instance != null) UIManager.Instance.UpdateBioSample(bioSamples); }
    // [수정] 버튼 4개에 대응하는 강화 로직
    // [수정된 함수] GameManager.cs 안에 덮어씌우세요
    public void UpgradeStat(string type)
    {
        // 1. 현재 선택한 항목의 가격 확인
        int currentCost = 0;
        switch (type)
        {
            case "HP": currentCost = costHeal; break;
            case "Damage": currentCost = costDamage; break;
            case "Ammo": currentCost = costAmmo; break;
            case "Speed": currentCost = costSpeed; break;
        }

        // 2. 재화 부족 확인
        if (bioSamples < currentCost)
        {
            Debug.Log($"샘플이 부족합니다! (필요: {currentCost}, 보유: {bioSamples})");
            return;
        }

        PlayerController player = FindAnyObjectByType<PlayerController>();
        GunController gun = FindAnyObjectByType<GunController>();
        bool isSuccess = false;

        switch (type)
        {
            // 1. 체력 회복
            case "HP":
                if (player != null)
                {
                    if (player.IsHealthFull())
                    {
                        Debug.Log("체력이 이미 최대입니다!");
                        isSuccess = false;
                    }
                    else
                    {
                        player.Heal(30);
                        Debug.Log("?? 체력 회복 완료!");
                        isSuccess = true;
                    }
                }
                break;

            // 2. 전체 공격력 증가
            case "Damage":
                globalDamageMultiplier += 0.1f;
                Debug.Log($"?? 전체 공격력 증가! (현재 {globalDamageMultiplier * 100}%)");
                isSuccess = true;
                break;

            // 3. 전체 탄약 증가
            case "Ammo":
                globalAmmoMultiplier += 0.2f;
                Debug.Log($"?? 전체 탄약량 증가! (현재 {globalAmmoMultiplier * 100}%)");
                if (gun != null) gun.RefreshAmmoUI();
                isSuccess = true;
                break;

            // 4. 이동 속도 증가
            case "Speed":
                globalMoveSpeedMultiplier += 0.05f;
                Debug.Log($"? 이동 속도 증가! (현재 {globalMoveSpeedMultiplier * 100}%)");
                isSuccess = true;
                break;
        }

        // 3. 성공 처리: 재화 차감 및 가격 인상
        if (isSuccess)
        {
            bioSamples -= currentCost; // 해당 비용만큼 차감

            // [핵심] 가격 인상 (HP 제외)
            switch (type)
            {
                case "Damage": costDamage += 2; break;
                case "Ammo": costAmmo += 2; break;
                case "Speed": costSpeed += 2; break;
                    // HP는 가격 유지
            }

            // UI 갱신 (보유 샘플 및 가격 텍스트)
            if (UIManager.Instance != null)
            {
                UIManager.Instance.UpdateBioSample(bioSamples);
                // 가격이 올랐으니 텍스트 갱신
                UIManager.Instance.UpdateUpgradePrices(costHeal, costDamage, costAmmo, costSpeed);
            }

            if (TutorialManager.Instance != null) TutorialManager.Instance.OnUpgradeCompleted();
        }
    }
    public void RegenerateMap() { LoadNextLevel(); }
    public Transform GetStartRoomSpawnPoint()
    {
        GameObject startRoom = FindObjectByNameContains("StartRoom");
        if (startRoom == null) startRoom = GameObject.FindGameObjectWithTag("Respawn");
        if (startRoom != null) { Transform spawnPoint = startRoom.transform.Find("PlayerSpawnPoint"); return spawnPoint != null ? spawnPoint : startRoom.transform; }
        return null;
    }
}