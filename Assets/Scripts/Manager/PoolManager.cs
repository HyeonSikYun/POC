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
    private Dictionary<string, List<GameObject>> activeObjects; // 활성화된 오브젝트 관리

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        activeObjects = new Dictionary<string, List<GameObject>>();

        InitializePools();
    }

    private void InitializePools()
    {
        foreach (Pool pool in pools)
        {
            Queue<GameObject> objectPool = new Queue<GameObject>();
            for (int i = 0; i < pool.size; i++)
            {
                CreateNewObject(pool, objectPool);
            }
            poolDictionary.Add(pool.tag, objectPool);
            activeObjects.Add(pool.tag, new List<GameObject>());
        }
    }

    private GameObject CreateNewObject(Pool pool, Queue<GameObject> queue = null)
    {
        if (pool.prefab == null) return null;

        GameObject obj = Instantiate(pool.prefab, transform);
        obj.SetActive(false);
        if (queue != null) queue.Enqueue(obj);
        return obj;
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(tag)) return null;

        GameObject objectToSpawn = null;
        Queue<GameObject> poolQueue = poolDictionary[tag];

        // 1. 대기열에서 꺼내기 (파괴된 객체는 버림)
        while (poolQueue.Count > 0)
        {
            objectToSpawn = poolQueue.Dequeue();
            if (objectToSpawn != null) break;
        }

        // 2. 대기열이 비었으면?
        if (objectToSpawn == null)
        {
            // -> 활성 리스트에서 죽은(null) 객체 정리
            if (activeObjects.ContainsKey(tag))
            {
                activeObjects[tag].RemoveAll(item => item == null);
            }

            // -> 그래도 없으면 새로 생성 (풀 확장)
            Pool pool = pools.Find(p => p.tag == tag);
            objectToSpawn = CreateNewObject(pool);
            Debug.LogWarning($"<color=yellow>{tag} 풀 고갈 -> 추가 생성됨 (Pool Size를 늘리는 것을 권장합니다)</color>");
        }

        // 3. 배치 및 활성화
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.SetActive(true);
        objectToSpawn.transform.SetParent(transform); // 부모를 PoolManager로 고정 (맵 삭제 시 보호)

        // 4. 활성 목록에 등록
        if (activeObjects.ContainsKey(tag))
        {
            if (!activeObjects[tag].Contains(objectToSpawn))
            {
                activeObjects[tag].Add(objectToSpawn);
            }
        }

        // 5. 인터페이스 호출
        IPooledObject pooledObj = objectToSpawn.GetComponent<IPooledObject>();
        if (pooledObj != null) pooledObj.OnObjectSpawn();

        // 6. ZombieAI 초기화 호출 (필수)
        ZombieAI zombie = objectToSpawn.GetComponent<ZombieAI>();
        if (zombie != null) zombie.Initialize(position);

        return objectToSpawn;
    }

    public void ReturnToPool(string tag, GameObject obj)
    {
        if (obj == null) return;
        if (!poolDictionary.ContainsKey(tag)) { Destroy(obj); return; }

        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }

        // 활성 목록에서 제거
        if (activeObjects.ContainsKey(tag))
        {
            activeObjects[tag].Remove(obj);
        }

        obj.SetActive(false);
        obj.transform.SetParent(transform);
        poolDictionary[tag].Enqueue(obj);
    }

    // [핵심 추가] 맵 이동 시 모든 몬스터를 강제로 풀로 복귀시키는 함수
    public void ReturnAllActiveObjects()
    {
        foreach (var key in activeObjects.Keys)
        {
            // 리스트를 복사해서 순회 (중간에 Remove가 일어나므로)
            List<GameObject> list = new List<GameObject>(activeObjects[key]);
            foreach (var obj in list)
            {
                if (obj != null && obj.activeInHierarchy)
                {
                    // 즉시 반환 처리
                    obj.SetActive(false);
                    obj.transform.SetParent(transform);

                    if (poolDictionary.ContainsKey(key))
                        poolDictionary[key].Enqueue(obj);
                }
            }
            // 리스트 초기화
            activeObjects[key].Clear();
        }
        Debug.Log("모든 활성 오브젝트가 풀로 반환되었습니다.");
    }
}

public interface IPooledObject
{
    void OnObjectSpawn();
}