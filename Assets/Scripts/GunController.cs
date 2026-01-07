using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class GunController : MonoBehaviour
{
    public enum GunType { Semi, Auto };
    public GunType gunType;
    public Transform spawn;
    public Transform shellPoint;
    public float fireRate = 0.1f;

    private PlayerController playerController;
    private Coroutine shootCoroutine;
    private LineRenderer tracer;

    private void Start()
    {
        playerController = GetComponentInParent<PlayerController>();
        if (GetComponent<LineRenderer>())
        {
            tracer = GetComponent<LineRenderer>();
        }
    }

    public void OnFire(InputAction.CallbackContext context)
    {
        if (!playerController.hasGun) return;

        if (context.started)
        {
            if (gunType == GunType.Semi)
            {
                Shoot();
            }
            else if (gunType == GunType.Auto)
            {
                if (shootCoroutine != null) StopCoroutine(shootCoroutine);
                shootCoroutine = StartCoroutine(AutoShootRoutine());
            }
        }

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

        if (tracer)
        {
            StartCoroutine("RenderTracer", ray.direction * shotDistance);
        }

        // 범용 오브젝트 풀에서 탄피 가져오기
        GameObject shellObj = PoolManager.Instance.SpawnFromPool("Shell", shellPoint.position, Quaternion.identity);

        if (shellObj != null)
        {
            Rigidbody rb = shellObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddForce(shellPoint.forward * Random.Range(150f, 200f) + spawn.forward * Random.Range(-10f, 10f));
            }
        }
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