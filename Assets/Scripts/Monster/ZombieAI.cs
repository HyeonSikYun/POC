using UnityEngine;
using UnityEngine.AI;
using System.Collections;

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
    [Header("타겟 설정")]
    public Transform player;

    [Header("AI 설정")]
    public float detectionRange = 10f;
    public float attackRange = 2f;
    public float moveSpeed = 3.5f;

    [Header("충돌 방지")]
    public LayerMask zombieLayer; // 인스펙터에서 'Zombie' 레이어를 선택하세요!

    [Header("전투 설정")]
    public float attackCooldown = 2f;
    public float attackDelay = 0.5f;
    public int defaultMaxHealth = 100;
    public int maxHealth;
    public int currentHealth;

    [Header("죽음 설정")]
    public float deathAnimationDuration = 3f;

    [Header("피격 플래시 설정")]
    public Renderer meshRenderer;       // 좀비 몸통 (Skinned Mesh Renderer)
    public Color damageColor = Color.red; // 맞았을 때 변할 색 (빨강 추천)
    private Color originColor;          // 원래 색 저장용
    public Material flashMaterial; // 여기에 흰색 Unlit 재질 연결
    private Material originalMaterial;

    [Header("드랍 아이템")]
    public GameObject bioSamplePrefab; // 죽을 때 떨어질 재화 프리팹
    [Tooltip("아이템이 떨어질 바닥 레이어를 선택하세요 (Default, Ground, Wall 등)")]
    public LayerMask groundLayer; // [추가] 바닥 감지용 레이어

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
    public readonly int hashAtk = Animator.StringToHash("zombie1Atk");
    public readonly int hashDie = Animator.StringToHash("zombie1Die");

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
        meshRenderer = GetComponentInChildren<SkinnedMeshRenderer>(); // 혹은 MeshRenderer
        originalMaterial = meshRenderer.material; // 원래 옷 기억하기
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
        // 내 눈높이(바닥+1m)에서 정면으로 1m 레이저 발사
        Vector3 origin = transform.position + Vector3.up * 1.0f;

        // 레이저가 'ZombieLayer'에 닿았는지 체크
        if (Physics.Raycast(origin, transform.forward, out RaycastHit hit, 1.5f, zombieLayer))
        {
            // 내가 쏜 레이저에 맞은 게 '나 자신'이 아니면 true
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

        // [핵심 수정] 
        // 잘못된 예: maxHealth = maxHealth * multiplier; (X -> 계속 누적됨)
        // 올바른 예: maxHealth = defaultMaxHealth * multiplier; (O -> 항상 원본 기준)
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
            // [추가] 공격 사거리보다 약간 짧게 멈추는 거리 설정 (예: 1.5m)
            // 이렇게 하면 ChaseState에서 그냥 SetDestination만 해도 알아서 앞에서 멈춤
            Agent.stoppingDistance = attackRange - 0.5f;

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
            // 플레이어가 존재하고 & 안전지대 상태라면 -> 데미지 함수 즉시 종료(return)
            if (pc != null && pc.isSafeZone)
            {
                return;
            }
        }

        // [추가] 피격 플래시 재생
        if (meshRenderer != null)
        {
            // 혹시 이미 깜빡이는 중이면 멈추고 다시 (연타 맞을 때 대비)
            StopCoroutine("HitFlashRoutine");
            StartCoroutine("HitFlashRoutine");
        }

        SoundManager.Instance.PlaySFX(SoundManager.Instance.gunHit);

        // 체력 감소
        currentHealth -= damage;
        GameManager.Instance.ShowDamagePopup(transform.position, damage);
        if (currentHealth <= 0)
        {
            ChangeState(new DeadState());
        }
        else
        {
            // [추가된 기능 2] 맞았는데 아직 안 죽었고, 멍하니 있는 상태라면? -> 즉시 추적 시작!
            // 플레이어가 감지 범위 밖이어도 맞으면 쫓아옵니다.
            if (currentState is IdleState)
            {
                ChangeState(new ChaseState());
            }
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        // 흰색 옷으로 갈아입기
        meshRenderer.material = flashMaterial;

        yield return new WaitForSeconds(0.1f);

        // 원래 옷으로 갈아입기
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
                pc.TakeDamage(10);
                Debug.Log("플레이어 피격 성공! (딜레이 적용됨)");
            }
        }
    }

    // [추가] 바닥 위치를 정확히 찾아서 아이템을 드랍하는 함수
    private void DropItem()
    {
        if (bioSamplePrefab == null) return;

        Vector3 finalSpawnPos = transform.position;

        // 1. 네비메쉬(땅) 위에서 가장 가까운 유효한 위치 찾기
        // transform.position(좀비 위치)에서 반경 2.0f 안쪽의 NavMesh 바닥을 찾음
        if (UnityEngine.AI.NavMesh.SamplePosition(transform.position, out UnityEngine.AI.NavMeshHit hit, 2.0f, UnityEngine.AI.NavMesh.AllAreas))
        {
            finalSpawnPos = hit.position;
        }
        else
        {
            // 만약(정말 희박하지만) 네비메쉬를 못 찾았다면?
            // 벽 밖으로 튕겨 나간 상태일 수 있으므로, 플레이어 쪽으로 살짝 당겨옴
            if (player != null)
            {
                Vector3 dirToPlayer = (player.position - transform.position).normalized;
                finalSpawnPos = transform.position + (dirToPlayer * 1.0f);
            }
        }

        // 2. 바닥에 파묻히지 않게 높이(Y) 살짝 올리기
        finalSpawnPos.y += 1f;

        // 3. 아이템 생성
        Quaternion spawnRotation = Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);

        Instantiate(bioSamplePrefab, finalSpawnPos, spawnRotation);
    }

    public void Despawn()
    {
        // [수정] 단순 생성이 아니라 DropItem 함수를 통해 안전하게 생성
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
    }
}

