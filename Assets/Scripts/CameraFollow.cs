using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;          // 플레이어

    [Header("Settings")]
    public float smoothTime = 0.2f;   // 따라가는 부드러움 (0.1 ~ 0.3)

    // [중요] 이제 이 오프셋은 '거리'가 아니라 '타겟의 중심점 보정' 용도입니다.
    // 보통 (0, 0, 0)을 쓰거나, 플레이어 발 말고 배꼽을 보게 하려면 (0, 1, 0) 정도 줍니다.
    public Vector3 centerOffset = Vector3.zero;

    [Header("Look Ahead (시야 확보)")]
    public bool enableLookAhead = true;
    public float lookAheadFactor = 3f; // 마우스 쪽으로 얼마나 치우칠지

    private Vector3 currentVelocity;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. 홀더의 목표 위치는 '플레이어 위치' 그 자체입니다. (거리는 자식 카메라가 알아서 함)
        Vector3 finalPosition = target.position + centerOffset;

        // 2. 마우스 방향으로 홀더 자체를 살짝 밀어줍니다. (Look Ahead)
        if (enableLookAhead)
        {
            Vector3 mouseOffset = GetMouseOffset();
            finalPosition += mouseOffset * lookAheadFactor;
        }

        // 3. 부드럽게 이동
        transform.position = Vector3.SmoothDamp(transform.position, finalPosition, ref currentVelocity, smoothTime);
    }

    private Vector3 GetMouseOffset()
    {
        if (Mouse.current == null) return Vector3.zero;

        Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mouseScreenPos);
        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);

        if (groundPlane.Raycast(ray, out float distance))
        {
            Vector3 worldMousePos = ray.GetPoint(distance);
            Vector3 direction = (worldMousePos - target.position).normalized;

            // Y축(높이) 값은 무시하고 X, Z 평면상에서의 방향만 가져옵니다.
            return new Vector3(direction.x, 0, direction.z);
        }

        return Vector3.zero;
    }
}