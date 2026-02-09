using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using UnityEngine.SceneManagement;

public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance;

    [Header("UI")]
    [SerializeField] private TMP_Dropdown languageDropdown;

    public enum Language { Korean, English }
    public Language currentLanguage;

    // 텍스트 데이터 (Key, [한국어, 영어])
    private Dictionary<string, string[]> localizedData = new Dictionary<string, string[]>();

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }

        InitLocalizationData();

        // 저장된 언어 불러오기 (기본값: 한국어(0))
        int langIndex = PlayerPrefs.GetInt("Language", 0);

        // [중요] 값을 넣기 전에, 현재 언어 기준으로 옵션 텍스트를 먼저 생성해야 함
        currentLanguage = (Language)langIndex;
    }

    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

    // [핵심 2] 씬이 로드될 때마다(재시작 포함) 실행
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 2. 사라진 UI(드롭다운)를 UIManager한테서 다시 받아옴
        if (UIManager.Instance != null)
        {
            languageDropdown = UIManager.Instance.languageDropdown;
        }

        // 3. 받아온 드롭다운이 있으면 다시 세팅 (기존 Start에 있던 로직이 여기로 옴)
        if (languageDropdown != null)
        {
            // 드롭다운 옵션(한국어/영어) 텍스트 채워넣기
            RefreshLanguageDropdown();

            // 기존 이벤트 제거 (중복 방지)
            languageDropdown.onValueChanged.RemoveAllListeners();
            languageDropdown.onValueChanged.AddListener(delegate { ChangeLanguage(languageDropdown.value); });

            // [오류 수정] 소문자 s -> 대문자 S
            // 현재 언어 값으로 드롭다운 선택 상태 변경 (이벤트 실행 안 함)
            languageDropdown.SetValueWithoutNotify((int)currentLanguage);

            // 드롭다운에 표시되는 텍스트 갱신
            languageDropdown.RefreshShownValue();
        }

        // 4. 화면에 있는 모든 텍스트들을 현재 언어로 바꿈
        // (ChangeLanguage를 호출하면 저장까지 다시 하니까, 여기선 갱신만 수행)
        UpdateAllText();

        // 튜토리얼, 가격표 등 갱신
        if (TutorialManager.Instance != null) TutorialManager.Instance.RefreshCurrentMessage();
        RefreshPriceUI();
    }


    // 언어 데이터 등록
    void InitLocalizationData()
    {
        localizedData.Add("Upgrade_Heal", new string[] { "회복 30 (최대 100)\n필요 샘플: {0}", "Heal 30 (Max 100)\nSamples: {0}" });
        // 나머지: (+수치)를 표시하도록 {1} 추가
        localizedData.Add("Upgrade_Damage", new string[] { "공격력 강화 (+{1}%)\n필요 샘플: {0}", "Damage (+{1}%)\nSamples: {0}" });

        // [수정] 탄약도 배율로 늘어나므로 %가 맞습니다. (기존 '발' -> '%')
        // 예: 탄약 확장 (+20%)
        localizedData.Add("Upgrade_Ammo", new string[] { "탄약 확장 (+{1}%)\n필요 샘플: {0}", "Max Ammo (+{1}%)\nSamples: {0}" });

        // 속도는 0.05 같은 소수점이므로 그냥 (+0.05)로 둘지, (+5%)로 할지 선택해야 합니다.
        // 현재 코드(GameManager)는 0.05를 그대로 넘겨주므로, 그냥 (+0.05)라고 뜨게 두거나
        // %로 하고 싶다면 GameManager에서 100을 곱해서 넘겨줘야 합니다.
        // 일단은 기존대로 (+0.05) 형태로 둡니다.
        localizedData.Add("Upgrade_Speed", new string[] { "속도 증가 (+{1}%)\n필요 샘플: {0}", "Speed (+{1}%)\nSamples: {0}" });
        localizedData.Add("Mission_Start", new string[]
        {
            "{0}개의 발전기를 켜고\n엘리베이터를 찾아 탑승하십시오",
            "Activate {0} generators\nand find the elevator to escape."
        });
        localizedData.Add("Resume_Btn", new string[] { "계속 하기", "Resume" });
        localizedData.Add("Option_Btn", new string[] { "설정", "Option" });
        localizedData.Add("Exit_Btn", new string[] { "게임 종료", "Exit Game" });
        localizedData.Add("Opt_BgmText", new string[] { "배경음악", "BGM" });
        localizedData.Add("Opt_SFXText", new string[] { "효과음", "SFX" });
        localizedData.Add("Opt_DisplayText", new string[] { "디스플레이", "Display" });
        localizedData.Add("Opt_DisplayFull", new string[] { "전체화면", "FullScreen" });
        localizedData.Add("Opt_DisplayWindow", new string[] { "창모드", "Windowed" });
        localizedData.Add("Opt_Resolution", new string[] { "해상도", "Resolution" });
        localizedData.Add("Opt_LanguageText", new string[] { "언어", "Language" });
        localizedData.Add("Generator_Task", new string[]
        {
            "발전기 가동 {0} / {1}",       // 한국어
            "Generators {0} / {1}" // 영어
        });
        localizedData.Add("Opt_BackBtn", new string[] { "뒤로가기", "Back" });
        localizedData.Add("Quit_Msg", new string[] { "정말 종료하시겠습니까?", "Are you sure you want to quit?" });
        localizedData.Add("Quit_Yes", new string[] { "예", "Yes" });
        localizedData.Add("Quit_No", new string[] { "아니요", "No" });

        localizedData.Add("TUTORIAL_MOVE", new string[] { "WASD를 눌러 이동하세요.", "Press WASD to move." });
        localizedData.Add("TUTORIAL_GunPickup", new string[] { "전방의 무기를 획득하세요.", "Acquire the weapon ahead." });
        localizedData.Add("TUTORIAL_GunShoot", new string[] { "프로토타입 무기 가동.\n[L-Click]으로 타겟을 제거하세요.", "Prototype weapon activated.\nEliminate targets with [L-Click]." });
        localizedData.Add("TUTORIAL_Sample", new string[] { "바이오 캡슐을 획득하세요.", "Collect Bio Capsules." });
        localizedData.Add("TUTORIAL_Tap", new string[] { "[TAB] 키를 눌러 능력치를 강화하세요.", "Press [TAB] to upgrade your abilities." });
        localizedData.Add("TUTORIAL_FinUpgrade", new string[] { "보안 프로토콜 해제.\n다음 구역으로 이동하십시오.", "Security protocol disabled.\nProceed to the next sector." });
        localizedData.Add("TUTORIAL_Generator", new string[] { "발전기를 가동하여 엘리베이터 전력을 공급하세요.", "Activate the generator to power the elevator." });
        localizedData.Add("TUTORIAL_Fin", new string[] { "목표 갱신: 최상층(지상)으로 탈출하십시오.", "Objective Updated: Escape to the surface." });
    }

    public void ChangeLanguage(int index)
    {
        currentLanguage = (Language)index;
        PlayerPrefs.SetInt("Language", index); // 저장

        // 드롭다운 옵션 텍스트도 언어에 맞게 갱신 (한국어 <-> Korean)
        RefreshLanguageDropdown();

        // 모든 텍스트 갱신
        UpdateAllText();
        if (UIManager.Instance != null)
        {
            UIManager.Instance.RefreshMissionText(); // 이 함수를 UIManager에 만들어야 함
            UIManager.Instance.RefreshGeneratorUI();
        }
        if (TutorialManager.Instance != null && GameManager.Instance != null)
        {
            if (GameManager.Instance.currentFloor == -9)
            {
                TutorialManager.Instance.RefreshCurrentMessage();
            }
        }
        RefreshPriceUI();
    }

    private void RefreshPriceUI()
    {
        if (UIManager.Instance != null && GameManager.Instance != null)
        {
            // [수정됨] GameManager의 실제 변수명과 연결하고, % 단위로 변환

            // 0.1 -> 10으로 변환 (UI에 "10"으로 표시하기 위함)
            int dmgPercent = (int)(GameManager.Instance.damageUpgradeVal * 100);
            // 0.2 -> 20으로 변환
            int ammoPercent = (int)(GameManager.Instance.ammoUpgradeVal * 100);

            UIManager.Instance.UpdateUpgradePrices(
                GameManager.Instance.costHeal,
                GameManager.Instance.costDamage,
                GameManager.Instance.costAmmo,
                GameManager.Instance.costSpeed,

                // ▼ 여기가 수정된 부분입니다 (실제 변수 연결)
                dmgPercent,                           // 공격력 (10)
                ammoPercent,                          // 탄약 (20)
                GameManager.Instance.speedUpgradeVal  // 속도 (0.05)
            );
        }
    }

    // [수정] 언어 선택 드롭다운 갱신
    public void RefreshLanguageDropdown()
    {
        if (languageDropdown == null) return;

        // 현재 선택된 인덱스 저장
        int currentIndex = (int)currentLanguage;

        languageDropdown.ClearOptions();

        List<string> options = new List<string>();

        // [핵심 변경] 
        // GetText("Opt_LanguageKor") 처럼 번역을 거치지 않고,
        // 언제나 고정된 '원어'로 표시합니다.
        options.Add("한국어");  // 0번: 항상 한국어
        options.Add("English"); // 1번: 항상 English

        languageDropdown.AddOptions(options);

        // 값 설정 및 UI 갱신
        languageDropdown.SetValueWithoutNotify(currentIndex);
        languageDropdown.RefreshShownValue();
    }

    // 씬에 있는 모든 LocalizedText를 찾아서 업데이트
    void UpdateAllText()
    {
        LocalizedText[] texts = FindObjectsOfType<LocalizedText>(true);
        foreach (var text in texts)
        {
            text.UpdateText();
        }

        GraphicSettings graphicSettings = FindObjectOfType<GraphicSettings>(); // 씬에서 찾기
        if (graphicSettings != null)
        {
            graphicSettings.RefreshDisplayModeOptions();
        }
    }

    // 텍스트 가져오기 함수
    public string GetText(string key)
    {
        if (localizedData.ContainsKey(key))
        {
            return localizedData[key][(int)currentLanguage];
        }
        return key;
    }
}