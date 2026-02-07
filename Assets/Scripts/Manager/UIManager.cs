using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("기본 UI")]
    public TextMeshProUGUI floorText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI ammoText;

    [Header("장전(Reload) UI")]
    public GameObject reloadingObject;
    public GameObject reloadGaugeGroup; // 게이지 바 전체 그룹 (배경 + fill)
    public Image reloadGaugeFill;
    private Coroutine currentReloadRoutine;

    [Header("재화 UI")]
    public TextMeshProUGUI bioSampleText;

    [Header("튜토리얼 UI")]
    public TextMeshProUGUI tutorialText;
    public GameObject tutorialUIGroup;

    [Header("발전기 UI")]
    public TextMeshProUGUI generatorCountText;
    public GameObject interactionPromptObj;
    public GameObject progressBarObj;
    public Image progressBarFill;

    [Header("미션 알림 UI")]
    public TextMeshProUGUI missionText;
    public CanvasGroup missionPanelGroup; // [변경] GameObject 대신 CanvasGroup을 씁니다.

    [Header("패널")]
    public GameObject upgradePanel;
    public GameObject pausePanel;
    public GameObject settingsPanel;
    public GameObject quitPanel;

    [Header("강화 메뉴 텍스트")]
    public TMPro.TextMeshProUGUI txtHealCost;
    public TMPro.TextMeshProUGUI txtDamageCost;
    public TMPro.TextMeshProUGUI txtAmmoCost;
    public TMPro.TextMeshProUGUI txtSpeedCost;

    [Header("전역 페이드 패널")]
    public CanvasGroup globalFadeCanvas;

    [Header("설정 UI 연결")]
    public TMP_Dropdown languageDropdown;    // 언어 변경 드롭다운
    public TMP_Dropdown resolutionDropdown;  // 해상도 변경 드롭다운
    public TMP_Dropdown displayModeDropdown; // 전체화면/창모드 드롭다운

    private int currentMissionCount = 0;
    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        ShowGeneratorUI(false);
        if (missionPanelGroup != null)
        {
            missionPanelGroup.alpha = 0f; // 투명하게 시작
            missionPanelGroup.gameObject.SetActive(false);
        }
    }

    // --- [핵심 추가] 버튼 연결용 중계 함수 (Bridge) ---
    // 유니티 에디터 버튼 OnClick에 GameManager 대신 이 함수들을 연결하세요!
    public void OnClickUpgradeHP()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("HP");
    }
    public void OnClickUpgradeDamage()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Damage");
    }
    public void OnClickUpgradeAmmo()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Ammo");
    }
    public void OnClickUpgradeSpeed()
    {
        if (GameManager.Instance != null) GameManager.Instance.UpgradeStat("Speed");
    }

    public void OnClickResumeBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickResume();
    }
    public void OnClickOptionsBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickOptions();
    }
    public void OnClickQuitBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickQuit();
    }
    public void OnClickOptionsBackBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickOptionsBack();
    }

    // [추가] 종료 확인 창 -> '예' 버튼용
    public void OnClickQuitYesBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickQuitYes();
    }

    // [추가] 종료 확인 창 -> '아니요' 버튼용
    public void OnClickQuitNoBridge()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnClickQuitNo();
    }
    // -----------------------------------------------------

    // [추가됨] 페이드 효과를 즉시 적용하는 함수 (재시작 시 깜빡임 방지용)
    public void SetFadeAlpha(float alpha)
    {
        if (globalFadeCanvas != null)
        {
            globalFadeCanvas.alpha = alpha;
            globalFadeCanvas.blocksRaycasts = (alpha > 0.1f);
        }
    }

    public void ShowGeneratorUI(bool isShow)
    {
        if (generatorCountText != null)
        {
            generatorCountText.gameObject.SetActive(isShow);
        }
    }


    public void ShowMissionStartMessage(int count)
    {
        currentMissionCount = count; // 개수 기억해두기!
        ShowGeneratorUI(true);

        // 텍스트 갱신 (함수로 분리)
        RefreshMissionText();

        if (missionPanelGroup != null) StartCoroutine(MissionFadeRoutine());
    }

    public void RefreshMissionText()
    {
        if (missionText != null && LanguageManager.Instance != null)
        {
            // 기억해둔 currentMissionCount를 다시 넣어서 재번역
            string format = LanguageManager.Instance.GetText("Mission_Start");
            missionText.text = string.Format(format, currentMissionCount);
        }
    }

    private IEnumerator MissionFadeRoutine()
    {
        // 1. 켜기 (아직 투명함)
        missionPanelGroup.gameObject.SetActive(true);
        missionPanelGroup.alpha = 0f;

        // 2. 페이드 인 (나타나기) - 0.5초 동안
        float timer = 0f;
        while (timer < 0.5f)
        {
            timer += Time.deltaTime;
            missionPanelGroup.alpha = Mathf.Lerp(0f, 1f, timer / 0.5f);
            yield return null;
        }
        missionPanelGroup.alpha = 1f;

        // 3. 유지 (2초 대기)
        yield return new WaitForSeconds(2.0f);

        // 4. 페이드 아웃 (사라지기) - 1초 동안
        timer = 0f;
        while (timer < 1.0f)
        {
            timer += Time.deltaTime;
            missionPanelGroup.alpha = Mathf.Lerp(1f, 0f, timer / 1.0f);
            yield return null;
        }
        missionPanelGroup.alpha = 0f;

        // 5. 끄기
        missionPanelGroup.gameObject.SetActive(false);
    }

    public void ShowTutorialText(string message)
    {
        // [핵심] 메시지가 비어있으면 패널을 끕니다.
        bool shouldShow = !string.IsNullOrEmpty(message);

        if (tutorialUIGroup != null)
        {
            tutorialUIGroup.SetActive(shouldShow);
        }

        if (shouldShow && tutorialText != null)
        {
            tutorialText.text = message;
        }
    }

    public void HideTutorialText()
    {
        if (tutorialUIGroup != null) tutorialUIGroup.SetActive(false);
    }

    // --- 기존 UI 함수들 ---
    public void UpdateBioSample(int amount) { if (bioSampleText != null) bioSampleText.text = $"Samples: {amount}"; }
    public void ShowUpgradePanel(bool show) { if (upgradePanel != null) upgradePanel.SetActive(show); }

    // [수정] 가격 업데이트 함수 ({0} 문제 해결)
    // [수정됨] 가격(Cost)과 증가량(Value)을 모두 받아서 표시
    public void UpdateUpgradePrices(int healCost, int dmgCost, int ammoCost, int spdCost,
                                    int dmgVal, int ammoVal, float spdVal)
    {
        if (LanguageManager.Instance == null) return;

        // 체력: {0} = 가격 (최대 체력 100 설명은 텍스트 자체에 포함됨)
        string healFmt = LanguageManager.Instance.GetText("Upgrade_Heal");
        if (txtHealCost != null) txtHealCost.text = string.Format(healFmt, healCost);

        // 데미지: {0} = 가격, {1} = 증가량
        string dmgFmt = LanguageManager.Instance.GetText("Upgrade_Damage");
        if (txtDamageCost != null) txtDamageCost.text = string.Format(dmgFmt, dmgCost, dmgVal);

        // 탄약: {0} = 가격, {1} = 증가량
        string ammoFmt = LanguageManager.Instance.GetText("Upgrade_Ammo");
        if (txtAmmoCost != null) txtAmmoCost.text = string.Format(ammoFmt, ammoCost, ammoVal);

        // 속도: {0} = 가격, {1} = 증가량 (소수점이 길어질 수 있으니 F1 포맷 사용 추천)
        string speedFmt = LanguageManager.Instance.GetText("Upgrade_Speed");
        if (txtSpeedCost != null) txtSpeedCost.text = string.Format(speedFmt, spdCost, spdVal.ToString("F1"));
    }

    public void UpdateFloor(int floorIndex) { if (floorText == null) return; string floorString = floorIndex < 0 ? $"B{Mathf.Abs(floorIndex)}" : (floorIndex == 0 ? "Lobby" : $"{floorIndex}F"); floorText.text = floorString; }
    public void UpdateHealth(int currentHealth) { if (healthText == null) return; int displayHealth = Mathf.Max(0, currentHealth); healthText.text = $"HP {displayHealth}"; healthText.color = displayHealth <= 30 ? Color.red : Color.white; }
    public void UpdateWeaponName(string name) { if (weaponNameText != null) weaponNameText.text = name; }
    public void UpdateAmmo(int current, int max) { if (ammoText != null) ammoText.text = $"{current} / {max}"; }
    public void ShowReloading(bool isReloading)
    {
        if (isReloading)
        {
            // 1. 기존 텍스트(Reloading...) 켜기
            if (reloadingObject != null) reloadingObject.SetActive(true);

            // 2. 게이지 바 그룹 켜기
            if (reloadGaugeGroup != null)
            {
                reloadGaugeGroup.SetActive(true);

                // 기존 코루틴 멈추고 새로 시작
                if (currentReloadRoutine != null) StopCoroutine(currentReloadRoutine);
                currentReloadRoutine = StartCoroutine(ReloadBarRoutine(3.0f));
            }
        }
        else
        {
            // 장전 끝: 둘 다 끄기
            if (reloadingObject != null) reloadingObject.SetActive(false);
            if (reloadGaugeGroup != null) reloadGaugeGroup.SetActive(false);

            if (currentReloadRoutine != null) StopCoroutine(currentReloadRoutine);
        }
    }
    IEnumerator ReloadBarRoutine(float duration)
    {
        float timer = 0f;

        // 처음엔 0으로 시작
        if (reloadGaugeFill != null) reloadGaugeFill.fillAmount = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            if (reloadGaugeFill != null)
            {
                // 경과 시간 비율만큼 채움 (0.0 ~ 1.0)
                reloadGaugeFill.fillAmount = timer / duration;
            }
            yield return null;
        }

        // 끝났을 때 확실하게 꽉 채움
        if (reloadGaugeFill != null) reloadGaugeFill.fillAmount = 1f;
    }
    public void UpdateGeneratorCount(int current, int total) { if (generatorCountText != null) generatorCountText.text = $"{current} / {total}"; }
    public void ShowInteractionPrompt(bool isVisible) { if (interactionPromptObj != null) interactionPromptObj.SetActive(isVisible); }
    public void UpdateInteractionProgress(float ratio) { bool shouldShow = ratio > 0f && ratio < 1.0f; if (progressBarObj != null) progressBarObj.SetActive(shouldShow); if (progressBarFill != null) progressBarFill.fillAmount = ratio; }
    public void ShowPausePanel(bool isOpen) { if (pausePanel != null) { pausePanel.SetActive(isOpen); if (!isOpen && settingsPanel != null) settingsPanel.SetActive(false); } }
    public void ShowSettingsPanel(bool isOpen) { if (settingsPanel != null) settingsPanel.SetActive(isOpen); }
    public void ShowQuitConfirmPanel(bool isShow)
    {
        if (quitPanel) quitPanel.SetActive(isShow);
    }

    public IEnumerator FadeOut()
    {
        if (globalFadeCanvas == null) yield break;
        globalFadeCanvas.blocksRaycasts = true;
        float t = 0f;
        while (t < 1f) { t += Time.deltaTime * 1.5f; globalFadeCanvas.alpha = t; yield return null; }
        globalFadeCanvas.alpha = 1f;
    }

    public IEnumerator FadeIn()
    {
        if (globalFadeCanvas == null) yield break;
        float t = 1f;
        while (t > 0f) { t -= Time.deltaTime * 1.5f; globalFadeCanvas.alpha = t; yield return null; }
        globalFadeCanvas.alpha = 0f;
        globalFadeCanvas.blocksRaycasts = false;
    }
}