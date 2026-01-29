using UnityEngine;
using System; // Action을 사용하기 위해 필요

public class ElevatorTrigger : MonoBehaviour
{
    public System.Action onPlayerEnter;
    public System.Action onPlayerExit;
    public System.Action onPlayerStay; // [NEW] 머무는 동안 감지

    private void OnTriggerEnter(Collider other) { if (other.CompareTag("Player")) onPlayerEnter?.Invoke(); }
    private void OnTriggerExit(Collider other) { if (other.CompareTag("Player")) onPlayerExit?.Invoke(); }

    // [NEW] 순간이동으로 들어와서 Enter가 안 눌렸을 때를 대비
    private void OnTriggerStay(Collider other) { if (other.CompareTag("Player")) onPlayerStay?.Invoke(); }
}