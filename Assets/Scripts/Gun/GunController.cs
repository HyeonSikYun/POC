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

    [Header("모델 및 발사 위치 연결 (필수)")]
    public GameObject weaponModel; // 1. 이 무기의 3D 모델 (켜고 끌 대상)
    public Transform muzzlePoint;  // 2. 이 무기의 총구 위치 (총알 나가는 곳)
    public Transform shellEjectPoint;

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

    [Header("머즐 이펙트 (Muzzle Flash)")]
    public bool useMuzzleFlash = true;      // 이펙트 사용 여부
    public string muzzleFlashTag = "MuzzleFlash_Rifle";
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
    private bool isSwitching = false;

    [Header("필수 할당")]
    //public Transform spawn;
    //public Transform shellPoint;
    public float reloadTime = 3f;
    private Transform currentMuzzlePoint;

    public PlayerController playerController;
    private Coroutine shootCoroutine;
    private float lastFireTime;

    [Header("오디오 소스 연결")]
    public AudioSource gunAudioSource;

    private void Start()
    {
        playerController = GetComponent<PlayerController>();

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

        if (playerController != null && playerController.hasGun)
        {
            if (weapons.Count > 0)
            {
                EquipWeapon(0);
            }
        }
        else
        {
            // 총이 없다면(튜토리얼 등) -> 모든 모델 숨기기
            HideAllWeapons();
        }
    }

    private void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;
        if (isReloading || isSwitching) return;

        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scrollY = mouse.scroll.ReadValue().y;

            if (scrollY > 0) // 휠 올림 (다음 무기)
            {
                SwitchToNextWeapon();
            }
            else if (scrollY < 0) // 휠 내림 (이전 무기)
            {
                SwitchToPreviousWeapon();
            }
        }

        //var keyboard = Keyboard.current;
        //if (keyboard == null) return;

        //// 잠금 해제된 무기만 교체 가능
        //if (keyboard.digit1Key.wasPressedThisFrame) TrySwitchWeapon(0);
        //if (keyboard.digit2Key.wasPressedThisFrame) TrySwitchWeapon(1);
        //if (keyboard.digit3Key.wasPressedThisFrame) TrySwitchWeapon(2);
        //if (keyboard.digit4Key.wasPressedThisFrame) TrySwitchWeapon(3);
        //if (keyboard.digit5Key.wasPressedThisFrame) TrySwitchWeapon(4);
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

        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i].weaponModel != null)
            {
                if (i == index)
                {
                    weapons[i].weaponModel.SetActive(true); // 선택된 것만 켜기
                }
                else
                {
                    weapons[i].weaponModel.SetActive(false); // 나머지는 끄기
                }
            }
        }

        currentWeaponIndex = index;
        currentWeapon = weapons[currentWeaponIndex];

        if (currentWeapon.muzzlePoint != null)
        {
            currentMuzzlePoint = currentWeapon.muzzlePoint;
        }
        else
        {
            Debug.LogError($"{currentWeapon.weaponName}에 Muzzle Point가 연결되지 않았습니다!");
            currentMuzzlePoint = transform; // 비상시 내 위치 사용
        }

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
        Debug.Log($"[실행 중] 현재 이 코드는 '{gameObject.name}' 오브젝트에서 실행되고 있습니다.");

        if (playerController == null)
        {
            Debug.LogError($"🚨 [검거 완료] 범인은 바로 '{gameObject.name}' 입니다! 이 오브젝트에 붙은 GunController를 삭제하세요!");
            return; // 더 이상 실행하지 않고 멈춤
        }

        if (GameManager.Instance != null && (GameManager.Instance.isUpgradeMenuOpen || GameManager.Instance.isPaused)) return;
        //if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        if (!playerController.hasGun || isReloading || isSwitching) return;

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

    private void PlayMuzzleFlash()
    {
        if (!currentWeapon.useMuzzleFlash) return;
        if (string.IsNullOrEmpty(currentWeapon.muzzleFlashTag)) return;

        // [수정] 회전값 보정: 총구 회전값 * 90도 회전 (Y축 기준)
        // 만약 반대로 나가면 -90 으로 바꿔보세요.
        Quaternion fixRotation = currentMuzzlePoint.rotation * Quaternion.Euler(0, -90, 0);

        // 수정된 회전값(fixRotation)으로 소환
        GameObject flash = PoolManager.Instance.SpawnFromPool(
            currentWeapon.muzzleFlashTag,
            currentMuzzlePoint.position,
            fixRotation
        );

        if (flash != null)
        {
            StartCoroutine(ReturnMuzzleFlash(flash, 0.1f));
        }
    }

    private IEnumerator ReturnMuzzleFlash(GameObject flashObj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (flashObj.activeInHierarchy)
        {
            PoolManager.Instance.ReturnToPool(currentWeapon.muzzleFlashTag, flashObj);
        }
    }

    private void Shoot()
    {
        weaponAmmoList[currentWeaponIndex]--;

        //if (UIManager.Instance != null)
        //{
        //    UIManager.Instance.UpdateAmmo(currentAmmo, GetFinalMaxAmmo());
        //}

        RefreshUI();
        PlayMuzzleFlash();

        // --- 발사 방향 계산 ---
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        Plane gunPlane = new Plane(Vector3.up, currentMuzzlePoint.position);
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
            baseDirection = currentMuzzlePoint.forward;
        }
        else
        {
            baseDirection = (targetPoint - currentMuzzlePoint.position).normalized;
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
        GameObject projectileObj = PoolManager.Instance.SpawnFromPool(currentWeapon.projectilePoolTag, currentMuzzlePoint.position, fireRotation);

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
        RaycastHit[] hits = Physics.RaycastAll(currentMuzzlePoint.position, direction, currentWeapon.range);

        // 거리순 정렬 (가까운 순서대로 맞아야 함)
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        int hitCount = 0;
        Vector3 finalEndPoint = currentMuzzlePoint.position + (direction * currentWeapon.range); // 기본적으로 최대 사거리까지

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
            EffectManager.Instance.SpawnTracer(currentMuzzlePoint.position, finalEndPoint, 0.05f, currentWeapon.tracerColor, 0.1f);
        }
    }

    // [기존] 일반 단발(라이플) 발사 로직
    private void FireRaycast(Vector3 direction)
    {
        Ray ray = new Ray(currentMuzzlePoint.position, direction);
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
            endPoint = currentMuzzlePoint.position + (direction * currentWeapon.range);
        }

        if (currentWeapon.useTracer)
        {
            EffectManager.Instance.SpawnTracer(currentMuzzlePoint.position, endPoint, 0.05f, currentWeapon.tracerColor, 0.05f);
        }
    }

    private void SpawnShell()
    {
        // 1. 현재 무기에 탄피 배출구가 설정되어 있는지 확인
        if (currentWeapon.shellEjectPoint == null) return;

        // 2. 탄피 생성 (위치는 무기별 shellEjectPoint 사용)
        GameObject shell = PoolManager.Instance.SpawnFromPool("Shell", currentWeapon.shellEjectPoint.position, currentWeapon.shellEjectPoint.rotation);

        if (shell != null)
        {
            Rigidbody rb = shell.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;

                // 배출구의 오른쪽(Right) + 위쪽(Up) 방향으로 튕겨 나감
                Vector3 ejectDir = currentWeapon.shellEjectPoint.right + Vector3.up * 0.5f;

                // 랜덤성 추가 (더 자연스럽게)
                ejectDir += Random.insideUnitSphere * 0.2f;

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
    // [수정] 탄약 소진 시 로직 (순서대로 해금 및 즉시 교체)
    private void HandleWeaponDepleted()
    {
        // 1. 다 쓴 무기 정리 (이펙트, 소리 끄기)
        if (currentWeapon.weaponParticle != null)
        {
            currentWeapon.weaponParticle.Stop();
        }
        if (gunAudioSource != null)
        {
            gunAudioSource.Stop();
            gunAudioSource.loop = false;
        }
        isHoldingTrigger = false;
        if (shootCoroutine != null)
        {
            StopCoroutine(shootCoroutine);
            shootCoroutine = null;
        }

        Debug.Log($"{currentWeapon.weaponName} 탄약 소진! 무기를 잠급니다.");

        // 2. 현재 무기 잠금 (확실하게 잠금)
        isWeaponUnlocked[currentWeaponIndex] = false;

        // 3. [핵심] 다음 해금할 무기 가져오기
        // nextUnlockIndex는 Start()에서 이미 2로 설정되어 있고, 
        // 무기가 바뀔 때마다 계속 다음 순번을 가리키고 있습니다.
        int unlockTargetIndex = nextUnlockIndex;

        // 방어 코드: 만약 해금하려는 게 이미 열려있다면(꼬임 방지), 
        // 닫혀있는 걸 찾을 때까지 뒤로 넘어감
        int safetyCount = 0;
        while (isWeaponUnlocked[unlockTargetIndex] && safetyCount < weapons.Count)
        {
            unlockTargetIndex = (unlockTargetIndex + 1) % weapons.Count;
            safetyCount++;
        }

        // 4. 새 무기 해금 및 탄약 충전
        isWeaponUnlocked[unlockTargetIndex] = true;
        weaponAmmoList[unlockTargetIndex] = GetFinalMaxAmmo(weapons[unlockTargetIndex]);
        Debug.Log($"새로운 무기 해제: {weapons[unlockTargetIndex].weaponName}");

        // 5. [중요] 다음 해금 순서 미리 갱신해두기
        // 이번에 unlockTargetIndex를 열었으니, 그 다음 번호부터 검사해서 잠긴 걸 찾음
        int tempNextIndex = (unlockTargetIndex + 1) % weapons.Count;
        safetyCount = 0;
        // 잠겨있는 무기가 나올 때까지 계속 다음으로 넘김
        while (isWeaponUnlocked[tempNextIndex] && safetyCount < weapons.Count)
        {
            tempNextIndex = (tempNextIndex + 1) % weapons.Count;
            safetyCount++;
        }
        nextUnlockIndex = tempNextIndex; // 찾은 값을 저장

        // 6. [해결책] "새로 해금된 무기"로 즉시 교체!
        // 예전에는 '사용 가능한 아무거나'를 찾았지만, 이제는 unlockTargetIndex로 바로 바꿉니다.
        StartCoroutine(AutoSwitchRoutine(unlockTargetIndex));
    }

    // [신규] 자동 교체 딜레이
    private IEnumerator AutoSwitchRoutine(int targetIndex)
    {
        isSwitching = true;

        if (shootCoroutine != null) StopCoroutine(shootCoroutine);
        isHoldingTrigger = false;

        if (playerController != null)
        {
            playerController.PlayWeaponChangeAnim();
        }

        if (UIManager.Instance != null) UIManager.Instance.ShowReloading(true);
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySFX(SoundManager.Instance.reload);

        yield return new WaitForSeconds(3.5f); // 교체 시간 (reloadTime보다 짧게)

        EquipWeapon(targetIndex);

        if (UIManager.Instance != null) UIManager.Instance.ShowReloading(false);

        isSwitching = false;
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

    // [신규] 모든 무기 모델을 강제로 끄는 함수 (맨손 상태)
    public void HideAllWeapons()
    {
        if (weapons == null) return;

        foreach (var weapon in weapons)
        {
            if (weapon.weaponModel != null)
            {
                weapon.weaponModel.SetActive(false);
            }
            if (weapon.weaponParticle != null)
            {
                weapon.weaponParticle.gameObject.SetActive(false);
            }
        }

        // 현재 무기 정보도 초기화 (안 하면 쏠 수 있음)
        //currentWeaponIndex = -1; // 인덱스는 놔두더라도
        currentWeapon = null;    // 무기 데이터는 비워야 안전함
    }

    // [신규] 외부(PlayerController)에서 총 먹었을 때 호출할 함수
    public void EquipStartingWeapon()
    {
        // 0번(기본 무기) 장착
        if (weapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    // [신규] 다음 무기로 교체 (휠 올림)
    private void SwitchToNextWeapon()
    {
        int nextIndex = currentWeaponIndex;
        // 최대 무기 개수만큼 반복하며 찾음
        for (int i = 0; i < weapons.Count; i++)
        {
            nextIndex = (nextIndex + 1) % weapons.Count; // 인덱스 증가 및 순환 (0->1->2->0)

            // 해금되었고 & 탄약이 있고 & 현재 무기가 아니라면 교체
            if (isWeaponUnlocked[nextIndex] && weaponAmmoList[nextIndex] > 0 && nextIndex != currentWeaponIndex)
            {
                TrySwitchWeapon(nextIndex);
                return;
            }
        }
    }

    // [신규] 이전 무기로 교체 (휠 내림)
    private void SwitchToPreviousWeapon()
    {
        int prevIndex = currentWeaponIndex;
        // 최대 무기 개수만큼 반복하며 찾음
        for (int i = 0; i < weapons.Count; i++)
        {
            prevIndex--;
            if (prevIndex < 0) prevIndex = weapons.Count - 1; // 인덱스 감소 및 순환 (0->2->1->0)

            // 해금되었고 & 탄약이 있고 & 현재 무기가 아니라면 교체
            if (isWeaponUnlocked[prevIndex] && weaponAmmoList[prevIndex] > 0 && prevIndex != currentWeaponIndex)
            {
                TrySwitchWeapon(prevIndex);
                return;
            }
        }
    }
}