using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public enum WeaponType
{
    Rifle,
    Bazooka,
    FlameThrower
}

[System.Serializable]
public class WeaponStats
{
    public string weaponName;
    public WeaponType type;
    public int maxAmmo = 30;
    public float fireRate = 0.1f;
    public int damage = 50;
    public float range = 100f;
    public bool isAutomatic = true;

    [Header("발사체 설정")]
    public bool useProjectile = false;
    public string projectilePoolTag = "Rocket";

    [Header("이펙트 설정")]
    public bool useTracer = true;
    public Color tracerColor = Color.yellow;
    public bool useParticle = false;
    public ParticleSystem weaponParticle;
    public bool ejectShell = true;
}

public class GunController : MonoBehaviour
{
    [Header("무기 설정")]
    public List<WeaponStats> weapons;
    private int currentWeaponIndex = 0;
    private WeaponStats currentWeapon;

    [Header("상태")]
    private int currentAmmo;
    private bool isReloading = false;
    private bool isHoldingTrigger = false;

    [Header("필수 할당")]
    public Transform spawn;
    public Transform shellPoint;
    public float reloadTime = 3f;

    private PlayerController playerController;
    private Coroutine shootCoroutine;

    [Header("오디오 소스 연결")]
    public AudioSource gunAudioSource;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    // =========================================================
    // [핵심] 전역 배율 적용 헬퍼 함수들
    // =========================================================
    private int GetFinalDamage()
    {
        // GameManager가 없으면 기본값 1.0 적용
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalDamageMultiplier : 1.0f;
        return Mathf.RoundToInt(currentWeapon.damage * multiplier);
    }

