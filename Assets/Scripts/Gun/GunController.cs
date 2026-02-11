using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Linq; // [추가] 리스트 정렬을 위해 필요

public enum WeaponType
{
    Rifle,
    Bazooka,
    FlameThrower,
    Shotgun, // [추가]
    Sniper   // [추가]
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

    [Header("샷건 설정 (Shotgun Only)")]
    public int pellets = 6;         // 한 번에 나가는 총알 수
    public float spreadAngle = 15f; // 부채꼴 각도

    [Header("저격총 설정 (Sniper Only)")]
    public int maxPenetration = 3; // 최대 관통 인원 수

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
    private int[] weaponAmmoList;
    private bool[] isWeaponUnlocked;
    private int nextUnlockIndex = 2; //

    [Header("상태")]
    //private int currentAmmo;
    private bool isReloading = false;
    private bool isHoldingTrigger = false;

    [Header("필수 할당")]
    public Transform spawn;
    public Transform shellPoint;
    public float reloadTime = 3f;

    private PlayerController playerController;
    private Coroutine shootCoroutine;
    private float lastFireTime;

    [Header("오디오 소스 연결")]
    public AudioSource gunAudioSource;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();

        // [수정] 데이터 초기화 및 1, 2번 무기 해금
        int count = weapons.Count;
        weaponAmmoList = new int[count];
        isWeaponUnlocked = new bool[count];

        for (int i = 0; i < count; i++)
        {
            // 탄약 꽉 채우기 & 일단 다 잠금
            weaponAmmoList[i] = GetFinalMaxAmmo(weapons[i]);
            isWeaponUnlocked[i] = false;
        }

        // 1번(Index 0), 2번(Index 1)만 해제
        if (count >= 1) isWeaponUnlocked[0] = true;
        if (count >= 2) isWeaponUnlocked[1] = true;

        nextUnlockIndex = 2; // 다음 해금될 무기 번호

        if (weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;
        if (isReloading) return;

        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        // 잠금 해제된 무기만 교체 가능
        if (keyboard.digit1Key.wasPressedThisFrame) TrySwitchWeapon(0);
        if (keyboard.digit2Key.wasPressedThisFrame) TrySwitchWeapon(1);
        if (keyboard.digit3Key.wasPressedThisFrame) TrySwitchWeapon(2);
        if (keyboard.digit4Key.wasPressedThisFrame) TrySwitchWeapon(3);
        if (keyboard.digit5Key.wasPressedThisFrame) TrySwitchWeapon(4);
    }

