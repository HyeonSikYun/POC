using UnityEngine;

public class EndingTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        // 플레이어가 닿으면
        if (other.CompareTag("Player"))
        {
            // 매니저에게 하얀색 엔딩 시작하라고 신호 보냄
            if (EndingSceneManager.Instance != null)
            {
                EndingSceneManager.Instance.TriggerWhiteEnding();
            }
        }
    }
}