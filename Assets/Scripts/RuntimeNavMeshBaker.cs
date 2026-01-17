using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class RuntimeNavMeshBaker : MonoBehaviour
{
    [Header("NavMesh 설정")]
    public NavMeshSurface navMeshSurface;

    [Header("베이크 타이밍")]
    public float bakeDelay = 2f; // 맵 생성 후 베이크 대기 시간 (증가)

    private void Start()
    {
        // NavMeshSurface가 없으면 자동 추가
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
            {
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
            }
        }
    }

    public void BakeNavMesh()
    {
        Invoke(nameof(DoBake), bakeDelay);
    }

    private void DoBake()
    {
        if (navMeshSurface != null)
        {
            Debug.Log("NavMesh 베이킹 시작...");
            navMeshSurface.BuildNavMesh();
            Debug.Log("<color=green>NavMesh 베이킹 완료!</color>");
        }
        else
        {
            Debug.LogError("NavMeshSurface가 없습니다!");
        }
    }

    // 맵 재생성 시 NavMesh 제거
    public void ClearNavMesh()
    {
        if (navMeshSurface != null)
        {
            navMeshSurface.RemoveData();
            Debug.Log("NavMesh 제거 완료");
        }
    }

    // 수동 테스트용
    [ContextMenu("Test Bake NavMesh")]
    public void TestBake()
    {
        DoBake();
    }
}