    private int GetFinalDamage()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalDamageMultiplier : 1.0f;
        return Mathf.RoundToInt(currentWeapon.damage * multiplier);
    }

    private int GetFinalMaxAmmo()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalAmmoMultiplier : 1.0f;
        return Mathf.RoundToInt(currentWeapon.maxAmmo * multiplier);
    }

    public void RefreshAmmoUI()
    {
        if (UIManager.Instance != null && currentWeapon != null)
        {
            int current = weaponAmmoList[currentWeaponIndex];
            UIManager.Instance.UpdateAmmo(current, GetFinalMaxAmmo());
        }
    }

    private void EquipWeapon(int index)
    {
        if (gunAudioSource != null)
        {
            gunAudioSource.Stop();
            gunAudioSource.loop = false;
        }

        if (currentWeapon != null && currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
            currentWeapon.weaponParticle.gameObject.SetActive(false);
        }

        currentWeaponIndex = index;
        currentWeapon = weapons[currentWeaponIndex];

        lastFireTime = -currentWeapon.fireRate;

        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.gameObject.SetActive(true);
            currentWeapon.weaponParticle.Stop();
        }

        //if (UIManager.Instance != null)
        //{
        //    UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);
        //    UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        //    UIManager.Instance.ShowReloading(false);
        //}

        RefreshUI(); // [수정] UI 갱신 함수 호출로 변경

        Debug.Log($"무기 장착: {currentWeapon.weaponName}");
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (GameManager.Instance != null && (GameManager.Instance.isUpgradeMenuOpen || GameManager.Instance.isPaused)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!playerController.hasGun || isReloading) return;

        if (weaponAmmoList[currentWeaponIndex] <= 0)
        {
            return;
        }

        if (context.started)
        {
            isHoldingTrigger = true;

            // 화염방사기 사운드 루프 처리
            if (currentWeapon.type == WeaponType.FlameThrower)
            {
                if (SoundManager.Instance != null)
                {
                    gunAudioSource.clip = SoundManager.Instance.flameThrower;
                    gunAudioSource.loop = true;
                    gunAudioSource.Play();
                }
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
                if (Time.time >= lastFireTime + currentWeapon.fireRate)
                {
                    Shoot();
                    lastFireTime = Time.time; // 발사 시간 갱신
                }
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
        while (isHoldingTrigger && weaponAmmoList[currentWeaponIndex] > 0 && !isReloading)
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
        weaponAmmoList[currentWeaponIndex]--;

        //if (UIManager.Instance != null)
        //{
        //    UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        //}

        RefreshUI();

        // --- 발사 방향 계산 ---
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane gunPlane = new Plane(Vector3.up, spawn.position);
        float distance;
        Vector3 targetPoint = Vector3.zero;

        if (gunPlane.Raycast(ray, out distance))
        {
            targetPoint = ray.GetPoint(distance);
        }

        Vector3 baseDirection;
        float distanceToMouse = Vector3.Distance(transform.position, targetPoint);
        float deadZoneRadius = 2.0f;

        if (distanceToMouse < deadZoneRadius)
        {
            baseDirection = spawn.forward;
        }
        else
        {
            baseDirection = (targetPoint - spawn.position).normalized;
        }
        baseDirection.y = 0;
        baseDirection.Normalize();

        // --- 무기 타입별 로직 분기 ---
        if (currentWeapon.useProjectile) // 바주카 등
        {
            FireProjectile(baseDirection);
        }
        else
        {
            switch (currentWeapon.type)
            {
                case WeaponType.Shotgun:
                    FireShotgun(baseDirection);
                    break;
                case WeaponType.Sniper:
                    FireSniper(baseDirection);
                    break;
                case WeaponType.Rifle:
                default:
                    FireRaycast(baseDirection); // 기존 일반 발사
                    if (currentWeapon.type == WeaponType.Rifle && SoundManager.Instance != null)
                    {
                        SoundManager.Instance.PlaySFX(SoundManager.Instance.Rifle, 0.1f);
                    }
                    break;
            }
        }

        if (currentWeapon.ejectShell) SpawnShell();

        if (weaponAmmoList[currentWeaponIndex] <= 0)
        {
            HandleWeaponDepleted(); // [신규] 함수 호출
        }
    }

    // [기존] 발사체 발사 로직 분리
    private void FireProjectile(Vector3 direction)
    {
        Quaternion fireRotation = Quaternion.LookRotation(direction);
        GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, spawn.position, fireRotation);

        if (SoundManager.Instance != null)
            SoundManager.Instance.PlaySFX(SoundManager.Instance.Bazooka, 0.1f);

        if (projectileObj != null)
        {
            Projectile proj = projectileObj.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.damage = GetFinalDamage();
                proj.Launch(direction);
            }
        }
    }

    // [신규] 샷건 발사 로직
    private void FireShotgun(Vector3 baseDirection)
    {
        // SoundManager에 Shotgun 클립이 있다고 가정하고 없으면 Rifle 소리라도 냄
        if (SoundManager.Instance != null)
        {
            // SoundManager.Instance.Shotgun 이 있다면 교체하세요. 임시로 Rifle 사용 혹은 null 체크
            SoundManager.Instance.PlaySFX(SoundManager.Instance.shotGun, 0.2f);
        }

        for (int i = 0; i < currentWeapon.pellets; i++)
        {
            // -spreadAngle/2 ~ +spreadAngle/2 사이의 랜덤 각도 생성
            float randomAngle = Random.Range(-currentWeapon.spreadAngle / 2f, currentWeapon.spreadAngle / 2f);

            // Y축 기준 회전 쿼터니언 생성
            Quaternion spreadRotation = Quaternion.Euler(0, randomAngle, 0);

            // 기준 방향을 회전시켜 최종 방향 산출
            Vector3 pelletDirection = spreadRotation * baseDirection;

            // 기존 FireRaycast 재사용 (각 펠릿마다 트레이서 생성됨)
            FireRaycast(pelletDirection);
        }
    }

    // [신규] 저격총 관통 발사 로직
    private void FireSniper(Vector3 direction)
    {
        if (SoundManager.Instance != null)
        {
            // SoundManager.Instance.Sniper 가 있다면 교체하세요.
            SoundManager.Instance.PlaySFX(SoundManager.Instance.sniperShot, 0.3f);
        }

        // RaycastAll로 경로상의 모든 물체 검출
        RaycastHit[] hits = Physics.RaycastAll(spawn.position, direction, currentWeapon.range);

        // 거리순 정렬 (가까운 순서대로 맞아야 함)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int hitCount = 0;
        Vector3 finalEndPoint = spawn.position + (direction * currentWeapon.range); // 기본적으로 최대 사거리까지

        foreach (RaycastHit hit in hits)
        {
            // 자기 자신 충돌 방지 (혹시 모를)
            if (hit.collider.gameObject == gameObject) continue;

            // 벽(Environment)에 맞으면 거기서 관통 멈춤
            if (!hit.collider.CompareTag("Enemy") && !hit.collider.isTrigger)
            {
                // 적이 아닌데 Trigger가 아닌(벽 등) 물체에 닿으면 멈춤
                finalEndPoint = hit.point;
                EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);
                break;
            }

            if (hit.collider.CompareTag("Enemy"))
            {
                ZombieAI zombie = hit.collider.GetComponent<ZombieAI>();
                if (zombie != null)
                {
                    zombie.TakeDamage(GetFinalDamage());
                    EffectManager.Instance.PlayHitEffect(hit.point, hit.normal);

                    hitCount++;
                    // 최대 관통 수 도달 시 멈춤
                    if (hitCount >= currentWeapon.maxPenetration)
                    {
                        finalEndPoint = hit.point; // 시각적 효과는 여기까지
                        break;
                    }
                }
            }
        }

        // 저격총은 관통하므로 트레이서를 맨 마지막 지점까지 한 번만 그림
        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(spawn.position, finalEndPoint, 0.05f, currentWeapon.tracerColor, 0.1f);
        }
    }

    // [기존] 일반 단발(라이플) 발사 로직
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

    //private IEnumerator ReloadAndSwitch()
    //{
    //    if (isReloading) yield break;
    //    isReloading = true;

    //    if (shootCoroutine != null) StopCoroutine(shootCoroutine);
    //    if (currentWeapon.weaponParticle != null) currentWeapon.weaponParticle.Stop();

    //    if (gunAudioSource.isPlaying && currentWeapon.type == WeaponType.FlameThrower)
    //    {
    //        gunAudioSource.Stop();
    //        gunAudioSource.loop = false;
    //    }

    //    if (UIManager.Instance != null)
    //    {
    //        UIManager.Instance.ShowReloading(true);
    //    }
    //    if (SoundManager.Instance != null)
    //        SoundManager.Instance.PlaySFX(SoundManager.Instance.reload);

    //    yield return new WaitForSeconds(reloadTime);

    //    int nextIndex = (currentWeaponIndex + 1) % weapons.Count;
    //    EquipWeapon(nextIndex);

    //    isReloading = false;
    //}

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

    // [신규] 무기 교체 시도 (잠겨있거나 탄약 없으면 실패)
    private void TrySwitchWeapon(int index)
    {
        if (index < 0 || index >= weapons.Count) return;
        if (!isWeaponUnlocked[index]) return; // 잠겨있음
        if (weaponAmmoList[index] <= 0) return; // 탄약 없음
        if (index == currentWeaponIndex) return;

        EquipWeapon(index);
    }

    // [신규] 탄약 소진 시 다음 무기 해금 및 교체 로직
    private void HandleWeaponDepleted()
    {
        // 1. [수정] 다 쓴 무기의 이펙트와 소리 즉시 끄기
        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
        }
        if (gunAudioSource != null)
        {
            gunAudioSource.Stop();
            gunAudioSource.loop = false;
        }

        // 발사 상태 강제 해제
        isHoldingTrigger = false;
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }

        Debug.Log($"{currentWeapon.weaponName} 탄약 소진! 무기를 잠급니다.");

        // 2. 현재 무기 잠금
        isWeaponUnlocked[currentWeaponIndex] = false;

        // 3. [핵심 수정] "이미 열려있는 무기"는 건너뛰고, "잠겨있는 다음 무기"를 찾습니다.
        // 이렇게 해야 무기 개수가 줄어들지 않고 계속 2개씩 유지됩니다.
        int unlockTargetIndex = nextUnlockIndex;
        int safetyCount = 0; // 무한루프 방지용

        // 잠겨있는 무기가 나올 때까지 인덱스를 계속 넘김
        while (isWeaponUnlocked[unlockTargetIndex] && safetyCount < weapons.Count)
        {
            unlockTargetIndex = (unlockTargetIndex + 1) % weapons.Count;
            safetyCount++;
        }

        // 4. 찾은 무기 해제 및 탄약 리필
        isWeaponUnlocked[unlockTargetIndex] = true;
        weaponAmmoList[unlockTargetIndex] = GetFinalMaxAmmo(weapons[unlockTargetIndex]);
        Debug.Log($"새로운 무기 해제: {weapons[unlockTargetIndex].weaponName}");

        // 다음 해금 순서는 이번에 연 것의 다음 번호로 설정
        nextUnlockIndex = (unlockTargetIndex + 1) % weapons.Count;

        // 5. 사용 가능한(열려있고 탄약 있는) 무기로 자동 교체
        int switchTargetIndex = -1;
        for (int i = 0; i < weapons.Count; i++)
        {
            if (isWeaponUnlocked[i] && weaponAmmoList[i] > 0)
            {
                switchTargetIndex = i;
                break;
            }
        }

        if (switchTargetIndex != -1)
        {
            StartCoroutine(AutoSwitchRoutine(switchTargetIndex));
        }
        else
        {
            // 만약 정말 쏠 게 없다면 1번 강제 지급 (비상용)
            Debug.Log("사용 가능한 무기 없음. 1번 강제 보급");
            isWeaponUnlocked[0] = true;
            weaponAmmoList[0] = GetFinalMaxAmmo(weapons[0]);
            EquipWeapon(0);
        }
    }

    // [신규] 자동 교체 딜레이
    private IEnumerator AutoSwitchRoutine(int targetIndex)
    {
        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        isHoldingTrigger = false;

        if (UIManager.Instance != null) UIManager.Instance.ShowReloading(true);
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.Instance.reload);

        yield return new WaitForSeconds(1.0f); // 교체 시간 (reloadTime보다 짧게)

        EquipWeapon(targetIndex);

        if (UIManager.Instance != null) UIManager.Instance.ShowReloading(false);
    }

    // [신규] UI 갱신 헬퍼
    private void RefreshUI()
    {
        if (UIManager.Instance != null)
        {
            int current = weaponAmmoList[currentWeaponIndex];
            int max = GetFinalMaxAmmo(weapons[currentWeaponIndex]);

            UIManager.Instance.UpdateAmmo(current, max);
            UIManager.Instance.UpdateWeaponName(currentWeapon.weaponName);

            // 슬롯 UI가 있다면 여기서 갱신 (UIManager에 UpdateWeaponSlots 함수 필요)
            UIManager.Instance.UpdateWeaponSlots(isWeaponUnlocked, currentWeaponIndex);
        }
    }

    // [신규] 인자 받는 GetFinalMaxAmmo 오버로딩
    private int GetFinalMaxAmmo(WeaponStats weapon)
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalAmmoMultiplier : 1.0f;
        return Mathf.RoundToInt(weapon.maxAmmo * multiplier);
    }
}