// ================= 상태 클래스들 =================

public class IdleState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;
        zombie.Anim.SetBool(zombie.hashIsRun, false);
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        // [수정] 거리가 가까워도 플레이어가 '안전지대'면 추적 안 함!
        if (dist <= zombie.detectionRange)
        {
            // 1. 플레이어의 PlayerController 가져오기
            PlayerController pc = zombie.player.GetComponent<PlayerController>();

            // 2. [핵심] 안전지대(엘리베이터 안)라면 추적 금지! (여기서 return해버림)
            if (pc != null && pc.isSafeZone) return;

            // 3. 안전지대가 아닐 때만 추적 시작
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
            zombie.Agent.speed = zombie.moveSpeed;
        }
        zombie.Anim.SetBool(zombie.hashIsRun, true);

        zombie.audioSource.clip = SoundManager.Instance.zombieChase;
        zombie.audioSource.loop = true; // 쫓아오는 동안 계속 소리나게 Loop 켜기
        zombie.audioSource.Play();
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) { /* ... */ return; }
        if (!zombie.Agent.isOnNavMesh) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        PlayerController pc = zombie.player.GetComponent<PlayerController>();
        if (pc != null && pc.isSafeZone)
        {
            zombie.ChangeState(new IdleState());
            return;
        }
        // [수정] 공격 사거리 안이거나 OR (플레이어가 근처에 있고 && 앞이 막혔으면)
        // dist < 5.0f 조건은 너무 멀리서끼리 비비는 건 무시하고, 플레이어 근처에서만 작동하게 함
        bool isBlocked = zombie.IsBlockedByZombie();

        if (dist <= zombie.attackRange || (dist < 5.0f && isBlocked))
        {
            zombie.ChangeState(new AttackState());
            return;
        }

        zombie.Agent.SetDestination(zombie.player.position);
    }

    public void Exit(ZombieAI zombie)
    {
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;
        zombie.Anim.SetBool(zombie.hashIsRun, false);
    }
}

