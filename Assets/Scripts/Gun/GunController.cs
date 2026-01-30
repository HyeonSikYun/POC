using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[System.Serializable]
public class WeaponStats
{
    public string weaponName; // "Rifle", "Bazooka", "Flamethrower" (강화 시 이름 매칭용)
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
    public Transform spawn;       // 총구
    public Transform shellPoint;  // 탄피 배출구
    public float reloadTime = 2f;

    private PlayerController playerController;
    private Coroutine shootCoroutine;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    // --- 강화 함수들 (GameManager에서 호출) ---
    public bool UpgradeWeaponDamage(string name, int amount)
    {
        WeaponStats wp = weapons.Find(w => w.weaponName == name);
        if (wp != null)
        {
            wp.damage += amount;
            return true;
        }
        return false;
    }

    public bool UpgradeWeaponAmmo(string name, int amount)
    {
        WeaponStats wp = weapons.Find(w => w.weaponName == name);
        if (wp != null)
        {
            wp.maxAmmo += amount;

            // 만약 현재 들고 있는 무기라면, UI 갱신
            if (currentWeapon.weaponName == name)
            {
                if (UIManager.Instance != null)
                    UIManager.Instance.UpdateAmmo(currentAmmo, currentWeapon.maxAmmo);
            }
            return true;
        }
        return false;
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
        currentAmmo = currentWeapon.maxAmmo;

        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.gameObject.SetActive(true);
            currentWeapon.weaponParticle.Stop();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);
            UIManager.Instance.UpdateAmmo(currentAmmo, currentWeapon.maxAmmo);
            UIManager.Instance.ShowReloading(false);
        }

        Debug.Log($"무기 장착: {currentWeapon.weaponName}");
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (!playerController.hasGun || isReloading) return;

        if (currentAmmo <= 0)
        {
            if (context.started) StartCoroutine(ReloadAndSwitch());
            return;
        }

        if (context.started)
        {
            isHoldingTrigger = true;
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
            UIManager.Instance.UpdateAmmo(currentAmmo, currentWeapon.maxAmmo);
        }

        // 1. [핵심] 마우스가 가리키는 정확한 월드 좌표 계산 (총구 높이 기준)
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        // 총구(spawn)의 높이에 맞는 가상의 평면 생성
        Plane gunPlane = new Plane(Vector3.up, spawn.position);
        float distance;
        Vector3 targetPoint = Vector3.zero;

        if (gunPlane.Raycast(ray, out distance))
        {
            targetPoint = ray.GetPoint(distance);
        }

        // 2. 발사 방향 벡터 계산 (목표지점 - 총구위치)
        Vector3 fireDirection = (targetPoint - spawn.position).normalized;

        // (옵션) 높이차 무시하고 수평으로만 쏘려면 아래 주석 해제
        // fireDirection.y = 0; fireDirection.Normalize();


        if (currentWeapon.useProjectile)
        {
            // [수정] 총구 회전을 발사 방향으로 잠시 맞춤 (로켓 등이 엉뚱하게 나가는 것 방지)
            Quaternion fireRotation = Quaternion.LookRotation(fireDirection);

            GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, spawn.position, fireRotation);
            if (projectileObj != null)
            {
                Projectile proj = projectileObj.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.damage = currentWeapon.damage;
                    // [수정] spawn.forward 대신 계산된 fireDirection 전달
                    proj.Launch(fireDirection);
                }
            }
        }
        else
        {
            // [수정] Raycast 발사 시 계산된 방향 전달
            FireRaycast(fireDirection);
        }

        if (currentWeapon.ejectShell) SpawnShell();

        if (currentAmmo <= 0)
        {
            StartCoroutine(ReloadAndSwitch());
        }
    }

    // [수정] 매개변수로 direction을 받도록 변경
    private void FireRaycast(Vector3 direction)
    {
        // [수정] spawn.forward 대신 전달받은 direction 사용
        Ray ray = new Ray(spawn.position, direction);
        RaycastHit hit;
        Vector3 endPoint;

        if (Physics.Raycast(ray, out hit, currentWeapon.range))
        {
            endPoint = hit.point;

            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null) zombie.TakeDamage(currentWeapon.damage);
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

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowReloading(true);
        }

        yield return new WaitForSeconds(reloadTime);

        int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
        EquipWeapon(nextIndex);

        isReloading = false;
    }
}