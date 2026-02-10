using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using FIMSpace.FProceduralAnimation;

public interface IZombieState
{
    void Enter(ZombieAI zombie);
    void Execute(ZombieAI zombie);
    void Exit(ZombieAI zombie);
}

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZombieAI : MonoBehaviour, IPooledObject
{
    // [추가] 좀비 타입 정의
    public enum ZombieType { Normal, Explosive }

    [Header("좀비 타입 설정")]
    public ZombieType zombieType = ZombieType.Normal;

    [Header("타겟 설정")]
    public Transform player;

    [Header("AI 설정")]
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public float moveSpeed = 3.5f;

    [Header("충돌 방지")]
    public LayerMask zombieLayer;

    [Header("전투 설정")]
    public float attackCooldown = 2f;
    public float attackDelay = 0.5f;
    public int defaultMaxHealth = 100;
    public int maxHealth;
    public int currentHealth;

    [Header("죽음 설정")]
    public float deathAnimationDuration = 3f;

    [Header("폭발 설정 (Explosive 타입 전용)")]
    public float explosionRange = 3.0f; // 폭발 범위
    public int explosionDamage = 50;    // 플레이어에게 줄 데미지
    public GameObject explosionEffect;  // 폭발 이펙트 프리팹
    public GameObject rangeIndicatorPrefab;

    [Header("피격 플래시 설정")]
    public Renderer meshRenderer;
    public Color damageColor = Color.red;
    private Color originColor;
    public Material flashMaterial;
    private Material originalMaterial;

    [Header("드랍 아이템")]
    public GameObject bioSamplePrefab;
    public LayerMask groundLayer;

    [Header("사운드 설정")]
    public AudioSource audioSource;

    [Header("디버그")]
    public bool showGizmos = true;

    public bool isDead = false;

    public NavMeshAgent Agent { get; private set; }
    public Animator Anim { get; private set; }
    public Collider Col { get; private set; }
    public float LastAttackTime { get; set; }

    private IZombieState currentState;

    public readonly int hashIsRun = Animator.StringToHash("isRun");
    // [추가] 기어가기 애니메이션 해시
    public readonly int hashIsCrawling = Animator.StringToHash("isCrawling");
    public readonly int hashAtk = Animator.StringToHash("zombie1Atk");
    public readonly int hashDie = Animator.StringToHash("zombie1Die");
    public readonly int hashFrontDie = Animator.StringToHash("zombieFrontDie");
    public readonly int hashDie3 = Animator.StringToHash("die3");
    public readonly int hashDie4 = Animator.StringToHash("die4");
    public readonly int hashDie5 = Animator.StringToHash("die5");

    private void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Anim = GetComponent<Animator>();
        Col = GetComponent<Collider>();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (Agent != null) Agent.enabled = false;
    }

    private void Start()
    {
        meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        originalMaterial = meshRenderer.material;
        HideMyself();
        FindPlayer();
    }

    private void Update()
    {
        if (currentState != null)
        {
            currentState.Execute(this);
        }
    }

    private void HideMyself()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (var r in renderers) r.enabled = false;
    }

    public bool IsBlockedByZombie()
    {
        Vector3 origin = transform.position + Vector3.up * 1.0f;
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, 1.5f, zombieLayer))
        {
            if (hit.collider.gameObject != gameObject)
            {
                return true;
            }
        }
        return false;
    }

    public void ChangeState(IZombieState newState)
    {
        if (currentState != null)
        {
            currentState.Exit(this);
        }

        currentState = newState;
        currentState.Enter(this);
    }

    public void OnObjectSpawn()
    {
        HideMyself();
        float multiplier = 1.0f;
        if (GameManager.Instance != null)
        {
            multiplier = GameManager.Instance.GetZombieHP_Multiplier();
        }

        maxHealth = Mathf.RoundToInt(defaultMaxHealth * multiplier);
        currentHealth = maxHealth;
        LastAttackTime = -attackCooldown;
        isDead = false;

        if (meshRenderer != null) originColor = meshRenderer.material.color;

        Anim.SetLayerWeight(1, 1f);

        if (Col != null)
        {
            Col.enabled = true;
            Col.isTrigger = false;
        }

        if (Anim != null)
        {
            Anim.Rebind();

            // [핵심 수정] 타입에 따라 초기화할 파라미터를 철저히 분리
            if (zombieType == ZombieType.Explosive)
            {
                // 폭발 좀비: isCrawling만 끔 (isRun 건드리면 에러남)
                Anim.SetBool(hashIsCrawling, false);
            }
            else
            {
                // 일반 좀비: isRun만 끔 (isCrawling 건드리면 에러남)
                Anim.SetBool(hashIsRun, false);
            }
        }

        FindPlayer();
        currentState = null;
    }

    public void Initialize(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;

        if (Agent != null)
        {
            Agent.enabled = true;
            // 폭발 좀비는 좀 더 가까이 붙어야 할 수도 있음
            Agent.stoppingDistance = (zombieType == ZombieType.Explosive) ? 0.5f : attackRange - 0.5f;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
            {
                Agent.Warp(hit.position);
                Agent.speed = moveSpeed;
                ChangeState(new IdleState());
            }
            else
            {
                Agent.enabled = false;
            }
        }
    }

    public void TakeDamage(int damage)
    {
        if (isDead || currentState is DeadState) return;

        if (player != null)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null && pc.isSafeZone) return;
        }

        if (meshRenderer != null)
        {
            StopCoroutine("HitFlashRoutine");
            StartCoroutine("HitFlashRoutine");
        }

        SoundManager.Instance.PlaySFX(SoundManager.Instance.gunHit);

        currentHealth -= damage;
        GameManager.Instance.ShowDamagePopup(transform.position, damage);

        // [핵심 수정] 맞았을 때 처리
        if (currentHealth <= 0)
        {
            // 폭발 좀비라면 죽는 애니메이션 대신 폭발!
            if (zombieType == ZombieType.Explosive)
            {
                // 이미 죽는 중이면 중복 실행 방지
                if (!isDead)
                {
                    // 즉시 폭발 대신 코루틴 시작
                    StartCoroutine(ExplodeRoutine());
                }
            }
            else
            {
                ChangeState(new DeadState());
            }
        }
        else
        {
            // 맞았는데 안 죽었으면 추적 시작
            // 폭발 좀비도 맞으면 기어서 쫓아옴
            if (currentState is IdleState)
            {
                ChangeState(new ChaseState());
            }
        }
    }

    private IEnumerator ExplodeRoutine()
    {
        isDead = true;

        // 이동 정지
        if (Agent != null && Agent.isOnNavMesh)
        {
            Agent.isStopped = true;
            Agent.velocity = Vector3.zero;
        }

        StopCoroutine("HitFlashRoutine");

        // 1. 범위 표시기(인디케이터) 생성
        GameObject indicator = null;
        if (rangeIndicatorPrefab != null)
        {
            Vector3 spawnPos = transform.position;
            spawnPos.y += 0.2f; // 바닥에 묻히지 않게 살짝 띄움

            indicator = Instantiate(rangeIndicatorPrefab, spawnPos, Quaternion.identity);

            // 크기 설정 (반지름 * 2)
            float size = explosionRange * 2.0f;
            indicator.transform.localScale = new Vector3(size, 0.1f, size);
        }

        // --- 깜빡임 로직 (좀비 몸체 + 인디케이터 동기화) ---
        int blinkCount = 5;
        float blinkSpeed = 0.1f;

        for (int i = 0; i < blinkCount; i++)
        {
            // [상태 1: 켜짐/위험색]
            if (meshRenderer != null) meshRenderer.material = flashMaterial; // 좀비 하얗게
            if (indicator != null) indicator.SetActive(true);                // 인디케이터 보이기

            yield return new WaitForSeconds(blinkSpeed);

            // [상태 2: 꺼짐/원래색]
            if (meshRenderer != null) meshRenderer.material = originalMaterial; // 좀비 원래대로
            if (indicator != null) indicator.SetActive(false);                  // 인디케이터 안 보이기 (깜빡!)

            yield return new WaitForSeconds(blinkSpeed);
        }

        // 폭발 직전 표시기 완전 삭제
        if (indicator != null) Destroy(indicator);

        Explode();
    }
    // [신규] 폭발 함수
    private void Explode()
    {
        isDead = true;

        // 1. 이펙트 생성
        if (explosionEffect != null)
        {
            Vector3 spawnPos = transform.position + Vector3.up * 1.0f;
            Instantiate(explosionEffect, spawnPos, Quaternion.identity);
        }

        // 2. 소리 재생 (SoundManager에 폭발음이 있다면 추가)
        SoundManager.Instance.PlaySFX(SoundManager.Instance.zombieExplosion);

        // 3. 범위 데미지 처리
        Collider[] colliders = Physics.OverlapSphere(transform.position, explosionRange);
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                PlayerController pc = col.GetComponent<PlayerController>();
                if (pc != null)
                {
                    pc.TakeDamage(explosionDamage);
                    Debug.Log("플레이어 폭발 데미지 입음!");
                }
            }
        }

        // 4. 아이템 드랍 여부 (선택 사항: 폭발해서 아이템도 날아갔다고 칠지, 드랍할지)
        // DropItem(); 

        // 5. 즉시 제거 (오브젝트 풀 반환)
        if (Agent != null && Agent.enabled) Agent.enabled = false;
        PoolManager.Instance.ReturnToPool("Zombie", gameObject); // 이름 주의: 풀링 키값 확인 필요
    }

    // [신규] 외부(총)에서 소리 듣고 반응하는 함수
    public void OnHearGunshot(Vector3 playerPos)
    {
        if (isDead) return;

        // Idle 상태였다면 추적 시작
        if (currentState is IdleState)
        {
            ChangeState(new ChaseState());
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        meshRenderer.material = flashMaterial;
        yield return new WaitForSeconds(0.1f);
        meshRenderer.material = originalMaterial;
    }

    public IEnumerator DealDamageWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DealDamageToPlayer();
    }

    public void DealDamageToPlayer()
    {
        if (player == null || isDead) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange + 0.5f)
        {
            PlayerController pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                // 폭발 좀비는 평타 공격 안 함 (자폭이 공격임)
                if (zombieType == ZombieType.Explosive) return;

                pc.TakeDamage(10);
            }
        }
    }

    private void DropItem()
    {
        if (bioSamplePrefab == null) return;

        Vector3 finalSpawnPos = transform.position;

        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            finalSpawnPos = hit.position;
        }
        else
        {
            if (player != null)
            {
                Vector3 dirToPlayer = (player.position - transform.position).normalized;
                finalSpawnPos = transform.position + (dirToPlayer * 1.0f);
            }
        }

        finalSpawnPos.y += 1f;
        Quaternion spawnRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
        Instantiate(bioSamplePrefab, finalSpawnPos, spawnRotation);
    }

    public void Despawn()
    {
        DropItem();
        if (Agent != null && Agent.enabled) Agent.enabled = false;
        PoolManager.Instance.ReturnToPool("Zombie", gameObject);
    }

    private void FindPlayer()
    {
        if (player == null)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) player = p.transform;
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        // 폭발 범위 표시
        if (zombieType == ZombieType.Explosive)
        {
            Gizmos.color = new Color(1, 0.5f, 0, 0.5f); // 주황색
            Gizmos.DrawSphere(transform.position, explosionRange);
        }
    }
}

