using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target; // 플레이어 Transform 연결

    [Header("Settings")]
    public float smoothSpeed = 5f; // 따라가는 속도 (높을수록 빠름)
    public Vector3 offset; // 플레이어와 카메라 사이의 거리 (초기값 자동 설정)

    private void LateUpdate()
    {
        if (target == null) return;

        // 목표 위치 계산 (플레이어 위치 + 초기 거리값)
        Vector3 desiredPosition = target.position + offset;

        // 부드럽게 이동 (Lerp 사용)
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        transform.position = smoothedPosition;
    }
}