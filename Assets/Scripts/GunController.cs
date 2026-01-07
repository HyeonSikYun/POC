using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GunController : MonoBehaviour
{
    public enum GunType { Semi, Auto };
    public GunType gunType;
    public Transform spawn;
    public float fireRate = 0.1f; // 연사 속도 (초 단위)

    private PlayerController playerController;
    private Coroutine shootCoroutine;
    private LineRenderer tracer;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        if(GetComponent<LineRenderer>())
        {
            tracer = GetComponent<LineRenderer>();
        }
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (!playerController.hasGun) return;

        // 1. 버튼을 눌렀을 때 (Started)
        if (context.started)
        {
            if (gunType == GunType.Semi)
            {
                Shoot();
            }
            else if (gunType == GunType.Auto)
            {
                // 이미 실행 중인 코루틴이 있다면 중지 후 새로 시작
                if (shootCoroutine != null) StopCoroutine(shootCoroutine);
                shootCoroutine = StartCoroutine(AutoShootRoutine());
            }
        }

        // 2. 버튼에서 손을 떼었을 때 (Canceled)
        if (context.canceled)
        {
            if (shootCoroutine != null)
            {
                StopCoroutine(shootCoroutine);
                shootCoroutine = null;
            }
        }
    }

    private IEnumerator AutoShootRoutine()
    {
        while (true)
        {
            Shoot();
            yield return new WaitForSeconds(fireRate);
        }
    }

    public void Shoot()
    {
        Ray ray = new Ray(spawn.position, spawn.forward);
        RaycastHit hit;
        float shotDistance = 20;

        if (Physics.Raycast(ray, out hit, shotDistance))
        {
            shotDistance = hit.distance;
        }
        if(tracer)
        {
            StartCoroutine("RenderTracer", ray.direction * shotDistance);
        }
        Debug.Log("Shot!");
    }

    IEnumerator RenderTracer(Vector3 hitPoint)
    {
        tracer.enabled = true;
        tracer.SetPosition(0, spawn.position);
        tracer.SetPosition(1, spawn.position + hitPoint);
        yield return null;
        tracer.enabled = false;
    }
}