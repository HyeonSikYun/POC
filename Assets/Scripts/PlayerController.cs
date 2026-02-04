using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Movement")]
    public float speed = 5f; // 기본 속도
    public bool isPc;

    [Header("Gun")]
    public bool hasGun = false;
    [SerializeField] private GunController gunController;

    [Header("Push Prevention")]
    [SerializeField] private float maxPushForce = 2f;
    [SerializeField] private LayerMask pushableLayer;

    [Header("Sound")]
    public float stepRate = 0.4f;   // 발소리 간격 (0.4초마다)
    private float nextStepTime = 0f; // 다음 발소리 시간

    private float verticalVelocity;
    private float gravity = -9.81f;
    private Vector2 move, mouseLook, joystickLook;
    private Vector3 rotationTarget;
    private Animator anim;
    private CharacterController charCon;
    private Vector3 pushForce = Vector3.zero;

    public void OnMove(InputAction.CallbackContext context)
    {
        move = context.ReadValue<Vector2>();
    }

    public void OnMouseLook(InputAction.CallbackContext context)
    {
        mouseLook = context.ReadValue<Vector2>();
    }

    public void OnJoystickLook(InputAction.CallbackContext context)
    {
        joystickLook = context.ReadValue<Vector2>();
    }

    private void Start()
    {
        anim = GetComponent<Animator>();
        charCon = GetComponent<CharacterController>();

        if (gunController == null)
        {
            gunController = GetComponentInChildren<GunController>();
        }

        hasGun = false;
        if (gunController != null)
            gunController.SetWeaponVisible(false);
        currentHealth = maxHealth;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealth(currentHealth);
            UIManager.Instance.ShowTutorialText("WASD를 눌러 이동하세요.");
        }
    }

    void Update()
    {
        UpdateGunState();
        ApplyGravity();
        UpdateMovement();
        ApplyPushForce();

        HandleFootstep();
    }

    private void HandleFootstep()
    {
        // 1. 플레이어가 땅에 있고 (점프 중 아님)
        // 2. 이동 입력이 있고 (움직이는 중)
        // 3. 소리 쿨타임이 지났으면
        if (charCon.isGrounded && move.magnitude > 0.1f && Time.time >= nextStepTime)
        {
            if (SoundManager.Instance != null)
            {
                // 약간의 피치 변화를 주어 자연스럽게 (선택사항)
                // SoundManager.Instance.PlaySFX(SoundManager.Instance.footStep);

                // 만약 피치 조절 없이 그냥 재생하려면:
                SoundManager.Instance.PlaySFX(SoundManager.Instance.footStep);
            }

            // 다음 발소리 시간 예약
            nextStepTime = Time.time + stepRate;
        }
    }

    public void AcquireGun()
    {
        hasGun = true;
        if (gunController != null)
            gunController.SetWeaponVisible(true);

        anim.SetBool("gunReady", true);
    }

    // --- 체력 관련 함수 ---
    public bool IsHealthFull()
    {
        return currentHealth >= maxHealth;
    }
    public void Heal(int amount)
    {
        currentHealth += amount;
        if (currentHealth > maxHealth) currentHealth = maxHealth;

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateHealth(currentHealth);
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealth(currentHealth);
        }

        if (currentHealth <= 0)
        {
            Debug.Log("플레이어 사망!");
            // 사망 처리 로직 추가 가능 (GameManager.Instance.GameOver() 등)
        }
    }

    // [핵심 추가] 최종 이동 속도 계산 (기본 속도 * 전역 배율)
    private float GetFinalSpeed()
    {
        float multiplier = GameManager.Instance != null ? GameManager.Instance.globalMoveSpeedMultiplier : 1.0f;
        return speed * multiplier;
    }

    private void ApplyGravity()
    {
        if (charCon.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
    }

    private void ApplyPushForce()
    {
        if (pushForce.magnitude > 0.01f)
        {
            charCon.Move(pushForce * Time.deltaTime);
            pushForce = Vector3.Lerp(pushForce, Vector3.zero, Time.deltaTime * 5f);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.gameObject.CompareTag("Enemy") || hit.gameObject.layer == LayerMask.NameToLayer("Enemy"))
        {
            Vector3 pushDir = transform.position - hit.transform.position;
            pushDir.y = 0;
            pushDir = pushDir.normalized;

            pushForce += pushDir * maxPushForce;
            pushForce = Vector3.ClampMagnitude(pushForce, maxPushForce);
        }
    }

    private void UpdateGunState()
    {
        anim.SetBool("gunReady", hasGun);
    }

    private void UpdateMovement()
    {
        if (isPc)
        {
            UpdateMouseAim();
            movePlayerWithAim();
        }
        else
        {
            if (joystickLook.x == 0 && joystickLook.y == 0)
            {
                movePlayer();
            }
            else
            {
                movePlayerWithAim();
            }
        }
    }

    private void UpdateMouseAim()
    {
        Ray ray = Camera.main.ScreenPointToRay(mouseLook);
        Plane playerPlane = new Plane(Vector3.up, transform.position);
        float distance = 0f;

        if (playerPlane.Raycast(ray, out distance))
        {
            rotationTarget = ray.GetPoint(distance);
        }
    }

    public void movePlayer()
    {
        Vector3 targetVector = new Vector3(move.x, 0f, move.y);
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;

        UpdateAnimation(move.x, move.y, movement.magnitude > 0.01f);

        if (movement != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement), 0.15f);
        }

        movement.y = verticalVelocity;

        // [수정] GetFinalSpeed() 적용
        charCon.Move(movement * GetFinalSpeed() * Time.deltaTime);
    }

    public void movePlayerWithAim()
    {
        UpdateRotation();

        Vector3 targetVector = new Vector3(move.x, 0f, move.y);
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;
        Vector3 localMove = transform.InverseTransformDirection(movement);

        UpdateAnimation(localMove.x, localMove.z, movement.magnitude > 0.01f);

        movement.y = verticalVelocity;

        // [수정] GetFinalSpeed() 적용
        charCon.Move(movement * GetFinalSpeed() * Time.deltaTime);
    }

    private void UpdateRotation()
    {
        if (isPc)
        {
            if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

            var lookPos = rotationTarget - transform.position;
            lookPos.y = 0f;

            if (lookPos != Vector3.zero)
            {
                var rotation = Quaternion.LookRotation(lookPos);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 0.05f);
            }
        }
        else
        {
            Vector3 aimDir = new Vector3(joystickLook.x, 0f, joystickLook.y);
            if (aimDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(aimDir), 0.15f);
            }
        }
    }

    private void UpdateAnimation(float moveX, float moveY, bool isMoving)
    {
        anim.SetFloat("MoveX", moveX);
        anim.SetFloat("MoveY", moveY);
        anim.SetBool("isMoving", isMoving);
    }
}