using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FIMSpace.Generating;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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

    [Header("UI / 이펙트 프리팹")]
    public GameObject damagePopupPrefab;

    [Header("게임 상태")]
    public int currentFloor = -9;
    public bool isMapGenerated = false;
    private int requiredGenerators = 0;
    private int activatedGenerators = 0;
    public bool isPaused = false;
    public bool isRetry = false;

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

    [Header("강화 증가량 설정")]
    public float damageUpgradeVal = 0.1f; // 1회 강화 시 10% 증가
    public float ammoUpgradeVal = 0.2f;   // 1회 강화 시 20% 증가
    public float speedUpgradeVal = 0.05f; // 1회 강화 시 0.05 증가

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

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // 2. 이벤트 해제 (안 하면 에러 남)
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 3. Start 대신 이 함수가 실행됩니다! (재시작할 때마다 호출됨)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("[GameManager] 씬 로드 완료.");

        // 맵 생성기 재연결
        if (buildPlanner == null) buildPlanner = FindAnyObjectByType<BuildPlannerExecutor>();
        if (navMeshBaker == null) navMeshBaker = FindAnyObjectByType<RuntimeNavMeshBaker>();

        if (crosshairTexture != null) cursorHotspot = new Vector2(crosshairTexture.width / 2, crosshairTexture.height / 2);
        SetCursorType(true);

        // [핵심] UI 값 즉시 갱신 (강화 텍스트 {0} 오류 수정)
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateFloor(currentFloor);
            UIManager.Instance.UpdateBioSample(bioSamples);
            // 가격표를 미리 갱신해둬야 탭 눌렀을 때 숫자가 제대로 나옵니다.
            UpdateUIPrices();
        }

        if (isRetry)
        {
            GameObject tutorialCanvas = GameObject.Find("Tutorial Canvas");
            if (tutorialCanvas != null)
            {
                tutorialCanvas.SetActive(false); // 강제 비활성화!
                Debug.Log("[GameManager] 재시작 중이므로 Tutorial Canvas를 제거했습니다.");
            }
            // [핵심] 재시작 시 화면을 즉시 검게 만듭니다. (이동하는 모습 안 보이게)
            if (UIManager.Instance != null) UIManager.Instance.SetFadeAlpha(1f);

            // [핵심] 튜토리얼 텍스트 강제 끄기
            if (UIManager.Instance != null) UIManager.Instance.HideTutorialText();

            StartCoroutine(RetrySequence());
        }
        else if (currentFloor == -9)
        {
            isMapGenerated = true;
            PlaceGenerators();
            if (UIManager.Instance != null) StartCoroutine(UIManager.Instance.FadeIn());
        }
        else
        {
            StartCoroutine(LoadLevelSequence());
        }
    }

    private void UpdateUIPrices()
    {
        if (UIManager.Instance != null)
        {
            // 공격력/탄약은 %단위(int)로 변환해서 전달 (0.1 -> 10)
            int dmgDisplay = (int)(damageUpgradeVal * 100);
            int ammoDisplay = (int)(ammoUpgradeVal * 100);
            int speedDisplay = (int)(speedUpgradeVal * 100);

            UIManager.Instance.UpdateUpgradePrices(
                costHeal, costDamage, costAmmo, costSpeed,
                dmgDisplay, ammoDisplay, speedDisplay
            );
        }
    }
    //private void Start()
    //{
    //    if (crosshairTexture != null) cursorHotspot = new Vector2(crosshairTexture.width / 2, crosshairTexture.height / 2);
    //    SetCursorType(true);
    //    if (UIManager.Instance != null) { UIManager.Instance.UpdateFloor(currentFloor); UIManager.Instance.UpdateBioSample(bioSamples); }

    //    // CASE 1: 재시작(Retry) 상태 -> 이미 있는 'RestArea'로 이동해서 대기
    //    if (isRetry)
    //    {
    //        Debug.Log("=== 게임 재시작: 기존 RestArea로 플레이어 이동 ===");

    //        // 맵 생성은 안 함! 플레이어 위치만 옮김
    //        TeleportPlayerToRestArea();

    //        // 화면 밝히기
    //        if (UIManager.Instance != null) StartCoroutine(UIManager.Instance.FadeIn());
    //    }
    //    // CASE 2: 튜토리얼 (-9층)이고 처음 시작임 -> 튜토리얼 배치
    //    else if (currentFloor == -9)
    //    {
    //        Debug.Log($"=== 게임 시작 (튜토리얼 모드) ===");
    //        isMapGenerated = true;
    //        PlaceGenerators(); // 튜토리얼용 발전기 세팅
    //        if (UIManager.Instance != null) StartCoroutine(UIManager.Instance.FadeIn());
    //    }
    //    // CASE 3: 그 외 (세이브 로드 등) -> 바로 맵 생성
    //    else
    //    {
    //        StartCoroutine(LoadLevelSequence());
    //    }
    //}

    private void Update()
    {
        // 1. 탭(TAB) 키: 강화 메뉴 (기존)
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            // 일시정지 중이 아닐 때만 작동
            if (!isPaused) ToggleUpgradeMenu();
        }

        // 2. [추가] ESC 키: 일시정지 메뉴
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // 강화 메뉴가 열려있으면 -> 강화 메뉴 닫기
            if (isUpgradeMenuOpen)
            {
                ToggleUpgradeMenu();
            }
            // 아니라면 -> 일시정지 토글
            else
            {
                TogglePauseMenu();
            }
        }
    }

    public void TogglePauseMenu()
    {
        isPaused = !isPaused;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPausePanel(isPaused);
        }

        if (isPaused)
        {
            Time.timeScale = 0f; // 시간 정지
            SetCursorType(false); // 마우스 보이기
            SoundManager.Instance.PauseAllGameSounds();
        }
        else
        {
            Time.timeScale = 1f; // 시간 재개
            SetCursorType(true);  // 마우스 숨기고 게임 모드
            SoundManager.Instance.ResumeAllGameSounds();
        }
    }

    public void OnPlayerDead()
    {
        StartCoroutine(GameOverSequence());
    }

    private IEnumerator GameOverSequence()
    {
        // 1. 죽는 모션 감상 (3초 대기)
        yield return new WaitForSeconds(3.0f);

        // 2. 화면 페이드 아웃 (암전)
        if (UIManager.Instance != null) yield return StartCoroutine(UIManager.Instance.FadeOut());

        // 3. 데이터 초기화 (-8층, 샘플 10개, 강화 리셋)
        ResetGameData();

        // 4. 씬 재시작 (현재 씬 다시 로드)
        // 씬이 다시 로드되면 Start()가 호출되면서 초기화된 데이터로 시작합니다.
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);

        // 5. 씬 로딩 후 페이드 인
        //yield return new WaitForSeconds(0.5f); // 로딩 안전 대기
        //if (UIManager.Instance != null) StartCoroutine(UIManager.Instance.FadeIn());
    }

    // 데이터 초기화 함수
    private void ResetGameData()
    {
        Debug.Log("[GameManager] 데이터 초기화");

        currentFloor = -9;
        isRetry = true;
        bioSamples = 10;

        // [핵심] 상태 변수 초기화 (강화 버튼 작동 안 함 문제 해결)
        isPaused = false;
        isUpgradeMenuOpen = false;
        Time.timeScale = 1.0f; // 시간을 다시 흐르게 해야 버튼이 눌립니다!

        globalDamageMultiplier = 1.0f;
        globalAmmoMultiplier = 1.0f;
        globalMoveSpeedMultiplier = 1.0f;

        costDamage = 10; costAmmo = 10; costSpeed = 10; costHeal = 10;
    }

    // GameManager.cs 내부

    // =========================================================
    // [수정됨] 재시작 시퀀스: BGM 끄기 -> 청소 -> 맵생성 -> 대기 -> BGM 켜기 & 문 열림
    // =========================================================
    private IEnumerator RetrySequence()
    {
        yield return null; // 물리 초기화 대기

        // BGM 변경 (소음)
        if (SoundManager.Instance != null)
        {
            if (SoundManager.Instance.elevatorAmbience != null)
                SoundManager.Instance.PlayBGM(SoundManager.Instance.elevatorAmbience);
            else
                SoundManager.Instance.StopBGM();
        }

        // 맵 청소
        CleanupObjectsForNextLevel();
        if (buildPlanner != null) buildPlanner.ClearGenerated();
        if (dynamicSpawner != null) dynamicSpawner.StopSpawning();

        yield return null;

        // RestArea 이동
        GameObject restArea = TeleportPlayerToRestArea();
        ElevatorManager restElevator = null;
        if (restArea != null)
        {
            restElevator = restArea.GetComponent<ElevatorManager>();
            if (restElevator != null)
            {
                restElevator.SetType(ElevatorManager.ElevatorType.RestArea);
                //restElevator.LockDoor();
                restElevator.Initialize();
            }
        }

        // [핵심] 플레이어 이동이 끝났으니 이제 페이드 인 (화면 밝히기)
        // 이때 카메라는 RestArea만 비추고 있고, 맵 레이어는 꺼져 있어서 맵은 안 보입니다.
        if (UIManager.Instance != null) StartCoroutine(UIManager.Instance.FadeIn());

        // --- 맵 생성 ---
        currentFloor = -8;
        if (UIManager.Instance != null) UIManager.Instance.UpdateFloor(currentFloor);

        currentSeed = GenerateValidSeed();
        UnityEngine.Random.InitState(currentSeed);

        if (buildPlanner != null)
        {
            buildPlanner.Seed = currentSeed;
            buildPlanner.Generate();
        }

        yield return new WaitForSeconds(1.5f);

        if (navMeshBaker == null) navMeshBaker = FindAnyObjectByType<RuntimeNavMeshBaker>();
        if (navMeshBaker != null) navMeshBaker.BakeNavMesh();

        yield return new WaitForFixedUpdate();

        PlaceFinishRoomElevator();
        PlaceGenerators();
        isMapGenerated = true;

        // 10초 대기
        float waitTime = 10f;
        while (waitTime > 0)
        {
            waitTime -= Time.deltaTime;
            yield return null;
        }
        Debug.Log("=== [재시작] 문 열림 & 맵 보이기 ===");

        // 메인 BGM
        //if (SoundManager.Instance != null)
        //{
        //    if (SoundManager.Instance.mainBgm != null)
        //        SoundManager.Instance.PlayBGM(SoundManager.Instance.mainBgm);
        //    else
        //        SoundManager.Instance.PlayBGM(SoundManager.Instance.tutorialBgm);
        //}

        //if (restElevator != null) restElevator.UnlockDoor();

        if (autoSpawnerSetup != null) autoSpawnerSetup.SetupSpawners();
        if (dynamicSpawner != null) dynamicSpawner.StartSpawning();

        isRetry = false;
    }

    private GameObject TeleportPlayerToRestArea()
    {
        GameObject restArea = GameObject.Find("RestArea");

        if (restArea == null)
        {
            ElevatorManager[] elevs = FindObjectsByType<ElevatorManager>(FindObjectsSortMode.None);
            foreach (var e in elevs)
            {
                if (e.currentType == ElevatorManager.ElevatorType.RestArea)
                {
                    restArea = e.gameObject;
                    break;
                }
            }
        }

        if (restArea != null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                CharacterController cc = player.GetComponent<CharacterController>();
                if (cc) cc.enabled = false;

                Transform spawnPoint = restArea.transform.Find("PlayerSpawnPoint");
                if (spawnPoint != null)
                {
                    player.transform.position = spawnPoint.position;
                    player.transform.rotation = spawnPoint.rotation;
                }
                else
                {
                    player.transform.position = restArea.transform.position;
                }

                if (cc) cc.enabled = true;
            }
        }
        else
        {
            Debug.LogError("RestArea를 찾을 수 없습니다.");
        }

        return restArea; // 찾은 오브젝트 반환
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

        ZombieAI[] zombies = FindObjectsByType<ZombieAI>(FindObjectsSortMode.None);
        foreach (var z in zombies) { if (z.gameObject.activeSelf) z.Despawn(); }
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
                UpdateUIPrices();
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
                        SoundManager.Instance.PlaySFX(SoundManager.Instance.btnClick);
                        isSuccess = true;
                    }
                }
                break;

            // 2. 전체 공격력 증가
            case "Damage":
                globalDamageMultiplier += damageUpgradeVal; // 변수 사용
                Debug.Log($"전체 공격력 증가! (+{damageUpgradeVal * 100}%)");
                SoundManager.Instance.PlaySFX(SoundManager.Instance.btnClick);
                isSuccess = true;
                break;

            case "Ammo":
                globalAmmoMultiplier += ammoUpgradeVal; // 변수 사용
                Debug.Log($"전체 탄약량 증가! (+{ammoUpgradeVal * 100}%)");
                SoundManager.Instance.PlaySFX(SoundManager.Instance.btnClick);
                if (gun != null) gun.RefreshAmmoUI();
                isSuccess = true;
                break;

            case "Speed":
                globalMoveSpeedMultiplier += speedUpgradeVal; // 변수 사용
                Debug.Log($"이동 속도 증가! (+{speedUpgradeVal * 100}%)");
                SoundManager.Instance.PlaySFX(SoundManager.Instance.btnClick);
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
                UpdateUIPrices();
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

    public void OnClickResume()
    {
        if (isPaused) TogglePauseMenu();
        SoundManager.Instance.PlayUISFX(SoundManager.Instance.btnClick);
    }

    // 2. 옵션 버튼용
    public void OnClickOptions()
    {
        if (UIManager.Instance != null)
        {
            // 옵션 패널이 꺼져있으면 켜고, 켜져있으면 끔 (토글)
            bool isActive = UIManager.Instance.settingsPanel.activeSelf;
            UIManager.Instance.ShowSettingsPanel(!isActive);
            SoundManager.Instance.PlayUISFX(SoundManager.Instance.btnClick);
        }
    }
    public void OnClickOptionsBack()
    {
        if (UIManager.Instance != null)
        {
            // 설정 창 숨기고, 일시정지 메뉴 다시 보이기
            UIManager.Instance.ShowSettingsPanel(false);
            UIManager.Instance.ShowPausePanel(true);
            SoundManager.Instance.PlayUISFX(SoundManager.Instance.btnClick);
        }
    }

    // 3. 게임 종료 버튼용
    public void OnClickQuit()
    {
        UIManager.Instance.ShowQuitConfirmPanel(true); // 확인 창 띄우기
        SoundManager.Instance.PlayUISFX(SoundManager.Instance.btnClick);
    }

    public void OnClickQuitYes()
    {
        Debug.Log("게임 완전 종료!");
        SoundManager.Instance.PlayUISFX(SoundManager.Instance.btnClick);
        Application.Quit();
    }

    // [추가] 종료 확인 창에서 '아니요(No)' 클릭 -> 다시 일시정지 메뉴로
    public void OnClickQuitNo()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowQuitConfirmPanel(false);
            UIManager.Instance.ShowPausePanel(true); // 복귀
            SoundManager.Instance.PlayUISFX(SoundManager.Instance.btnClick);
        }
    }

    public void ShowDamagePopup(Vector3 position, int damage)
    {
        if (damagePopupPrefab != null)
        {
            // 좀비 머리 위쯤에 뜨게 y축 + 1.5f
            Vector3 spawnPos = position + new Vector3(0, 1.5f, 0);

            // 약간 랜덤한 위치에 뜨게 해서 숫자가 겹치지 않게 함
            spawnPos.x += Random.Range(-0.3f, 0.3f);
            spawnPos.z += Random.Range(-0.3f, 0.3f);

            GameObject popup = Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity);

            // 텍스트 설정
            DamagePopup damageScript = popup.GetComponent<DamagePopup>();
            if (damageScript != null)
            {
                damageScript.Setup(damage);
            }
        }
    }
}