using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class ElevatorTrigger : MonoBehaviour
{
    // 외부에서 로직을 연결할 수 있는 대리자(Delegate)
    public Action onPlayerEnter;
    public Action onPlayerExit;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Debug.Log($"[{gameObject.name}] 플레이어 진입");
            onPlayerEnter?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Debug.Log($"[{gameObject.name}] 플레이어 퇴장");
            onPlayerExit?.Invoke();
        }
    }
}