// ================= 상태 클래스들 =================

public class IdleState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;

        // [수정] 타입에 따라 존재하는 파라미터만 끄기
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        }
        else
        {
            zombie.Anim.SetBool(zombie.hashIsRun, false);
        }
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        if (dist <= zombie.detectionRange)
        {
            PlayerController pc = zombie.player.GetComponent<PlayerController>();
            if (pc != null && pc.isSafeZone) return;

            zombie.ChangeState(new ChaseState());
        }
    }

    public void Exit(ZombieAI zombie) { }
}

public class ChaseState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        // (이동/애니메이션 설정 코드는 기존 유지...)
        if (zombie.Agent.isOnNavMesh)
        {
            zombie.Agent.isStopped = false;
            if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
            {
                zombie.Agent.speed = zombie.moveSpeed * 0.6f;
                zombie.Anim.SetBool(zombie.hashIsCrawling, true);
            }
            else
            {
                zombie.Agent.speed = zombie.moveSpeed;
                zombie.Anim.SetBool(zombie.hashIsRun, true);
            }
        }

        // -------------------------------------------------------
        // [수정] 랜덤 재생 + 볼륨 밸런스 조절
        // -------------------------------------------------------
        if (SoundManager.Instance != null)
        {
            var sm = SoundManager.Instance;
            System.Collections.Generic.List<AudioClip> chaseClips = new System.Collections.Generic.List<AudioClip>();

            // 리스트에 추가
            if (sm.zombieChase != null) chaseClips.Add(sm.zombieChase);  // 0번 (기존, 큰 소리)
            if (sm.zombieChase2 != null) chaseClips.Add(sm.zombieChase2); // 1번 (신규, 작은 소리)
            if (sm.zombieChase3 != null) chaseClips.Add(sm.zombieChase3); // 2번 (신규, 작은 소리)

            if (chaseClips.Count > 0)
            {
                // 랜덤 선택
                int randomIndex = Random.Range(0, chaseClips.Count);
                AudioClip selectedClip = chaseClips[randomIndex];

                zombie.audioSource.clip = selectedClip;
                zombie.audioSource.loop = true;

                // [핵심] 볼륨 밸런스 조절
                // 선택된 게 '기존의 큰 소리(zombieChase)'라면 -> 볼륨을 60%로 줄임
                if (selectedClip == sm.zombieChase)
                {
                    zombie.audioSource.volume = 0.1f; // 필요하면 0.5f로 더 줄이세요
                }
                else
                {
                    // 새로 추가한 작은 소리들이라면 -> 볼륨을 100%로
                    zombie.audioSource.volume = 0.1f;
                }

                zombie.audioSource.Play();
            }
        }
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;
        if (!zombie.Agent.isOnNavMesh) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        PlayerController pc = zombie.player.GetComponent<PlayerController>();
        if (pc != null && pc.isSafeZone)
        {
            zombie.ChangeState(new IdleState());
            return;
        }

        if (zombie.zombieType == ZombieAI.ZombieType.Explosive && dist <= 1.5f)
        {
            zombie.ChangeState(new AttackState());
            return;
        }

        bool isBlocked = zombie.IsBlockedByZombie();
        float checkDist = (zombie.zombieType == ZombieAI.ZombieType.Explosive) ? 1.5f : zombie.attackRange;

        if (dist <= checkDist || (dist < 5.0f && isBlocked))
        {
            zombie.ChangeState(new AttackState());
            return;
        }

        zombie.Agent.SetDestination(zombie.player.position);
    }

    public void Exit(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;

        // [수정] 오류가 발생하던 지점! 타입 확인 후 끄기
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        }
        else
        {
            zombie.Anim.SetBool(zombie.hashIsRun, false);
        }
    }
}

