using UnityEngine;

public class GeneratorRoomTrigger : MonoBehaviour
{
    private bool hasTriggered = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!hasTriggered && other.CompareTag("Player"))
        {
            hasTriggered = true;

            // 매니저에게 "발전기 방 왔음" 알림
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.OnPlayerEnterGeneratorRoom();
            }

            // 역할 끝났으니 삭제
            Destroy(gameObject);
        }
    }
}