    private int GetFinalMaxAmmo()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalAmmoMultiplier : 1.0f;
        return Mathf.RoundToInt(currentWeapon.maxAmmo * multiplier);
    }

    // =========================================================
    // [추가됨] GameManager가 강화 직후 호출하는 UI 갱신 함수
    // =========================================================
    public void RefreshAmmoUI()
    {
        // 늘어난 최대 탄약량으로 UI를 즉시 갱신합니다.
        if (UIManager.Instance != null && currentWeapon != null)
        {
            UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        }
    }

    private void EquipWeapon(int index)
    {
        if (currentWeapon != null && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
            currentWeapon.weaponParticle.gameObject.SetActive(false);
        }

        currentWeaponIndex = index;
        currentWeapon = weapons[currentWeaponIndex];

        // [적용] 배율이 적용된 최대 탄약으로 설정
        currentAmmo = GetFinalMaxAmmo();

        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.gameObject.SetActive(true);
            currentWeapon.weaponParticle.Stop();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);
            // [적용] 배율 적용된 값 UI 표시
            UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
            UIManager.Instance.ShowReloading(false);
        }

        Debug.Log($"무기 장착: {currentWeapon.weaponName}");
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (GameManager.Instance != null && GameManager.Instance.isUpgradeMenuOpen) return;

        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        if (!playerController.hasGun || isReloading) return;

        if (currentAmmo <= 0)
        {
            if (context.started) StartCoroutine(ReloadAndSwitch());
            return;
        }

        if (context.started)
        {
            isHoldingTrigger = true;

            if (currentWeapon.type == WeaponType.FlameThrower)
            {
                gunAudioSource.clip = SoundManager.Instance.flameThrower;
                gunAudioSource.loop = true; // 반복 재생 ON
                gunAudioSource.Play();
            }
            if (currentWeapon.useParticle && currentWeapon.weaponParticle != null)
            {
                currentWeapon.weaponParticle.Play();
            }

            if (currentWeapon.isAutomatic)
            {
                if (shootCoroutine == null) shootCoroutine = StartCoroutine(AutoShootRoutine());
            }
            else
            {
                Shoot();
            }
        }
        else if (context.canceled)
        {
            isHoldingTrigger = false;

            if (currentWeapon.type == WeaponType.FlameThrower)
            {
                gunAudioSource.Stop();
                gunAudioSource.loop = false;
            }
            if (currentWeapon.useParticle && currentWeapon.weaponParticle != null)
            {
                currentWeapon.weaponParticle.Stop();
            }

            if (shootCoroutine != null)
            {
                StopCoroutine(shootCoroutine);
                shootCoroutine = null;
            }
        }
    }

    private IEnumerator AutoShootRoutine()
    {
        while (isHoldingTrigger && currentAmmo > 0 && !isReloading)
        {
            Shoot();
            yield return new WaitForSeconds(currentWeapon.fireRate);
        }

        if (currentWeapon.useParticle && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
        }
        shootCoroutine = null;
    }

    private void Shoot()
    {
        currentAmmo--;

        if (UIManager.Instance != null)
        {
            // [적용] 쏠 때도 늘어난 최대 탄약량 기준으로 갱신
            UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        }

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane gunPlane = new Plane(Vector3.up, spawn.position);
        float distance;
        Vector3 targetPoint = Vector3.zero;

        if (gunPlane.Raycast(ray, out distance))
        {
            targetPoint = ray.GetPoint(distance);
        }

        Vector3 fireDirection = (targetPoint - spawn.position).normalized;

        if (currentWeapon.useProjectile)
        {
            Quaternion fireRotation = Quaternion.LookRotation(fireDirection);

            GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, spawn.position, fireRotation);
            SoundManager.Instance.PlaySFX(SoundManager.Instance.Bazooka,0.1f);
            if (projectileObj != null)
            {
                Projectile proj = projectileObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    // [적용] 전역 데미지 배율 적용
                    proj.damage = GetFinalDamage();
                    proj.Launch(fireDirection);
                }
            }
        }
        else
        {
            FireRaycast(fireDirection);
            if (currentWeapon.type == WeaponType.Rifle)
            {
                SoundManager.Instance.PlaySFX(SoundManager.Instance.Rifle,0.1f);
            }
        }

        if (currentWeapon.ejectShell) SpawnShell();

        if (currentAmmo <= 0)
        {
            StartCoroutine(ReloadAndSwitch());
        }
    }

    private void FireRaycast(Vector3 direction)
    {
        Ray ray = new Ray(spawn.position, direction);
        RaycastHit hit;
        Vector3 endPoint;

        if (Physics.Raycast(ray, out hit, currentWeapon.range))
        {
            endPoint = hit.point;

            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null)
                {
                    // [적용] 전역 데미지 배율 적용
                    zombie.TakeDamage(GetFinalDamage());
                }
            }
            else if (!currentWeapon.useParticle)
            {
                EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);
            }
        }
        else
        {
            endPoint = spawn.position + (direction * currentWeapon.range);
        }

        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(spawn.position, endPoint, 0.05f, currentWeapon.tracerColor, 0.05f);
        }
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
                Vector3 ejectDir = shellPoint.right + Vector3.up * 0.5f;
                rb.AddForce(ejectDir * 5f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * 10f);
            }
            StartCoroutine(ReturnShellAfterDelay(shell, 3f));
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

    private IEnumerator ReloadAndSwitch()
    {
        if (isReloading) yield break;
        isReloading = true;

        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        if (currentWeapon.weaponParticle != null) currentWeapon.weaponParticle.Stop();

        if (gunAudioSource.isPlaying && currentWeapon.type == WeaponType.FlameThrower)
        {
            gunAudioSource.Stop();
            gunAudioSource.loop = false;
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowReloading(true);
        }
        SoundManager.Instance.PlaySFX(SoundManager.Instance.reload);

        yield return new WaitForSeconds(reloadTime);

        int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
        EquipWeapon(nextIndex); // 여기서도 GetFinalMaxAmmo가 호출되므로 탄약 갱신됨

        isReloading = false;
    }

    public void SetWeaponVisible(bool isVisible)
    {
        if (currentWeapon != null && currentWeapon.weaponParticle != null)
        {
            foreach (Transform child in transform)
            {
                child.gameObject.SetActive(isVisible);
            }
        }
    }
}