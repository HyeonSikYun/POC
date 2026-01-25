using UnityEngine;
using UnityEngine.InputSystem; // [필수] New Input System 네임스페이스 추가

public class Generator : MonoBehaviour
{
    public bool isActivated = false;
    public GameObject activeEffect; // 켜졌을 때 켤 불빛이나 파티클 오브젝트

    private bool playerInRange = false;

    private void Update()
    {
        if (isActivated) return;

        // [수정] New Input System 방식으로 E 키 입력 감지
        if (playerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Activate();
        }
    }

    private void Activate()
    {
        isActivated = true;
        if (activeEffect != null) activeEffect.SetActive(true);

        Debug.Log("발전기 가동!");

        // 게임 매니저에게 알림
        if (GameManager.Instance != null)
            GameManager.Instance.OnGeneratorActivated();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) playerInRange = false;
    }
}