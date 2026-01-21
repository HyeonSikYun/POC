using UnityEngine;
using UnityEngine.AI;
using System;

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

    [Header("죽음 애니메이션 설정")]
    public float deathAnimationDuration = 3f; // 죽는 애니메이션 길이

    [Header("디버그")]
    public bool showGizmos = true;

    private NavMeshAgent agent;
    private Animator animator;
    private Collider zombieCollider; // 콜라이더 참조
    private int currentHealth;
    private float lastAttackTime;
    private bool isDead = false;

    // 이벤트: 좀비가 죽었을 때
    public event Action<GameObject> onZombieDeath;

    // 애니메이션 파라미터 해시
    private static readonly int IsRun = Animator.StringToHash("isRun");
    private static readonly int Zombie1Atk = Animator.StringToHash("zombie1Atk");
    private static readonly int Zombie1Die = Animator.StringToHash("zombie1Die");

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        zombieCollider = GetComponent<Collider>(); // Collider 참조 저장

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

        // Collider 활성화
        if (zombieCollider != null)
        {
            zombieCollider.enabled = true;
        }

        if (animator != null)
        {
            animator.SetBool(IsRun, false);
            // 죽음 애니메이션 상태 초기화
            animator.ResetTrigger(Zombie1Die);
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
        // 다른 좀비와의 거리 체크하여 너무 가까우면 멈춤
        Collider[] nearbyZombies = Physics.OverlapSphere(transform.position, 1.5f);
        int zombieCount = 0;

        foreach (var col in nearbyZombies)
        {
            if (col.gameObject != gameObject && col.CompareTag("Enemy"))
            {
                zombieCount++;
            }
        }

        // 주변에 좀비가 너무 많으면 속도 감소
        if (zombieCount > 2)
        {
            agent.speed = moveSpeed * 0.3f; // 속도 30%로 감소
        }
        else
        {
            agent.speed = moveSpeed;
        }

        agent.isStopped = false;

        // NavMeshAgent의 stoppingDistance 설정으로 일정 거리 유지
        agent.stoppingDistance = attackRange * 0.8f;
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
        Debug.Log($"<color=yellow>좀비 체력: {currentHealth}/{maxHealth}</color>");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;

        // NavMeshAgent 정지 및 비활성화
        if (agent != null && agent.enabled)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled = false; // 완전히 비활성화
        }

        // Collider 비활성화 (죽은 좀비를 더 이상 맞출 수 없음)
        if (zombieCollider != null)
        {
            zombieCollider.enabled = false;
        }

        // 애니메이션 정지 및 죽음 애니메이션 재생
        animator.SetBool(IsRun, false);
        animator.SetTrigger(Zombie1Die);

        Debug.Log($"<color=red>좀비 사망! {gameObject.name}</color>");

        // 죽은 후 풀로 반환 (애니메이션 재생 시간 고려)
        Invoke(nameof(ReturnToPool), deathAnimationDuration);
    }

    private void ReturnToPool()
    {
        // 풀로 반환
        PoolManager.Instance.ReturnToPool("Zombie", gameObject);
        Debug.Log($"<color=cyan>좀비 풀로 반환: {gameObject.name}</color>");
    }

    // 애니메이션 이벤트에서 호출 (공격 모션 중간에)
    public void DealDamageToPlayer()
    {
        if (player == null) return;

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