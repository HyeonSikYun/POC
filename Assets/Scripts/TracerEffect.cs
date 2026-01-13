using UnityEngine;

public class TracerEffect : MonoBehaviour, IPooledObject
{
    private LineRenderer lineRenderer;
    private Vector3 startPosition;
    private Vector3 direction;
    private Vector3 targetPoint;
    private float length;
    private float speed;
    private float lifetime;
    private Color startColor;
    private Color endColor;

    private float currentDistance = 0f;
    private float spawnTime;
    private float maxDistance;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;

            // 기본 Material 설정
            Material mat = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.material = mat;
        }
    }

    public void Initialize(Vector3 start, Vector3 dir, Vector3 target, float len,
                          float spd, float width, Color startCol, Color endCol, float life)
    {
        startPosition = start;
        direction = dir.normalized;
        targetPoint = target;
        length = len;
        speed = spd;
        startColor = startCol;
        endColor = endCol;
        lifetime = life;

        currentDistance = 0f;
        maxDistance = Vector3.Distance(start, target);

        // LineRenderer 설정
        lineRenderer.startWidth = width;
        lineRenderer.endWidth = width * 0.5f;
        lineRenderer.startColor = startCol;
        lineRenderer.endColor = endCol;
    }

    public void OnObjectSpawn()
    {
        spawnTime = Time.time;
        currentDistance = 0f;
    }

    private void Update()
    {
        // 수명이 다하면 풀로 반환
        if (Time.time - spawnTime >= lifetime)
        {
            PoolManager.Instance.ReturnToPool("Tracer", gameObject);
            return;
        }

        // 트레이서 이동
        currentDistance += speed * Time.deltaTime;

        // 목표 지점에 도달하면 반환
        if (currentDistance >= maxDistance)
        {
            PoolManager.Instance.ReturnToPool("Tracer", gameObject);
            return;
        }

        // 트레이서 시작점과 끝점 계산 (끊긴 레이저)
        Vector3 tracerStart = startPosition + direction * Mathf.Max(0, currentDistance - length);
        Vector3 tracerEnd = startPosition + direction * currentDistance;

        // 목표 지점을 넘지 않도록 클램프
        if (Vector3.Distance(startPosition, tracerEnd) > maxDistance)
        {
            tracerEnd = targetPoint;
        }
        if (Vector3.Distance(startPosition, tracerStart) > maxDistance)
        {
            tracerStart = targetPoint;
        }

        lineRenderer.SetPosition(0, tracerStart);
        lineRenderer.SetPosition(1, tracerEnd);

        // 페이드 아웃 (시간이 지날수록 투명해짐)
        float fadeProgress = (Time.time - spawnTime) / lifetime;
        float alpha = 1f - fadeProgress;

        Color currentStartColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
        Color currentEndColor = new Color(endColor.r, endColor.g, endColor.b, alpha * 0.5f);

        lineRenderer.startColor = currentStartColor;
        lineRenderer.endColor = currentEndColor;
    }
}