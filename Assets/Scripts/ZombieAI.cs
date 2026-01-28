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
        FindPlayer();
    }

    private void Update()
    {
        if (currentState != null)
        {
            currentState.Execute(this);
        }
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
        currentHealth = maxHealth;
        LastAttackTime = -attackCooldown;
        isDead = false;

        Anim.SetLayerWeight(1, 1f);

        if (Col != null) Col.enabled = true;
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
        if (zombie.Agent.isOnNavMesh) zombie.Agent.isStopped = true;
        zombie.Anim.SetBool(zombie.hashIsRun, false);
    }

    public void Execute(ZombieAI zombie)
    {
        if (zombie.player == null)
        {
            zombie.ChangeState(new IdleState());
            return;
        }

        Vector3 dir = (zombie.player.position - zombie.transform.position).normalized;
        dir.y = 0;
        if (dir != Vector3.zero)
        {
            zombie.transform.rotation = Quaternion.Slerp(zombie.transform.rotation, Quaternion.LookRotation(dir), Time.deltaTime * 5f);
        }

        float dist = Vector3.Distance(zombie.transform.position, zombie.player.position);
        if (dist > zombie.attackRange)
        {
            zombie.ChangeState(new ChaseState());
            return;
        }

        if (Time.time >= zombie.LastAttackTime + zombie.attackCooldown)
        {
            zombie.Anim.SetTrigger(zombie.hashAtk);
            zombie.LastAttackTime = Time.time;
            zombie.StartCoroutine(zombie.DealDamageWithDelay(zombie.attackDelay));
        }
    }

    public void Exit(ZombieAI zombie) { }
}

public class DeadState : IZombieState
{
    public void Enter(ZombieAI zombie)
    {
        zombie.isDead = true;

        // [핵심] 상체 레이어(인덱스 1)의 가중치를 0으로 만듭니다.
        // 이렇게 하면 상체 레이어가 무시되고, Base Layer의 죽는 애니메이션이 전신에 적용됩니다.
        zombie.Anim.SetLayerWeight(1, 0f);

        if (zombie.Agent.enabled)
        {
            zombie.Agent.isStopped = true;
            zombie.Agent.ResetPath();
            zombie.Agent.enabled = false;
        }

        if (zombie.Col != null) zombie.Col.enabled = false;

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