using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;

public class PlayerController : MonoBehaviour
{
    public float speed;
    public bool isPc;
    private Vector2 move, mouseLook, joystickLook;
    private Vector3 rotationTarget;
    private Animator anim;
    private CharacterController charCon;

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
    }

    void Update()
    {
        if (isPc)
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(mouseLook);
            if (Physics.Raycast(ray, out hit))
            {
                rotationTarget = hit.point;
            }
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

    public void movePlayer()
    {
        Vector3 targetVector = new Vector3(move.x, 0f, move.y);

        // 카메라 Y축 회전을 기준으로 입력 방향 회전
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;

        // 애니메이션 파라미터 설정
        anim.SetFloat("MoveX", move.x);
        anim.SetFloat("MoveY", move.y);
        anim.SetBool("IsMoving", movement.magnitude > 0.01f);

        if (movement != Vector3.zero)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(movement), 0.15f);

        charCon.Move(movement * speed * Time.deltaTime);
    }

    public void movePlayerWithAim()
    {
        // 회전 처리
        if (isPc)
        {
            var lookPos = rotationTarget - transform.position;
            lookPos.y = 0f;
            var rotation = Quaternion.LookRotation(lookPos);
            Vector3 aimDir = new Vector3(rotationTarget.x, 0f, rotationTarget.z);
            if (aimDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, 0.15f);
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

        Vector3 targetVector = new Vector3(move.x, 0f, move.y);

        // 카메라 Y축 회전을 기준으로 입력 방향 회전
        Vector3 movement = Quaternion.Euler(0, Camera.main.transform.eulerAngles.y, 0) * targetVector;

        // 이동 방향을 로컬 좌표로 변환 (캐릭터 기준)
        Vector3 localMove = transform.InverseTransformDirection(movement);

        // 애니메이션 파라미터 설정 (로컬 방향 기준)
        anim.SetFloat("MoveX", localMove.x);
        anim.SetFloat("MoveY", localMove.z);
        anim.SetBool("IsMoving", movement.magnitude > 0.01f);

        charCon.Move(movement * speed * Time.deltaTime);
    }
}