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

    [Header("전투 설정")]
    public float attackCooldown = 2f;
    public float attackDelay = 0.5f;
    public int maxHealth = 100;
    public int currentHealth;

    [Header("죽음 설정")]
    public float deathAnimationDuration = 3f;

    [Header("드랍 아이템")]
    public GameObject bioSamplePrefab; // 죽을 때 떨어질 재화 프리팹
    [Tooltip("아이템이 떨어질 바닥 레이어를 선택하세요 (Default, Ground, Wall 등)")]
    public LayerMask groundLayer; // [추가] 바닥 감지용 레이어

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

        if (Agent != null) Agent.enabled = false;
    }

    private void Start()
    {
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

        currentHealth = maxHealth;
        LastAttackTime = -attackCooldown;
        isDead = false;

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

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            ChangeState(new DeadState());
        }
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

        Vector3 spawnPos = transform.position; // 기본값: 현재 좀비 위치

        // 좀비의 배꼽 정도 위치(위쪽 1.0f)에서 아래로 레이저를 쏨
        Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
        RaycastHit hit;

        // 아래로 2.0f 거리만큼 쏴서 groundLayer에 닿는지 확인
        if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 2.0f, groundLayer))
        {
            // 바닥을 찾았으면, 바닥 위치(hit.point)에서 살짝 위(0.1f)에 생성
            spawnPos = hit.point + Vector3.up * 0.1f;
        }
        else
        {
            // 바닥을 못 찾았으면(공중에 있거나 등), 현재 위치에서 살짝 위
            spawnPos += Vector3.up * 0.1f;
        }

        Instantiate(bioSamplePrefab, spawnPos, Quaternion.identity);
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

        if (dist <= zombie.detectionRange)
        {
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
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null)
        {
            zombie.ChangeState(new IdleState());
            return;
        }
        if (!zombie.Agent.isOnNavMesh) return;

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        if (dist <= zombie.attackRange)
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
        if (zombie.player == null)
        {
            zombie.ChangeState(new IdleState());
            return;
        }

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);

        // [핵심 로직] "공격 사거리 안쪽"이지만 "딱 붙지 않았을 때"의 이동 처리
        // 예: 공격 사거리(2m)보다 안쪽이지만, 1.2m보다는 멀면 -> 조금 더 다가감 (무빙샷)
        // 1.2m보다 가까우면 -> 멈춤 (스탠딩샷)

        float stopDistance = 1.5f; // 공격 사거리보다 약간 짧게 설정

        if (dist > stopDistance)
        {
            // 1. 거리가 좀 있으면 -> 움직이면서 공격
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = false;
                zombie.Agent.SetDestination(zombie.player.position);
            }
            zombie.Anim.SetBool(zombie.hashIsRun, true); // 하체: 달리기 / 상체: 공격
        }
        else
        {
            // 2. 충분히 가까우면 -> 멈춰서 공격
            if (zombie.Agent.isOnNavMesh)
            {
                zombie.Agent.isStopped = true;
                zombie.Agent.ResetPath(); // 미끄러짐 방지
            }
            zombie.Anim.SetBool(zombie.hashIsRun, false); // 하체: 대기 / 상체: 공격
        }

        // [회전] 항상 플레이어를 바라봄
        Vector3 dir = (zombie.player.position - zombie.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            zombie.transform.rotation = Quaternion.Slerp(zombie.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 10f);
        }

        // [상태 전환] 거리가 공격 사거리를 완전히 벗어나면 추적 상태로
        // 0.5f의 여유를 둬서 경계선에서 상태가 왔다갔다 하는 것 방지
        if (dist > zombie.attackRange + 0.5f)
        {
            zombie.ChangeState(new ChaseState());
            return;
        }

        // [공격 실행]
        if (Time.time >= zombie.LastAttackTime + zombie.attackCooldown)
        {
            zombie.Anim.SetTrigger(zombie.hashAtk);
            zombie.LastAttackTime = Time.time;
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
            // zombie.Col.enabled = false;  <-- 이 줄이 문제였음 (삭제)
            zombie.Col.isTrigger = true; // <-- 이렇게 변경 (감지는 되되, 길막은 안 함)
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