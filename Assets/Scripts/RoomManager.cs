using UnityEngine;

public class RoomManager : MonoBehaviour
{
    [Header("방 오브젝트 설정")]
    [SerializeField] private GameObject mainRoom;
    [SerializeField] private GameObject room1;
    [SerializeField] private GameObject room2;
    [SerializeField] private GameObject elevator;

    private void Start()
    {
        // 시작 시 메인룸만 활성화
        ShowRoom(mainRoom);
    }

    public void ShowRoom(GameObject roomToShow)
    {
        // 모든 방 비활성화
        if (mainRoom != null) mainRoom.SetActive(false);
        if (room1 != null) room1.SetActive(false);
        if (room2 != null) room2.SetActive(false);
        if (elevator != null) elevator.SetActive(false);

        // 선택한 방만 활성화
        if (roomToShow != null)
        {
            roomToShow.SetActive(true);
        }
    }

    public void ShowMainRoom() => ShowRoom(mainRoom);
    public void ShowRoom1() => ShowRoom(room1);
    public void ShowRoom2() => ShowRoom(room2);
    public void ShowElevator() => ShowRoom(elevator);
}