public class AttackState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        // [수정] 들어오자마자 무조건 멈추는 코드 삭제
        // 거리 판단은 Execute에서 실시간으로 합니다.
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null) { /* ... */ return; }

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);
        float stopDistance = 1.5f;

        // [핵심 수정] 앞이 막혔는지 체크
        bool isBlocked = zombie.IsBlockedByZombie();

        // 1. 거리가 멀고 AND 앞이 뚫려있을 때만 이동 (추적)
        if (dist > stopDistance && !isBlocked)
        {
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = false;
                zombie.Agent.SetDestination(zombie.player.position);
            }
            zombie.Anim.SetBool(zombie.hashIsRun, true);
        }
        else
        // 2. 가까이 있거나 OR 앞이 막혔으면 -> 제자리 멈춤 (공격 준비)
        {
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = true;
                zombie.Agent.velocity = Vector3.zero; // 미는 힘 제거
                zombie.Agent.ResetPath();
            }
            zombie.Anim.SetBool(zombie.hashIsRun, false);
        }

        // 회전 (항상 플레이어 보기)
        Vector3 dir = (zombie.player.position - zombie.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            zombie.transform.rotation = Quaternion.Slerp(zombie.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }

        // [상태 전환 체크]
        // 앞이 뚫렸는데 거리가 너무 멀어지면 다시 추적
        if (!isBlocked && dist > zombie.attackRange + 0.5f)
        {
            zombie.ChangeState(new ChaseState());
            return;
        }

        // [공격 실행]
        if (Time.time >= zombie.LastAttackTime + zombie.attackCooldown)
        {
            zombie.Anim.SetTrigger(zombie.hashAtk);
            zombie.LastAttackTime = Time.time;

            // 주의: 뒤에 있는 좀비는 공격 모션은 취하지만
            // DealDamageToPlayer 내부의 '거리 체크' 때문에 실제 데미지는 안 들어감 (이게 정상)
            zombie.StartCoroutine(zombie.DealDamageWithDelay(zombie.attackDelay));
        }
    }

    public void Exit(ZombieAI zombie)
    {
        // 상태를 나갈 때는 다시 움직일 수 있게 풀어줌
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = false;
        zombie.Anim.SetBool(zombie.hashIsRun, false);
    }
}

public class DeadState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        zombie.isDead = true;

        if (zombie.audioSource != null)
        {
            zombie.audioSource.Stop();
            zombie.audioSource.loop = false; // 루프 해제
        }

        // [이전 코드] 상체 레이어 힘 빼기 (서서 죽는 문제 해결용)
        zombie.Anim.SetLayerWeight(1, 0f);

        if (zombie.Agent.enabled)
        {
            zombie.Agent.isStopped = true;
            zombie.Agent.ResetPath();
            zombie.Agent.enabled = false;
        }

        // [핵심 수정] Collider를 끄지 마세요! (끄면 손전등이 못 찾아서 투명해짐)
        // 대신 Trigger로 바꿔서 플레이어가 밟지 않고 지나갈 수 있게 합니다.
        if (zombie.Col != null)
        {
            zombie.Col.isTrigger = true; // <-- 이렇게 변경 (감지는 되되, 길막은 안 함)
        }

        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.OnZombieKilled();
        }

        zombie.Anim.SetBool(zombie.hashIsRun, false);
        zombie.Anim.SetTrigger(zombie.hashDie);

        zombie.StartCoroutine(DespawnRoutine(zombie));
    }


    public void Execute(ZombieAI zombie) { }
    public void Exit(ZombieAI zombie) { }

    private IEnumerator DespawnRoutine(ZombieAI zombie)
    {
        yield return new WaitForSeconds(zombie.deathAnimationDuration);
        zombie.Despawn();
    }
}