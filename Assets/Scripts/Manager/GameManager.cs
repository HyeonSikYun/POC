using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FIMSpace.Generating;
using UnityEngine.InputSystem;

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

    [Header("발전기 설정")]
    public GameObject generatorPrefab;
    [Tooltip("발전기가 붙을 벽의 레이어")]
    public LayerMask wallLayer;
    private int requiredGenerators = 0;
    private int activatedGenerators = 0;

    // [삭제됨] 조명 관련 변수들 제거

    [Header("재화 및 강화")]
    public int bioSamples = 0;
    public int upgradeCost = 10;
    public bool isUpgradeMenuOpen = false;

    [Header("게임 상태")]
    public bool isMapGenerated = false;
    public int currentFloor = -9; // 로비: -9층

    [Header("콜라이더 교체 옵션")]
    public bool replaceMeshColliders = true;
    public string[] keepMeshColliderKeywords = new string[] { "Corner", "Stairs", "Door" };

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        if (autoSpawnerSetup == null) autoSpawnerSetup = GetComponent<AutoRoomSpawnerSetup>();
        if (navMeshBaker == null) navMeshBaker = GetComponent<RuntimeNavMeshBaker>();
    }

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateFloor(currentFloor);
            UIManager.Instance.UpdateBioSample(bioSamples);
        }

        // [중요] 혹시 켜져있을지 모르는 안개 끄기 & 환경광 기본값 설정
        RenderSettings.fog = false;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;

        StartCoroutine(InitializeGame());
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
        {
            ToggleUpgradeMenu();
        }
    }

    private IEnumerator InitializeGame()
    {
        Debug.Log($"=== 게임 초기화 시작 (현재 층: {currentFloor}) ===");

        // 1. 맵 생성
        yield return StartCoroutine(GenerateMap());

        // 2. 오브젝트 배치
        PlaceFinishRoomElevator();
        PlaceGenerators();

        // 3. 물리 업데이트 대기
        yield return new WaitForFixedUpdate();

        // 4. 네비메쉬 굽기
        if (navMeshBaker != null)
        {
            navMeshBaker.BakeNavMesh();
        }

        // 5. 몬스터 스폰
        if (isMapGenerated && autoSpawnerSetup != null)
        {
            autoSpawnerSetup.SetupSpawners();
        }

        Debug.Log("=== 스테이지 준비 완료 ===");
    }

    private IEnumerator GenerateMap()
    {
        if (buildPlanner != null)
        {
            buildPlanner.Generate();
            yield return new WaitForSeconds(1f);
            if (replaceMeshColliders) ReplaceProblematicColliders();
            isMapGenerated = true;
        }
    }

    private void PlaceGenerators()
    {
        activatedGenerators = 0;

        // 로비(-9)는 발전기 없음
        if (currentFloor <= -9) return;

        if (currentFloor <= -5)
        {
            requiredGenerators = 1;
            SpawnGeneratorOnWall("KeyRoom");
        }
        else
        {
            requiredGenerators = 2;
            SpawnGeneratorOnWall("KeyRoom");
            SpawnGeneratorOnWall("BossRoom");
        }

        Debug.Log($"필요 발전기: {requiredGenerators}개");

        // 엘리베이터 잠금 (발전기 다 켜야 열림)
        if (currentFinishElevator != null)
        {
            ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
            if (em != null) em.LockDoor();
        }
    }

    private void SpawnGeneratorOnWall(string roomNamePartial)
    {
        GameObject[] allObjs = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        GameObject targetRoom = null;

        foreach (var obj in allObjs)
        {
            if (obj.name.Contains(roomNamePartial))
            {
                targetRoom = obj;
                break;
            }
        }

        if (targetRoom != null && generatorPrefab != null)
        {
            Vector3 center = targetRoom.transform.position + Vector3.up;
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right };
            bool spawned = false;
            ShuffleArray(directions);

            foreach (Vector3 dir in directions)
            {
                RaycastHit hit;
                if (Physics.Raycast(center, dir, out hit, 20f, wallLayer))
                {
                    Vector3 spawnPos = hit.point + (hit.normal * 0.5f);
                    Quaternion spawnRot = Quaternion.LookRotation(hit.normal);
                    Instantiate(generatorPrefab, spawnPos, spawnRot);
                    spawned = true;
                    Debug.Log($"{roomNamePartial} 벽면에 발전기 생성 완료");
                    break;
                }
            }
            if (!spawned)
            {
                Instantiate(generatorPrefab, center, Quaternion.identity);
            }
        }
    }

    private void ShuffleArray<T>(T[] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            T temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }

    // [핵심 로직] 발전기가 켜질 때마다 호출됨 (조명 변경 없음)
    public void OnGeneratorActivated()
    {
        activatedGenerators++;
        Debug.Log($"발전기 켜짐! ({activatedGenerators}/{requiredGenerators})");

        if (activatedGenerators >= requiredGenerators)
        {
            Debug.Log("모든 전력 복구! 엘리베이터 잠금이 해제됩니다.");

            // 피니쉬 엘리베이터 문 잠금 해제 -> 이제 문이 열림
            if (currentFinishElevator != null)
            {
                ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
                if (em != null) em.UnlockDoor();
            }
        }
    }

    public void AddBioSample(int amount)
    {
        bioSamples += amount;
        if (UIManager.Instance != null) UIManager.Instance.UpdateBioSample(bioSamples);
    }

    private void ToggleUpgradeMenu()
    {
        isUpgradeMenuOpen = !isUpgradeMenuOpen;
        if (UIManager.Instance != null) UIManager.Instance.ShowUpgradePanel(isUpgradeMenuOpen);

        if (isUpgradeMenuOpen)
        {
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    public void UpgradeStat(string type)
    {
        if (bioSamples < upgradeCost) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        PlayerController pc = player.GetComponent<PlayerController>();
        GunController gc = player.GetComponentInChildren<GunController>();
        bool success = false;

        switch (type)
        {
            case "HP": pc.Heal(50); success = true; break;
            case "Rifle": success = gc.UpgradeWeaponDamage("Rifle", 5); break;
            case "Bazooka": success = gc.UpgradeWeaponAmmo("Bazooka", 2); break;
            case "Flamethrower": success = gc.UpgradeWeaponDamage("Flamethrower", 2); break;
        }

        if (success)
        {
            bioSamples -= upgradeCost;
            if (UIManager.Instance != null) UIManager.Instance.UpdateBioSample(bioSamples);
        }
    }

    public void RegenerateMap()
    {
        StartCoroutine(RegenerateSequence());
    }

    private IEnumerator RegenerateSequence()
    {
#if UNITY_EDITOR
        UnityEditor.Selection.activeObject = null;
#endif
        currentFloor++;
        if (currentFloor == 0) currentFloor = 1;
        if (UIManager.Instance != null) UIManager.Instance.UpdateFloor(currentFloor);

        isMapGenerated = false;

        // 1. 네비메쉬 삭제
        if (navMeshBaker != null) navMeshBaker.ClearNavMesh();

        // 2. [핵심 추가] 살아있는 모든 좀비/투사체를 풀로 반환 (파괴 방지 및 재사용 준비)
        if (PoolManager.Instance != null)
        {
            PoolManager.Instance.ReturnAllActiveObjects();
        }

        // 3. 오브젝트 및 스포너 파괴
        if (currentFinishElevator != null) Destroy(currentFinishElevator);

        RoomMonsterSpawner[] spawners = FindObjectsByType<RoomMonsterSpawner>(FindObjectsSortMode.None);
        foreach (var spawner in spawners)
        {
            // 이미 풀 매니저가 회수했으므로 ClearAllMonsters 호출 불필요할 수 있으나, 안전하게 스포너 파괴
            Destroy(spawner.gameObject);
        }

        Generator[] generators = FindObjectsByType<Generator>(FindObjectsSortMode.None);
        foreach (var gen in generators) Destroy(gen.gameObject);

        BioSample[] samples = FindObjectsByType<BioSample>(FindObjectsSortMode.None);
        foreach (var sample in samples) Destroy(sample.gameObject);

        if (buildPlanner != null) buildPlanner.ClearGenerated();

        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.2f);

        yield return StartCoroutine(InitializeGame());
    }

    public Transform GetStartRoomSpawnPoint()
    {
        GameObject startRoom = GameObject.Find("StartRoom");
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
        return null;
    }

    private void PlaceFinishRoomElevator()
    {
        if (finishRoomElevatorPrefab == null) return;
        GameObject finishRoom = GameObject.Find("FinishRoom");
        if (finishRoom == null)
        {
            GameObject[] finishes = GameObject.FindGameObjectsWithTag("Finish");
            if (finishes.Length > 0) finishRoom = finishes[0];
        }
        if (finishRoom == null) return;

        Transform spawnPoint = finishRoom.transform.Find("ElevatorSpawnPoint");
        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : finishRoom.transform.position;

        currentFinishElevator = Instantiate(finishRoomElevatorPrefab, spawnPos, Quaternion.identity);
        currentFinishElevator.name = "FinishRoomElevator";

        ElevatorManager em = currentFinishElevator.GetComponent<ElevatorManager>();
        if (em != null) em.SetType(ElevatorManager.ElevatorType.Finish);
    }

    private void ReplaceProblematicColliders()
    {
        MeshCollider[] meshColliders = FindObjectsByType<MeshCollider>(FindObjectsSortMode.None);
        foreach (MeshCollider mc in meshColliders)
        {
            bool shouldSkip = false;
            foreach (string keyword in keepMeshColliderKeywords)
            {
                if (mc.gameObject.name.Contains(keyword)) { shouldSkip = true; break; }
            }
            if (shouldSkip) continue;

            if (mc.sharedMesh != null && !mc.sharedMesh.isReadable)
            {
                GameObject obj = mc.gameObject;
                MeshFilter mf = obj.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    bool isTrigger = mc.isTrigger;
                    PhysicsMaterial mat = mc.sharedMaterial;
                    Bounds b = mf.sharedMesh.bounds;
                    DestroyImmediate(mc);
                    BoxCollider bc = obj.AddComponent<BoxCollider>();
                    bc.center = b.center; bc.size = b.size; bc.isTrigger = isTrigger; bc.material = mat;
                }
                else
                {
                    Bounds b = mc.bounds;
                    bool isTrigger = mc.isTrigger;
                    PhysicsMaterial mat = mc.sharedMaterial;
                    DestroyImmediate(mc);
                    BoxCollider bc = obj.AddComponent<BoxCollider>();
                    bc.center = obj.transform.InverseTransformPoint(b.center);
                    bc.size = obj.transform.InverseTransformVector(b.size);
                    bc.isTrigger = isTrigger; bc.material = mat;
                }
            }
        }
    }
}