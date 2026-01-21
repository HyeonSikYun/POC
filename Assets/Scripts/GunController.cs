using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GunController : MonoBehaviour
{
    [Header("탄약 설정")]
    public int maxAmmo = 30;
    private int currentAmmo = 30;

    [Header("발사 설정")]
    public Transform spawn; // 총구 위치
    public Transform shellPoint; // 탄피 배출 위치
    public float fireRate = 0.1f; // 연사 속도
    public float bulletSpeed = 100f; // 총알 속도
    public float bulletMaxDistance = 100f; // 총알 최대 사거리
    public int damage = 50; // 총기 데미지 (강화 시스템용)

    [Header("탄피 설정")]
    public float shellEjectForce = 175f;
    public float shellForwardForce = 0f;
    public float shellTorque = 10f;
    public float shellLifetime = 3f;

    [Header("트레이서 설정")]
    public bool useTracer = true; // 트레이서 사용 여부
    public float tracerWidth = 0.05f; // 트레이서 두께
    public Color tracerColor = new Color(1f, 0.8f, 0.3f, 1f); // 주황빛 노란색
    public float tracerDuration = 0.1f; // 트레이서 지속 시간

    private PlayerController playerController;
    private Coroutine shootCoroutine;
    private bool isReloading = false;
    public float reloadTime = 2f;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        UIManager.Instance.UpdateAmmo(currentAmmo, maxAmmo);
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (!playerController.hasGun || isReloading) return;

        if (currentAmmo <= 0)
        {
            StartCoroutine(Reload());
            return;
        }

        if (context.started)
        {
            if (shootCoroutine != null) StopCoroutine(shootCoroutine);
            shootCoroutine = StartCoroutine(AutoShootRoutine());
        }

        if (context.canceled)
        {
            if (shootCoroutine != null)
            {
                StopCoroutine(shootCoroutine);
                shootCoroutine = null;
            }
        }
    }

    private IEnumerator AutoShootRoutine()
    {
        while (currentAmmo > 0 && !isReloading)
        {
            Shoot();
            yield return new WaitForSeconds(fireRate);
        }
    }

    private void Shoot()
    {
        currentAmmo--;
        UIManager.Instance.UpdateAmmo(currentAmmo, maxAmmo);

        // 레이캐스트로 충돌 감지
        Ray ray = new Ray(spawn.position, spawn.forward);
        RaycastHit hit;
        Vector3 endPoint;
        bool didHit = false;

        if (Physics.Raycast(ray, out hit, bulletMaxDistance))
        {
            endPoint = hit.point;
            didHit = true;

            // 충돌 이펙트
            EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);

            // 좀비 데미지 처리
            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null)
                {
                    zombie.TakeDamage(damage);
                    Debug.Log($"<color=red>좀비 피격! 데미지: {damage}</color>");
                }
            }
        }
        else
        {
            endPoint = spawn.position + spawn.forward * bulletMaxDistance;
        }

        // 트레이서 효과 (총알 궤적)
        if (useTracer)
        {
            EffectManager.Instance.SpawnTracer(spawn.position, endPoint, tracerWidth, tracerColor, tracerDuration);
        }

        // 탄피 배출
        SpawnShell();
    }

    private void SpawnShell()
    {
        GameObject shell = PoolManager.Instance.SpawnFromPool("Shell", shellPoint.position, Quaternion.identity);
        if (shell != null)
        {
            Rigidbody rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                Vector3 ejectDirection = shellPoint.forward;
                Vector3 forwardDirection = spawn.forward;

                rb.AddForce(ejectDirection * Random.Range(shellEjectForce * 0.85f, shellEjectForce * 1.15f)
                           + forwardDirection * Random.Range(-shellForwardForce, shellForwardForce));

                rb.AddTorque(Random.insideUnitSphere * shellTorque);
            }

            StartCoroutine(ReturnShellAfterDelay(shell, shellLifetime));
        }
    }

    private IEnumerator ReturnShellAfterDelay(GameObject shell, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (shell.activeInHierarchy)
        {
            PoolManager.Instance.ReturnToPool("Shell", shell);
        }
    }

    private IEnumerator Reload()
    {
        isReloading = true;
        UIManager.Instance.ShowReloading(true);

        Debug.Log("장전 중...");
        yield return new WaitForSeconds(reloadTime);

        currentAmmo = maxAmmo;
        isReloading = false;
        UIManager.Instance.ShowReloading(false);
        UIManager.Instance.UpdateAmmo(currentAmmo, maxAmmo);
        Debug.Log("장전 완료!");
    }

    private IEnumerator DisableAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && obj.activeInHierarchy)
        {
            obj.transform.SetParent(null);
            obj.SetActive(false);
        }
    }
}