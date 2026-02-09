using UnityEngine;

public class BioSample : MonoBehaviour
{
    public int amount = 1;
    public float rotateSpeed = 100f;
    public float pickupRange = 1.5f;

    private Transform playerTransform;

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        // 1. 회전 연출
        transform.Rotate(Vector3.forward * rotateSpeed * Time.deltaTime);

        // 2. 거리 체크 로직
        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            if (distance <= pickupRange)
            {
                GetItem();
            }
        }
    }

    private void GetItem()
    {
        if (GameManager.Instance != null)
        {
            // 1. 게임 매니저에 갯수 추가
            GameManager.Instance.AddBioSample(amount);
            Debug.Log("바이오 샘플 획득!");

            // =========================================================
            // [추가] 튜토리얼 매니저에게 "지금 몇 개인지 체크해봐"라고 알림
            // =========================================================
            if (TutorialManager.Instance != null)
            {
                // 현재 GameManager가 가지고 있는 총 개수를 전달
                TutorialManager.Instance.CheckCapsuleCount(GameManager.Instance.bioSamples);
            }
            // =========================================================
        }

        // 획득 효과음이나 파티클이 있다면 여기서 Instantiate 하세요.

        Destroy(gameObject);
    }
}