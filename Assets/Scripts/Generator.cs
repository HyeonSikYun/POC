using UnityEngine;
using UnityEngine.InputSystem; // [필수] New Input System

public class Generator : MonoBehaviour
{
    [Header("상태")]
    public bool isActivated = false;

    [Tooltip("체크하면 게임 매니저에게 알리지 않습니다. (튜토리얼용)")]
    public bool isTutorialGenerator = false; // [추가됨] 튜토리얼 구분 변수

    [Header("이펙트")]
    public GameObject activeEffect; // 켜졌을 때 켤 불빛이나 파티클

    private bool playerInRange = false;

    private void Update()
    {
        if (isActivated) return;

        // E 키 입력 감지
        if (playerInRange && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            Activate();
        }
    }

    private void Activate()
    {
        if (isActivated) return; // 중복 실행 방지

        isActivated = true;
        if (activeEffect != null) activeEffect.SetActive(true);

        Debug.Log($"발전기 가동! (튜토리얼 모드: {isTutorialGenerator})");

        // [핵심 수정] 튜토리얼이 '아닐 때만' 게임 매니저에게 보고함
        // 튜토리얼일 때는 TutorialElevator가 알아서 이 스크립트의 isActivated를 감지함
        if (!isTutorialGenerator)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGeneratorActivated();
        }
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