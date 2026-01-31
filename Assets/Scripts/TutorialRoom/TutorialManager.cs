using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance;

    [Header("단계별 텍스트")]
    public string msgMove = "WASD를 눌러 이동하세요.";
    public string msgGetGun = "전방의 무기를 획득하세요.";
    public string msgCombat = "목표물을 사격하여 제거하세요.";
    public string msgLoot = "바이오 캡슐을 획득하세요.";
    public string msgUpgrade = "[TAB] 키를 눌러 강화하세요.";
    public string msgEscape = "보안 해제. 다음 구역으로 이동하십시오.";
    public string msgGenerator = "발전기를 가동하여 엘리베이터 전력을 공급하세요.";
    public string msgFinalGoal = "목표 갱신: 최상층(지상)으로 탈출하십시오.";

    [Header("오브젝트 연결")]
    public GameObject gunItem;         // 바닥에 떨어진 총 아이템
    public GameObject zombieGroup;     // 좀비 그룹
    public TutorialDoor exitDoor;      // 탈출구 문

    // 현재 진행 단계 (0:이동 -> 1:총발견 -> 2:전투 -> 3:파밍 -> 4:강화 -> 5:탈출)
    private int currentStep = 0;

    private int zombiesKilled = 0;
    private int totalZombies = 2; // 좀비 개수

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        // 시작하자마자 할 일
        currentStep = 0;

        // 1. 좀비와 총 아이템은 일단 숨김
        if (zombieGroup != null) zombieGroup.SetActive(false);
        if (gunItem != null) gunItem.SetActive(false);

        // 2. 시작 텍스트 띄우기 (0.5초 뒤에 뜨는 연출이 있다면 그게 덮어쓸 수 있음)
        if (UIManager.Instance != null)
            UIManager.Instance.ShowTutorialText(msgMove);
    }

    // --- 이벤트 함수들 ---

    // [Step 1] 복도 진입 시 (Trigger가 호출)
    public void OnPlayerEnterCorridor()
    {
        // 이미 총을 먹었거나(2단계 이상) 진행 중이면 무시함 -> 메시지 안 꼬임!
        if (currentStep == 0)
        {
            currentStep = 1;
            UpdateText(msgGetGun);

            // ★ 여기서 총 아이템을 보이게 켭니다!
            if (gunItem != null) gunItem.SetActive(true);
        }
    }

    // [Step 2] 총을 먹었을 때 (GunPickup이 호출)
    public void OnGunPickedUp()
    {
        // 복도를 안 지나고 총을 먹는 버그가 있어도 강제로 2단계로 점프
        if (currentStep < 2)
        {
            currentStep = 2;
            UpdateText(msgCombat);

            // 좀비 등장!
            if (zombieGroup != null) zombieGroup.SetActive(true);
        }
    }

    // [Step 3] 좀비 처치 (ZombieAI가 호출)
    public void OnZombieKilled()
    {
        if (currentStep == 2)
        {
            zombiesKilled++;
            if (zombiesKilled >= totalZombies)
            {
                currentStep = 3;
                UpdateText(msgLoot);
            }
        }
    }

    // [Step 4] 캡슐 2개 획득 (BioSample이 호출해야 함 - GameManager가 체크)
    // GameManager의 Update나 캡슐 획득 함수에서 체크해서 호출해주세요.
    public void CheckCapsuleCount(int currentCount)
    {
        if (currentStep == 3 && currentCount >= 2)
        {
            currentStep = 4;
            UpdateText(msgUpgrade);
        }
    }

    // [Step 5] 강화 완료 (GameManager가 호출)
    public void OnUpgradeCompleted()
    {
        if (currentStep == 4)
        {
            currentStep = 5;
            UpdateText(msgEscape);
            if (exitDoor != null) exitDoor.OpenDoor();
        }
    }

    public void OnPlayerEnterGeneratorRoom()
    {
        // 5단계(문 열림) 상태에서만 반응
        if (currentStep == 5)
        {
            currentStep = 6;
            UpdateText(msgGenerator); // "발전기를 가동하세요" 출력
        }
    }

    // [추가] 발전기가 켜졌을 때 (TutorialElevator에서 호출)
    public void OnTutorialGeneratorActivated()
    {
        if (currentStep == 6)
        {
            currentStep = 7; // 완료 상태
            // 텍스트 숨기기
            UpdateText(msgFinalGoal);
        }
    }

    // [추가] 엘리베이터 타고 올라갈 때 (최종 목표 안내)
    public void ShowFinalGoalMessage()
    {
        UpdateText(msgFinalGoal);
    }

    private void UpdateText(string msg)
    {
        if (UIManager.Instance != null)
            UIManager.Instance.ShowTutorialText(msg);
    }
}