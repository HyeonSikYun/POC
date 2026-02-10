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
            Anim.SetBool(hashIsRun, false);
            // [추가] 초기화 시 크롤링 꺼주기
            Anim.SetBool(hashIsCrawling, false);
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

        if (Agent != null && Agent.isOnNavMesh)
        {
            Agent.isStopped = true;
            Agent.velocity = Vector3.zero;
        }

        StopCoroutine("HitFlashRoutine");

        // [신규] 범위 표시기 생성
        GameObject indicator = null;
        if (rangeIndicatorPrefab != null)
        {
            // 좀비 발밑에 생성
            // Y축을 0.05f 정도 살짝 올려야 바닥이랑 안 겹치고 잘 보임
            Vector3 spawnPos = transform.position;
            spawnPos.y += 0.05f;

            indicator = Instantiate(rangeIndicatorPrefab, spawnPos, Quaternion.identity);

            // [중요] 크기 맞추기
            // Cylinder 기본 크기가 지름 1m이므로, explosionRange(반지름) * 2를 해야 맞음
            float size = explosionRange * 2.0f;
            indicator.transform.localScale = new Vector3(size, 0.01f, size);
        }

        // --- 기존 깜빡임 로직 (1초) ---
        int blinkCount = 5;
        float blinkSpeed = 0.1f;

        for (int i = 0; i < blinkCount; i++)
        {
            if (meshRenderer != null) meshRenderer.material = flashMaterial;
            yield return new WaitForSeconds(blinkSpeed);

            if (meshRenderer != null) meshRenderer.material = originalMaterial;
            yield return new WaitForSeconds(blinkSpeed);
        }

        // [신규] 폭발 직전 표시기 삭제
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
        if (zombie.Agent.isOnNavMesh)
        {
            zombie.Agent.isStopped = false;

            // [수정] 타입에 따라 속도와 애니메이션 분기
            if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
            {
                zombie.Agent.speed = zombie.moveSpeed * 0.6f;
                zombie.Anim.SetBool(zombie.hashIsCrawling, true);
                // 일반 좀비용 파라미터는 건드리지 않음
            }
            else
            {
                zombie.Agent.speed = zombie.moveSpeed;
                zombie.Anim.SetBool(zombie.hashIsRun, true);
                // 폭발 좀비용 파라미터는 건드리지 않음
            }
        }

        zombie.audioSource.clip = SoundManager.Instance.zombieChase;
        zombie.audioSource.loop = true;
        zombie.audioSource.Play();
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

        // 1. 소리 끄기
        if (zombie.audioSource != null)
        {
            zombie.audioSource.Stop();
            zombie.audioSource.loop = false;
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
        // A. 폭발 좀비
        if (zombie.zombieType == ZombieAI.ZombieType.Explosive)
        {
            zombie.Anim.SetBool(zombie.hashIsCrawling, false);
        }
        else
        // B. 일반 좀비
        {
            zombie.Anim.SetBool(zombie.hashIsRun, false);

            // -------------------------------------------------
            // [방향 계산 및 애니메이션 실행]
            // 물리 힘(AddForce)이나 Ragdoll 함수는 전부 제거했습니다.
            // -------------------------------------------------
            if (zombie.player != null)
            {
                // 플레이어 -> 좀비 방향 (공격 방향)
                Vector3 attackDir = (zombie.transform.position - zombie.player.position).normalized;
                Vector3 zombieForward = zombie.transform.forward;

                // 내적 계산
                // 양수(+) = 뒤에서 맞음 -> 앞으로 넘어짐
                // 음수(-) = 앞에서 맞음 -> 뒤로 넘어짐
                float dot = Vector3.Dot(zombieForward, attackDir);

                if (dot > 0)
                {
                    zombie.Anim.SetTrigger(zombie.hashFrontDie);
                }
                else
                {
                    zombie.Anim.SetTrigger(zombie.hashDie);
                }
            }
            else
            {
                // 플레이어가 없으면 기본 모션(뒤로 넘어짐)
                zombie.Anim.SetTrigger(zombie.hashDie);
            }
        }

        // 애니메이션 재생 시간만큼 대기 후 삭제
        yield return new WaitForSeconds(zombie.deathAnimationDuration);
        zombie.Despawn();
    }
}