using System.Collections;
using System.Diagnostics;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 트레이서 효과 (총알 궤적)
    public void SpawnTracer(Vector3 start, Vector3 end, float width, Color color, float duration)
    {
        GameObject tracer = PoolManager.Instance.SpawnFromPool("Tracer", start, Quaternion.identity);
        if (tracer != null)
        {
            LineRenderer lr = tracer.GetComponent<LineRenderer>();
            if (lr != null)
            {
                lr.startWidth = width;
                lr.endWidth = width;
                lr.startColor = color;
                lr.endColor = new Color(color.r, color.g, color.b, 0f); // 끝부분 투명하게

                lr.SetPosition(0, start);
                lr.SetPosition(1, end);

                StartCoroutine(FadeOutTracer(tracer, lr, duration));
            }
        }
    }

    private IEnumerator FadeOutTracer(GameObject tracer, LineRenderer lr, float duration)
    {
        float elapsed = 0f;
        Color startColor = lr.startColor;
        Color endColor = lr.endColor;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / duration);

            lr.startColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
            lr.endColor = new Color(endColor.r, endColor.g, endColor.b, alpha * 0.5f);

            yield return null;
        }

        tracer.SetActive(false);
    }

    // 총알이 적/벽에 맞았을 때 이펙트
    public void PlayHitEffect(Vector3 position, Vector3 normal)
    {
        GameObject effect = PoolManager.Instance.SpawnFromPool("HitEffect", position, Quaternion.LookRotation(normal));
        if (effect != null)
        {
            ParticleSystem ps = effect.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear();
                ps.Play();
            }

            StartCoroutine(DisableAfterDelay(effect, 1f));
        }
    }

    private IEnumerator DisableAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (obj != null && obj.activeInHierarchy)
        {
            obj.SetActive(false);
        }
    }
}