using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string tag;              // 풀 식별용 태그
        public GameObject prefab;       // 생성할 프리팹
        public int size;                // 초기 풀 크기
        public float lifetime;          // 자동 반환 시간 (0이면 수동 반환)
    }

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        poolDictionary = new Dictionary<string, Queue<GameObject>>();

        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, transform);
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.tag, objectPool);
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            return null;
        }

        GameObject objectToSpawn;

        if (poolDictionary[tag].Count > 0)
        {
            objectToSpawn = poolDictionary[tag].Dequeue();
        }
        else
        {
            // 풀이 부족하면 새로 생성
            Pool pool = pools.Find(p => p.tag == tag);
            objectToSpawn = Instantiate(pool.prefab, transform);
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.SetActive(true);

        // IPooledObject 인터페이스가 있으면 OnObjectSpawn 호출
        IPooledObject pooledObj = objectToSpawn.GetComponent<IPooledObject>();
        if (pooledObj != null)
        {
            pooledObj.OnObjectSpawn();
        }

        // lifetime이 설정되어 있으면 자동 반환
        Pool poolInfo = pools.Find(p => p.tag == tag);
        if (poolInfo != null && poolInfo.lifetime > 0)
        {
            StartCoroutine(ReturnToPoolAfterDelay(objectToSpawn, tag, poolInfo.lifetime));
        }

        return objectToSpawn;
    }

    public void ReturnToPool(string tag, GameObject obj)
    {
        if (!poolDictionary.ContainsKey(tag))
        {
            Debug.LogWarning($"Pool with tag {tag} doesn't exist.");
            Destroy(obj);
            return;
        }

        // Rigidbody가 있으면 속도 초기화
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        obj.SetActive(false);
        poolDictionary[tag].Enqueue(obj);
    }

    private System.Collections.IEnumerator ReturnToPoolAfterDelay(GameObject obj, string tag, float delay)
    {
        yield return new WaitForSeconds(delay);

        // 오브젝트가 아직 활성화되어 있을 때만 반환
        if (obj.activeInHierarchy)
        {
            ReturnToPool(tag, obj);
        }
    }
}

// 풀링된 오브젝트가 스폰될 때 초기화가 필요하면 이 인터페이스 구현
public interface IPooledObject
{
    void OnObjectSpawn();
}