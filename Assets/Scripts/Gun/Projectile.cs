using UnityEngine;

public class Projectile : MonoBehaviour
{
    [Header("발사체 설정")]
    public float speed = 20f;
    public int damage = 100;
    public float explosionRadius = 5f;
    public GameObject explosionEffect; // War FX 등의 폭발 프리팹

    private Rigidbody rb;
    private bool hasHit = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        // 풀링에서 가져올 때 초기화
        hasHit = false;
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero; // (Unity 6) 구버전은 velocity
            rb.angularVelocity = Vector3.zero;
        }

        // 5초 뒤에도 안 터지면 강제로 풀로 반환 (안전장치)
        Invoke(nameof(DisableProjectile), 5f);
    }

    private void OnDisable()
    {
        CancelInvoke();
    }

    // GunController에서 발사할 때 호출하여 속도와 방향 설정
    public void Launch(Vector3 direction)
    {
        // 방향을 확실하게 설정
        transform.forward = direction;

        if (rb != null)
        {
            // 리지드바디를 이용해 물리적으로 힘을 가함 (벽 뚫기 방지)
            rb.linearVelocity = direction * speed;
        }
    }

    // 물리 충돌 (벽, 바닥)
    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;
        // 플레이어 몸체나 다른 총알과 충돌 무시
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Projectile")) return;

        Explode(collision.contacts[0].point, collision.contacts[0].normal);
    }

    // 트리거 충돌 (적 캐릭터)
    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;
        if (other.CompareTag("Player") || other.CompareTag("Projectile")) return;

        // 적이나 특정 물체에 닿으면 즉시 폭발
        Explode(transform.position, -transform.forward);
    }

    void Explode(Vector3 position, Vector3 normal)
    {
        hasHit = true;

        // 폭발 이펙트 생성 (이펙트도 풀링하면 좋지만 일단 Instantiate)
        if (explosionEffect != null)
        {
            Instantiate(explosionEffect, position, Quaternion.LookRotation(normal));
        }

        // 범위 데미지 처리
        Collider[] colliders = Physics.OverlapSphere(position, explosionRadius);
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Enemy"))
            {
                ZombieAI zombie = col.GetComponent<ZombieAI>();
                if (zombie != null)
                {
                    zombie.TakeDamage(damage);
                }
            }
        }

        DisableProjectile();
    }

    void DisableProjectile()
    {
        // 풀 매니저로 반환
        if (gameObject.activeInHierarchy)
        {
            PoolManager.Instance.ReturnToPool("Rocket", gameObject);
        }
    }
}