using UnityEngine;
using System.Collections.Generic;

public class FlashlightSight : MonoBehaviour
{
    [Header("손전등(부채꼴) 시야 설정")]
    public float viewRadius = 15f; // 손전등 거리
    [Range(0, 360)]
    public float viewAngle = 90f;  // 손전등 각도

    [Header("내 주변(원형) 시야 설정")]
    public float personalLightRadius = 3f; // [추가됨] 내 주변 3미터는 무조건 보임 (Point Light Range와 비슷하게 설정)

    [Header("타겟 설정")]
    public LayerMask targetMask;
    public LayerMask obstacleMask;

    private List<GameObject> visibleTargets = new List<GameObject>();

    void Update()
    {
        FindVisibleTargets();
    }

    void FindVisibleTargets()
    {
        // 1. 초기화: 이전에 보였던 애들 다 끄기
        foreach (var obj in visibleTargets)
        {
            if (obj != null) SetRendererState(obj, false);
        }
        visibleTargets.Clear();

        // 2. 검색 범위: 손전등 거리만큼 일단 다 가져옴 (최대 범위 기준)
        Collider[] targetsInViewRadius = Physics.OverlapSphere(transform.position, viewRadius, targetMask);

        for (int i = 0; i < targetsInViewRadius.Length; i++)
        {
            GameObject target = targetsInViewRadius[i].gameObject;
            Transform targetTransform = target.transform;

            // 거리 계산
            float dstToTarget = Vector3.Distance(transform.position, targetTransform.position);
            Vector3 dirToTarget = (targetTransform.position - transform.position).normalized;

            // [핵심 변경] "내 주변 범위" OR "손전등 시야각" 둘 중 하나라도 만족하면 보임
            bool isVisible = false;

            // 조건 A: 내 주변 (Personal Light) 범위 안인가? (각도 무시, 벽 무시 - 원하면 벽 체크 추가 가능)
            if (dstToTarget <= personalLightRadius)
            {
                isVisible = true;
            }
            // 조건 B: 손전등 (Flashlight) 시야각 안인가?
            else if (Vector3.Angle(transform.forward, dirToTarget) < viewAngle / 2)
            {
                // 벽 체크
                if (!Physics.Raycast(transform.position, dirToTarget, dstToTarget, obstacleMask))
                {
                    isVisible = true;
                }
            }

            // 최종 적용
            if (isVisible)
            {
                SetRendererState(target, true);
                visibleTargets.Add(target);
            }
        }
    }

    void SetRendererState(GameObject target, bool isVisible)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = isVisible;

        Canvas[] canvases = target.GetComponentsInChildren<Canvas>();
        foreach (var c in canvases) c.enabled = isVisible;
    }

    // 에디터 시각화 (선택사항)
    public Vector3 DirFromAngle(float angleInDegrees, bool angleIsGlobal)
    {
        if (!angleIsGlobal) angleInDegrees += transform.eulerAngles.y;
        return new Vector3(Mathf.Sin(angleInDegrees * Mathf.Deg2Rad), 0, Mathf.Cos(angleInDegrees * Mathf.Deg2Rad));
    }
}