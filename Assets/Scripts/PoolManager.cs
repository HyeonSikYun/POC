using System.Collections.Generic;
using UnityEngine;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance { get; private set; }

    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
        public float lifetime;
    }

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, List<GameObject>> activeObjects; // 활성 오브젝트 추적

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
        activeObjects = new Dictionary<string, List<GameObject>>();

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
            activeObjects.Add(pool.tag, new List<GameObject>());
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
            // 풀에 사용 가능한 오브젝트가 있으면 가져오기
            objectToSpawn = poolDictionary[tag].Dequeue();
        }
        else
        {
            // 풀이 비었으면 가장 오래된 활성 오브젝트 재사용
            if (activeObjects[tag].Count > 0)
            {
                objectToSpawn = activeObjects[tag][0]; // 가장 오래된 것
                activeObjects[tag].RemoveAt(0);
                Debug.Log($"<color=yellow>{tag} 풀 부족! 기존 오브젝트 재사용</color>");
            }
            else
            {
                // 그래도 없으면 새로 생성 (마지막 수단)
                Pool pool = pools.Find(p => p.tag == tag);
                Debug.LogWarning($"<color=red>{tag} 풀 부족! 새 오브젝트 생성 - 풀 사이즈를 늘리세요!</color>");
                objectToSpawn = Instantiate(pool.prefab, transform);
            }
        }

        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.SetActive(true);

        // 활성 오브젝트 리스트에 추가
        if (!activeObjects[tag].Contains(objectToSpawn))
        {
            activeObjects[tag].Add(objectToSpawn);
        }

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

        // 활성 오브젝트 리스트에서 제거
        if (activeObjects[tag].Contains(obj))
        {
            activeObjects[tag].Remove(obj);
        }

        obj.SetActive(false);
        poolDictionary[tag].Enqueue(obj);
    }

    private System.Collections.IEnumerator ReturnToPoolAfterDelay(GameObject obj, string tag, float delay)
    {
        yield return new WaitForSeconds(delay);

        // 오브젝트가 아직 활성화되어 있을 때만 반환
        if (obj != null && obj.activeInHierarchy)
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