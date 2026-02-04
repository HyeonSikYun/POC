using UnityEngine;

public class TutorialGunPickup : MonoBehaviour
{
    public float rotateSpeed = 50f;

    private void Update()
    {
        // 바닥에서 빙글빙글 (Z축 회전)
        transform.Rotate(Vector3.forward * rotateSpeed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController player = other.GetComponent<PlayerController>();

            if (player != null)
            {
                // 1. 플레이어 손에 총 쥐어주기
                player.AcquireGun();
                SoundManager.Instance.PlaySFX(SoundManager.Instance.gunPickup);

                // 2. 매니저에게 "총 먹었어!" 알림 (다음 단계로 진행)
                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.OnGunPickedUp();
                }

                // 3. 이 아이템은 삭제
                Destroy(gameObject);
            }
        }
    }
}