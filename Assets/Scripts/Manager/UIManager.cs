using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("기본 UI")]
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI ammoText;

    [Header("무기 슬롯 UI")]
    // 5개의 무기 슬롯 배경 (혹은 아이콘)
    public GameObject weaponSlotPanel;
    public Image[] weaponSlotImages;
    public Sprite[] normalWeaponSprites; // 일반 총 이미지 5개
    public Sprite[] lockedWeaponSprites; // 잠긴 총 이미지 5개

    public Color activeColor = Color.white;   // 선택된 무기 색상 (보통 흰색)
    public Color unlockedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
    public Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 미선택(해금됨) 색상 (약간 어둡게)
    private Vector3[] originalScales;

    [Header("층수UI")]
    [SerializeField] private RectTransform playerIcon;
    [SerializeField] private RectTransform[] floorAnchors; // B9(-9)부터 B1(-1)까지 순서대로 할당 (총 9개)
    [SerializeField] private GameObject floorPanel;
    [SerializeField] private float iconMoveSpeed = 2f;
    [SerializeField] private float yOffset = -50f; // 글자 아래로 보낼 간격 (조절 가능)

    [Header("장전(Reload) UI")]
    //public GameObject reloadingObject;
    public GameObject reloadGaugeGroup; // 게이지 바 전체 그룹 (배경 + fill)
    public Image reloadGaugeFill;
    private Coroutine currentReloadRoutine;

    [Header("재화 UI")]
    public TextMeshProUGUI bioSampleText;
    public Image bioSampleImg;

    [Header("튜토리얼 UI")]
    public TextMeshProUGUI tutorialText;
    public GameObject tutorialUIGroup;

    [Header("발전기 UI")]
    public TextMeshProUGUI generatorCountText;
    public GameObject interactionPromptObj;
    public GameObject progressBarObj;
    public Image progressBarFill;
    private int curGen = 0;
    private int totalGen = 0;

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
    private Coroutine iconMoveCoroutine;
    private bool isEnding = false;

    private void Awake()
    {
        if (Instance == null) { Instance = this; } 
        else { Destroy(gameObject); }
    }

    private void Start()
    {
        ResetGameUI();
        ShowGeneratorUI(false);
        if (missionPanelGroup != null)
        {
            missionPanelGroup.alpha = 0f; // 투명하게 시작
            missionPanelGroup.gameObject.SetActive(false);
        }

        if (weaponSlotImages != null)
        {
            originalScales = new Vector3[weaponSlotImages.Length];
            for (int i = 0; i < weaponSlotImages.Length; i++)
            {
                if (weaponSlotImages[i] != null)
                {
                    // 에디터에서 설정한 그 크기를 그대로 기억!
                    originalScales[i] = weaponSlotImages[i].rectTransform.localScale;
                }
            }
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
    public void UpdateBioSample(int amount)
    {
        if (bioSampleText != null)
        {
            // 앞에 이미지를 배치했으므로 텍스트는 곱하기 기호(X)와 숫자만 표시
            bioSampleText.text = $"X {amount}";
        }
    }
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

    public void UpdateHealth(int currentHealth) { if (healthText == null) return; int displayHealth = Mathf.Max(0, currentHealth); healthText.text = $"HP {displayHealth}"; healthText.color = displayHealth <= 30 ? Color.red : Color.white; }
    public void UpdateWeaponName(string name) { if (weaponNameText != null) weaponNameText.text = name; }
    public void UpdateAmmo(int current, int max) { if (ammoText != null) ammoText.text = $"{current} / {max}"; }
    public void ShowReloading(bool isReloading)
    {
        if (isReloading)
        {
            // 1. 기존 텍스트(Reloading...) 켜기
            //if (reloadingObject != null) reloadingObject.SetActive(true);

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
            //if (reloadingObject != null) reloadingObject.SetActive(false);
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

            // 게이지 채우기
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
    // [수정된 함수] UIManager.cs 안에 덮어씌우세요
    public void UpdateGeneratorCount(int current, int total)
    {
        // 1. 나중을 위해 값 기억해두기
        curGen = current;
        totalGen = total;

        // 2. 텍스트 갱신 (화면에 표시)
        RefreshGeneratorUI();
    }

    // [추가] 언어가 바뀌었을 때 호출할 함수
    public void RefreshGeneratorUI()
    {
        if (generatorCountText == null) return;

        if (LanguageManager.Instance != null)
        {
            // 기억해둔 curGen, totalGen을 사용하여 다시 번역해서 출력
            string format = LanguageManager.Instance.GetText("Generator_Task");

            // 안전장치
            if (format == "Generator_Task") format = "Generators: {0} / {1}";

            generatorCountText.text = string.Format(format, curGen, totalGen);
        }
    }
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

    // 1. 즉시 이동 (게임 시작 혹은 층 로딩 직후)
    public void SetFloorIconImmediate(int floor)
    {
        if (isEnding) return; // [추가] 엔딩 상태면 로직 무시

        // 아이콘이 꺼져있다면 다시 켭니다 (엔딩 후 재시작 대비)
        if (playerIcon != null && !playerIcon.gameObject.activeSelf)
            playerIcon.gameObject.SetActive(true);

        int anchorIndex = floor + 9; // -9층 -> 0번 인덱스
        if (anchorIndex >= 0 && anchorIndex < floorAnchors.Length)
        {
            // [중요] 레이아웃 계산 시간을 벌기 위해 즉시 이동 시에도 한 프레임 대기하는 것이 안전하지만, 
            // 수동 배치라면 바로 적용합니다.
            Vector2 targetPos = floorAnchors[anchorIndex].anchoredPosition;
            targetPos.y += yOffset;
            playerIcon.anchoredPosition = targetPos;
        }
    }

    // 2. 부드럽게 이동 (엘리베이터 이동 10초 동안)
    public void AnimateFloorIcon(int targetFloor, float duration)
    {
        // 아이콘 활성화 확인
        if (playerIcon != null && !playerIcon.gameObject.activeSelf)
            playerIcon.gameObject.SetActive(true);

        int anchorIndex = targetFloor + 9;
        if (anchorIndex >= 0 && anchorIndex < floorAnchors.Length)
        {
            // [수정] StopAllCoroutines() 대신 '이동 코루틴'만 멈춤!
            // 이래야 페이드 인/아웃 코루틴이 끊기지 않습니다.
            if (iconMoveCoroutine != null) StopCoroutine(iconMoveCoroutine);

            Vector2 targetPos = floorAnchors[anchorIndex].anchoredPosition;
            targetPos.y += yOffset;

            iconMoveCoroutine = StartCoroutine(MoveIconRoutine(targetPos, duration));
        }
    }

    private IEnumerator MoveIconRoutine(Vector2 targetPos, float duration)
    {
        Vector2 startPos = playerIcon.anchoredPosition;
        float elapsed = 0;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            // 부드러운 이동
            playerIcon.anchoredPosition = Vector2.Lerp(startPos, targetPos, elapsed / duration);
            yield return null;
        }
        playerIcon.anchoredPosition = targetPos;
        iconMoveCoroutine = null; // 초기화
    }

    public void UpdateWeaponSlots(bool[] unlockedStates, int currentIndex)
    {
        if (weaponSlotImages == null) return;
        // 방어 코드: Start가 실행되기 전에 호출될 경우를 대비
        if (originalScales == null || originalScales.Length != weaponSlotImages.Length) return;

        for (int i = 0; i < weaponSlotImages.Length; i++)
        {
            if (weaponSlotImages[i] == null) continue;

            // 1. 현재 선택된 무기 (Selected)
            if (i == currentIndex)
            {
                // 이미지 & 색상 변경
                if (normalWeaponSprites != null && i < normalWeaponSprites.Length)
                    weaponSlotImages[i].sprite = normalWeaponSprites[i];
                weaponSlotImages[i].color = activeColor;

                // [크기] 원래 크기 * 1.2배 (살짝 키움)
                weaponSlotImages[i].rectTransform.localScale = originalScales[i] * 1.4f;
            }
            // 2. 해금됐지만 안 쓰는 무기 (Unlocked)
            else if (unlockedStates[i])
            {
                if (normalWeaponSprites != null && i < normalWeaponSprites.Length)
                    weaponSlotImages[i].sprite = normalWeaponSprites[i];
                weaponSlotImages[i].color = unlockedColor;

                // [크기] 원래 크기로 복구
                weaponSlotImages[i].rectTransform.localScale = originalScales[i];
            }
            // 3. 잠긴 무기 (Locked)
            else
            {
                if (lockedWeaponSprites != null && i < lockedWeaponSprites.Length)
                    weaponSlotImages[i].sprite = lockedWeaponSprites[i];
                weaponSlotImages[i].color = Color.white;

                // [크기] 원래 크기로 복구
                weaponSlotImages[i].rectTransform.localScale = originalScales[i];
            }
        }
    }

    public void SetEndingUIState()
    {
        isEnding = true;
        // 1. 전투 정보 숨기기
        if (floorPanel != null) floorPanel.SetActive(false);
        upgradePanel.SetActive(false);
        if (weaponSlotPanel != null) weaponSlotPanel.SetActive(false);
        // [추가] 엔딩 시 플레이어 아이콘도 확실히 숨김
        if (playerIcon != null) playerIcon.gameObject.SetActive(false);
        bioSampleImg.gameObject.SetActive(false);
        if (healthText != null) healthText.gameObject.SetActive(false);
        if (weaponNameText != null) weaponNameText.gameObject.SetActive(false);
        if (ammoText != null) ammoText.gameObject.SetActive(false);
        if (bioSampleText != null) bioSampleText.gameObject.SetActive(false);
        if (generatorCountText != null) generatorCountText.gameObject.SetActive(false);
        if (HealthSystem.Instance != null)
        {
            HealthSystem.Instance.gameObject.SetActive(false);
        }
        // 2. 장전 게이지 등 끄기
        if (reloadGaugeGroup != null) reloadGaugeGroup.SetActive(false);
        //if (reloadingObject != null) reloadingObject.SetActive(false);

        // 일시정지 패널 등 시스템 UI는 ESC 작동을 위해 유지됩니다.
    }

    public void SetWeaponUIVisible(bool isVisible)
    {
        if (weaponSlotPanel != null) weaponSlotPanel.SetActive(isVisible);
        if (weaponNameText != null) weaponNameText.gameObject.SetActive(isVisible);
        if (ammoText != null) ammoText.gameObject.SetActive(isVisible); // 탄약 텍스트도 같이 숨기는 게 자연스럽습니다.

        // 무기 슬롯이 보일 때만 초기화 한번 해주기 (선택적)
        if (isVisible && weaponSlotImages != null)
        {
            // 켜질 때 갱신이 필요하다면 여기서 로직 추가 가능
        }
    }

    public void ResetGameUI()
    {
        isEnding = false; // 엔딩 상태 해제

        // 1. 기본 HUD 켜기
        if (floorPanel != null) floorPanel.SetActive(true);
        if (upgradePanel != null) upgradePanel.SetActive(false); // 업그레이드는 꺼진게 기본
        bool showWeaponUI = true;

        if (GameManager.Instance != null)
        {
            // -9층(튜토리얼)이면서, 재시작(Retry) 상태가 아니면 숨김
            if (GameManager.Instance.currentFloor == -9 && !GameManager.Instance.isRetry)
            {
                showWeaponUI = false;
            }
        }
        SetWeaponUIVisible(showWeaponUI);
        // 아이콘 다시 켜기
        if (playerIcon != null)
        {
            playerIcon.gameObject.SetActive(true);
            // 위치도 초기화하고 싶다면 여기서 SetFloorIconImmediate(0) 호출 가능
        }

        if (bioSampleImg != null) bioSampleImg.gameObject.SetActive(true);
        //if (weaponNameText != null) weaponNameText.gameObject.SetActive(true);
        //if (ammoText != null) ammoText.gameObject.SetActive(true);
        if (bioSampleText != null) bioSampleText.gameObject.SetActive(true);

        // 2. 미션/발전기 관련은 튜토리얼 룸에서는 꺼져있는 게 맞음
        ShowGeneratorUI(false);

        // 3. 체력 시스템 등 다른 매니저의 오브젝트가 있다면 켜주기
        if (HealthSystem.Instance != null)
        {
            HealthSystem.Instance.gameObject.SetActive(true);
        }

        // 4. 페이드 캔버스 초기화 (투명하게)
        if (globalFadeCanvas != null)
        {
            globalFadeCanvas.alpha = 0f;
            globalFadeCanvas.blocksRaycasts = false;
        }

        Debug.Log("UI가 리셋되었습니다 (다시 보임)");
    }
}