public class AttackState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);
        float stopDistance = 1.5f;

        // [핵심 추가] 폭발 좀비 처리
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            // 폭발 좀비는 공격 범위(매우 근접)에 들어오면 자폭!
            // 혹은 HP가 0일 때만 터지길 원하면 이 코드는 빼도 됩니다.
            // 하지만 보통 자폭병은 붙으면 터지므로 넣는 것을 추천합니다.
            if (dist <= 2.0f) 
            {
                // 자폭 로직 (데미지를 입혀서 죽게 만듦 -> Explode 호출됨)
                zombie.TakeDamage(50); 
                return;
            }
        }

        bool isBlocked = zombie.IsBlockedByZombie();

        if (dist > stopDistance && !isBlocked)
        {
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = false;
                zombie.Agent.SetDestination(zombie.player.position);
            }

            // 애니메이션 유지
            if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
                zombie.Anim.SetBool(zombie.hashIsCrawling, true);
            else
                zombie.Anim.SetBool(zombie.hashIsRun, true);
        }
        else
        {
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = true;
                zombie.Agent.velocity = Vector3.zero;
                zombie.Agent.ResetPath();
            }
            if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
                zombie.Anim.SetBool(zombie.hashIsCrawling, false);
            else
                zombie.Anim.SetBool(zombie.hashIsRun, false);
        }

        Vector3 dir = (zombie.player.position - zombie.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            zombie.transform.rotation = Quaternion.Slerp(zombie.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }

        if (!isBlocked && dist > zombie.attackRange + 0.5f)
        {
            zombie.ChangeState(new ChaseState());
            return;
        }

        // 일반 좀비만 공격 실행
        if (zombie.zombieType == ZombieAI.ZombieType.Normal)
        {
            if (Time.time >= zombie.LastAttackTime + zombie.attackCooldown)
            {
                zombie.Anim.SetTrigger(zombie.hashAtk);
                zombie.LastAttackTime = Time.time;
                zombie.StartCoroutine(zombie.DealDamageWithDelay(zombie.attackDelay));
            }
        }
    }

    public void Exit(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = false;

        // [수정] 나갈 때 끄는 것도 분기
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        else
            zombie.Anim.SetBool(zombie.hashIsRun, false);
    }
}

