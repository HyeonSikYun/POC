using UnityEngine;

public class DoorTeleporter : MonoBehaviour
{
    [Header("목표 방 위치")]
    [SerializeField] private Transform targetRoomPosition;

    [Header("목표 방 오브젝트")]
    [SerializeField] private GameObject targetRoom;

    [Header("텔레포트 설정")]
    [SerializeField] private float teleportCooldown = 0.3f;

    private static float lastTeleportTime = -999f;
    private RoomManager roomManager;

    private void Start()
    {
        roomManager = FindObjectOfType<RoomManager>();
        if (roomManager == null)
        {
            Debug.LogError("RoomManager를 찾을 수 없습니다!");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (Time.time - lastTeleportTime > teleportCooldown)
            {
                TeleportPlayer(other.transform);
            }
        }
    }

    private void TeleportPlayer(Transform player)
    {
        if (targetRoomPosition == null || targetRoom == null)
        {
            Debug.LogWarning("목표 방 위치 또는 방 오브젝트가 설정되지 않았습니다!");
            return;
        }

        lastTeleportTime = Time.time;

        // 목표 방 활성화 (다른 방들은 자동으로 비활성화됨)
        if (roomManager != null)
        {
            roomManager.ShowRoom(targetRoom);
        }

        // CharacterController 처리
        CharacterController controller = player.GetComponent<CharacterController>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        // 플레이어 워프
        player.position = targetRoomPosition.position;
        player.rotation = targetRoomPosition.rotation;

        // CharacterController 재활성화
        if (controller != null)
        {
            controller.enabled = true;
        }
    }
}
