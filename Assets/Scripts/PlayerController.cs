using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Health")]
    public int maxHealth = 100;
    private int currentHealth;

    [Header("Movement")]
    public float speed;
    public bool isPc;

    [Header("Gun")]
    public bool hasGun = false;
    [SerializeField] private GunController gunController;

    [Header("Push Prevention")]
    [SerializeField] private float maxPushForce = 2f;
    [SerializeField] private LayerMask pushableLayer;

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

        currentHealth = maxHealth;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealth(currentHealth);
        }
    }

    void Update()
    {
        UpdateGunState();
        ApplyGravity();
        UpdateMovement();
        ApplyPushForce();
    }

    // [추가] 강화 상점에서 호출할 체력 회복 함수
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
        }
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
        // [수정 전] 물리 레이캐스트 (바닥을 찍음 -> 오차 발생)
        /*
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(mouseLook);
        if (Physics.Raycast(ray, out hit))
        {
            rotationTarget = hit.point;
        }
        */

        // [수정 후] 수학적 평면 사용 (플레이어 높이를 기준으로 좌표 계산)
        Ray ray = Camera.main.ScreenPointToRay(mouseLook);

        // 플레이어의 현재 높이(transform.position.y)에 수평면(Vector3.up)을 생성합니다.
        Plane playerPlane = new Plane(Vector3.up, transform.position);
        float distance = 0f;

        // 레이가 이 평면과 만나는 지점을 구합니다.
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
        charCon.Move(movement * speed * Time.deltaTime);
    }

    public void movePlayerWithAim()
    {
        UpdateRotation();

        Vector3 targetVector = new Vector3(move.x, 0f, move.y);
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;
        Vector3 localMove = transform.InverseTransformDirection(movement);

        UpdateAnimation(localMove.x, localMove.z, movement.magnitude > 0.01f);

        movement.y = verticalVelocity;
        charCon.Move(movement * speed * Time.deltaTime);
    }

    private void UpdateRotation()
    {
        if (isPc)
        {
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