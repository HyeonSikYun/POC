using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

public class RuntimeNavMeshBaker : MonoBehaviour
{
    [Header("NavMesh 설정")]
    public NavMeshSurface navMeshSurface;

    [Tooltip("네비메쉬를 구울 대상 레이어 (Wall, Ground, Default 등)")]
    public LayerMask targetLayer;

    private void Awake()
    {
        if (navMeshSurface == null)
        {
            navMeshSurface = GetComponent<NavMeshSurface>();
            if (navMeshSurface == null)
                navMeshSurface = gameObject.AddComponent<NavMeshSurface>();
        }
    }

    public void BakeNavMesh()
    {
        if (navMeshSurface == null) return;

        // 1. 기존 데이터 완전 삭제 (유령 데이터 방지)
        ClearNavMesh();

        // 2. 설정 적용
        navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        navMeshSurface.collectObjects = CollectObjects.All;
        navMeshSurface.layerMask = targetLayer;

        // 3. 동기 방식(BuildNavMesh)으로 즉시 굽기
        // 비동기(Async)는 좀비 스폰 타이밍을 맞추기 까다로우므로, 
        // 맵 생성 시에는 이 방식이 가장 안정적입니다.
        navMeshSurface.BuildNavMesh();

        Debug.Log($"<color=green>NavMesh 베이크 완료 (즉시)!</color>");
    }

    public void ClearNavMesh()
    {
        // [핵심] 현재 씬의 모든 네비메쉬 데이터를 날려버림
        NavMesh.RemoveAllNavMeshData();

        if (navMeshSurface != null)
        {
            navMeshSurface.RemoveData();
            navMeshSurface.navMeshData = null;
        }
        Debug.Log("NavMesh 데이터 완전 초기화 완료");
    }
}