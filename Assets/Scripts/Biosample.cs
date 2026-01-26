using UnityEngine;

public class BioSample : MonoBehaviour
{
    public int amount = 1;
    public float rotateSpeed = 100f;
    public float pickupRange = 1.5f; // 이 거리 안에 들어오면 먹어짐

    private Transform playerTransform;

    private void Start()
    {
        // 플레이어 찾기 (Tag가 Player인 오브젝트)
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        // 1. 회전 연출
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime);

        // 2. 거리 체크 로직 (물리 충돌 대신 사용)
        if (playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, playerTransform.position);

            // 거리가 1.5m 이내면 획득 처리
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
            GameManager.Instance.AddBioSample(amount);
            Debug.Log("바이오 샘플 획득! (거리 감지)");
        }
        Destroy(gameObject);
    }
}