using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class ZombieAI : MonoBehaviour, IPooledObject
{
    [Header("타겟 설정")]
    public Transform player;

    [Header("AI 설정")]
    public float detectionRange = 10f; // 플레이어 감지 거리
    public float attackRange = 2f; // 공격 거리
    public float moveSpeed = 3.5f;

    [Header("전투 설정")]
    public float attackCooldown = 2f; // 공격 쿨타임
    public int maxHealth = 100;

    [Header("디버그")]
    public bool showGizmos = true;

    private NavMeshAgent agent;
    private Animator animator;
    private int currentHealth;
    private float lastAttackTime;
    private bool isDead = false;

    // 애니메이션 파라미터 해시
    private static readonly int IsRun = Animator.StringToHash("isRun");
    private static readonly int Zombie1Atk = Animator.StringToHash("zombie1Atk");
    private static readonly int Zombie1Die = Animator.StringToHash("zombie1Die");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // NavMeshAgent 비활성화 (스폰 시 활성화)
        if (agent != null)
        {
            agent.enabled = false;
        }
    }

    private void Start()
    {
        // 플레이어 자동 찾기
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }

    public void OnObjectSpawn()
    {
        // 풀에서 스폰될 때 초기화
        currentHealth = maxHealth;
        isDead = false;
        lastAttackTime = 0f;

        if (animator != null)
        {
            animator.SetBool(IsRun, false);
        }

        // NavMeshAgent 활성화 및 초기화
        if (agent != null)
        {
            // NavMesh가 존재하는지 먼저 확인
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 10f, NavMesh.AllAreas))
            {
                agent.enabled = true;
                StartCoroutine(InitializeAgent());
            }
            else
            {
                Debug.LogWarning($"NavMesh가 아직 베이크되지 않았습니다! {gameObject.name}");
            }
        }

        // 플레이어 찾기
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
            }
        }
    }

    private System.Collections.IEnumerator InitializeAgent()
    {
        // NavMesh에 배치될 때까지 대기
        yield return new WaitUntil(() => agent.isOnNavMesh);

        agent.speed = moveSpeed;
        agent.isStopped = false;
        agent.ResetPath();
    }

    private void Update()
    {
        if (isDead || player == null || !agent.isOnNavMesh) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= detectionRange)
        {
            if (distanceToPlayer <= attackRange)
            {
                // 공격 범위 안
                AttackPlayer();
            }
            else
            {
                // 추적
                ChasePlayer();
            }
        }
        else
        {
            // 대기
            Idle();
        }
    }

    private void ChasePlayer()
    {
        agent.isStopped = false;
        agent.SetDestination(player.position);
        animator.SetBool(IsRun, true);
    }

    private void AttackPlayer()
    {
        agent.isStopped = true;
        animator.SetBool(IsRun, false);

        // 플레이어를 바라보기
        Vector3 direction = (player.position - transform.position).normalized;
        direction.y = 0;
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(direction),
                Time.deltaTime * 5f
            );
        }

        // 공격 쿨타임 체크
        if (Time.time >= lastAttackTime + attackCooldown)
        {
            animator.SetTrigger(Zombie1Atk);
            lastAttackTime = Time.time;

            // 실제 데미지는 애니메이션 이벤트에서 처리하는 것을 권장
            // DealDamageToPlayer();
        }
    }

    private void Idle()
    {
        agent.isStopped = true;
        animator.SetBool(IsRun, false);
    }

    public void TakeDamage(int damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        Debug.Log($"좀비 체력: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        agent.isStopped = true;
        animator.SetBool(IsRun, false);
        animator.SetTrigger(Zombie1Die);

        // 죽은 후 풀로 반환 (애니메이션 재생 시간 고려)
        Invoke(nameof(ReturnToPool), 3f);
    }

    private void ReturnToPool()
    {
        // 풀로 반환 (PoolManager가 어떤 태그로 관리하는지에 따라 수정)
        PoolManager.Instance.ReturnToPool("Zombie", gameObject);
    }

    // 애니메이션 이벤트에서 호출 (공격 모션 중간에)
    public void DealDamageToPlayer()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            // 플레이어 데미지 처리
            // PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
            // if (playerHealth != null)
            // {
            //     playerHealth.TakeDamage(10);
            // }
            Debug.Log("플레이어에게 데미지!");
        }
    }

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // 감지 범위
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // 공격 범위
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }

    // NavMeshAgent가 비활성화될 때 경고 방지
    private void OnDisable()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }
}