public class DeadState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        zombie.isDead = true;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.AddZombieKill();
        }

        // 1. 소리 끄기
        if (zombie.audioSource != null)
        {
            zombie.audioSource.Stop();
            zombie.audioSource.loop = false;

            if (SoundManager.Instance != null)
            {
                // 재생 가능한 클립을 담을 리스트
                System.Collections.Generic.List<AudioClip> dieClips = new System.Collections.Generic.List<AudioClip>();

                // SoundManager에 등록된 1, 2, 3번 사운드 추가
                // (변수 이름이 SoundManager 스크립트와 정확히 일치해야 합니다)
                if (SoundManager.Instance.zombieDie != null) dieClips.Add(SoundManager.Instance.zombieDie);
                if (SoundManager.Instance.zombieDie2 != null) dieClips.Add(SoundManager.Instance.zombieDie2);
                if (SoundManager.Instance.zombieDie3 != null) dieClips.Add(SoundManager.Instance.zombieDie3);

                // 리스트에 있는 것 중 하나를 랜덤으로 뽑아서 재생
                if (dieClips.Count > 0)
                {
                    int randomIndex = Random.Range(0, dieClips.Count);
                    // PlayOneShot(클립, 볼륨): 볼륨 1.0f로 확실하게 재생
                    zombie.audioSource.PlayOneShot(dieClips[randomIndex], 1.0f);
                }
            }
        }

        // 2. 이동 멈추기 (필수)
        if (zombie.Agent.enabled)
        {
            zombie.Agent.isStopped = true;
            zombie.Agent.ResetPath();
            zombie.Agent.enabled = false;
        }

        // 3. 메인 콜라이더는 Trigger로 변경 (시체 통과 가능하게)
        // 단, 래그돌의 콜라이더(뼈대)들은 살아있어야 함
        if (zombie.Col != null)
        {
            zombie.Col.isTrigger = true;
        }

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnZombieKilled();
        }

        // [핵심] 애니메이션 재생 후 래그돌 전환 코루틴 시작
        zombie.StartCoroutine(RagdollDeathRoutine(zombie));
    }

    public void Execute(ZombieAI zombie) { }
    public void Exit(ZombieAI zombie) { }

    private IEnumerator RagdollDeathRoutine(ZombieAI zombie)
    {
        // A. 폭발 좀비 처리
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            // 폭발 좀비는 isCrawling을 꺼야 함
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);

            // (폭발 좀비는 별도의 폭발 로직이 있으므로 여기선 애니메이션 실행 안 함)
        }
        else
        // B. 일반 좀비 처리
        {
            // 일반 좀비는 isRun을 꺼야 함
            zombie.Anim.SetBool(zombie.hashIsRun, false);

            // -------------------------------------------------
            // [일반 좀비 전용: 랜덤 사망 애니메이션 5종]
            // -------------------------------------------------

            // 1. 실행할 트리거 목록 (Parameter에 등록된 이름이어야 함)
            int[] deathTriggers = new int[]
            {
                zombie.hashDie,       // 1번 (기존)
                zombie.hashFrontDie,  // 2번 (기존)
                zombie.hashDie3,      // 3번 (신규)
                zombie.hashDie4,      // 4번 (신규)
                zombie.hashDie5       // 5번 (신규)
            };

            // 2. 랜덤 뽑기
            int randomIndex = Random.Range(0, deathTriggers.Length);

            // 3. 실행
            zombie.Anim.SetTrigger(deathTriggers[randomIndex]);
        }

        // 애니메이션 재생 시간만큼 대기 후 삭제
        yield return new WaitForSeconds(zombie.deathAnimationDuration);
        zombie.Despawn();
    }
}