using UnityEngine;
using UnityEngine.InputSystem;

public class Generator : MonoBehaviour
{
    [Header("상태")]
    public bool isActivated = false;

    [Tooltip("체크하면 게임 매니저에게 알리지 않습니다. (튜토리얼용)")]
    public bool isTutorialGenerator = false;

    [Header("이펙트")]
    public GameObject activeEffect;

    [Header("상호작용 설정")]
    public float holdDuration = 2.0f; // [추가됨] 누르고 있어야 하는 시간 (2초)
    private float currentHoldTime = 0f;
    private bool playerInRange = false;

    private void Update()
    {
        // 이미 켜졌으면 아무것도 안 함
        if (isActivated) return;

        // 플레이어가 범위 안에 있고 E키를 누르고 있을 때
        if (playerInRange && Keyboard.current != null && Keyboard.current.eKey.isPressed)
        {
            // 시간 증가
            currentHoldTime += Time.deltaTime;

            // UI 게이지 업데이트 (0 ~ 1 사이 값)
            if (UIManager.Instance != null)
                UIManager.Instance.UpdateInteractionProgress(currentHoldTime / holdDuration);

            // 시간이 다 차면 발동
            if (currentHoldTime >= holdDuration)
            {
                SoundManager.Instance.PlaySFX(SoundManager.Instance.generateOn);
                Activate();
            }
        }
        else
        {
            // 키를 떼거나 범위 밖이면 초기화
            if (currentHoldTime > 0)
            {
                currentHoldTime = 0f;
                if (UIManager.Instance != null)
                    UIManager.Instance.UpdateInteractionProgress(0f); // 게이지 숨김
            }
        }
    }

    private void Activate()
    {
        if (isActivated) return;

        isActivated = true;
        currentHoldTime = 0f;

        // 완료 시 UI 정리
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateInteractionProgress(0f);
            UIManager.Instance.ShowInteractionPrompt(false); // 완료되면 안내 문구 끄기
        }

        if (activeEffect != null) activeEffect.SetActive(true);

        Debug.Log($"발전기 가동! (튜토리얼 모드: {isTutorialGenerator})");

        if (!isTutorialGenerator)
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnGeneratorActivated();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !isActivated)
        {
            playerInRange = true;
            // 안내 문구 표시 ("E키를 길게 눌러 작동")
            if (UIManager.Instance != null)
                UIManager.Instance.ShowInteractionPrompt(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
            // 안내 문구 및 게이지 숨김
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowInteractionPrompt(false);
                UIManager.Instance.UpdateInteractionProgress(0f);
            }
            currentHoldTime = 0f;
        }
    }
}