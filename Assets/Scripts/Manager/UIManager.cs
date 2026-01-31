using UnityEngine;
using TMPro;
using UnityEngine.UI; // [필수] Image 사용을 위해 추가

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("기본 UI")]
    public TextMeshProUGUI floorText;
    public TextMeshProUGUI healthText;
    public TextMeshProUGUI weaponNameText;
    public TextMeshProUGUI ammoText;
    public GameObject reloadingObject;

    [Header("재화 UI")]
    public TextMeshProUGUI bioSampleText;

    [Header("튜토리얼 UI")]
    public TextMeshProUGUI tutorialText;
    public GameObject tutorialUIGroup;

    // [추가됨] 발전기 관련 UI
    [Header("발전기 UI")]
    public TextMeshProUGUI generatorCountText; // 상단 "0 / 2" 표시
    public GameObject interactionPromptObj;    // "E키를 길게 누르세요" 텍스트 오브젝트
    public GameObject progressBarObj;          // 게이지 바 부모 오브젝트 (배경)
    public Image progressBarFill;              // 게이지 바 채워지는 이미지 (Image Type: Filled)

    [Header("업그레이드 패널")]
    public GameObject upgradePanel;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    // --- [추가됨] 발전기 UI 제어 함수 ---
    public void UpdateGeneratorCount(int current, int total)
    {
        if (generatorCountText != null)
        {
            generatorCountText.text = $"{current} / {total}";
        }
    }

    public void ShowInteractionPrompt(bool isVisible)
    {
        if (interactionPromptObj != null)
            interactionPromptObj.SetActive(isVisible);
    }

    public void UpdateInteractionProgress(float ratio)
    {
        // ratio가 0보다 크면 게이지를 켜고, 0이면 끕니다.
        bool shouldShow = ratio > 0f && ratio < 1.0f;

        if (progressBarObj != null)
            progressBarObj.SetActive(shouldShow);

        if (progressBarFill != null)
            progressBarFill.fillAmount = ratio;
    }

    public void ShowTutorialText(string message)
    {
        // 1. 그룹 전체(패널+텍스트)를 켭니다.
        if (tutorialUIGroup != null)
            tutorialUIGroup.SetActive(true);

        // 2. 텍스트 내용을 바꿉니다.
        if (tutorialText != null)
        {
            tutorialText.text = message;
        }
    }

    public void HideTutorialText()
    {
        // [핵심] 텍스트만 끄는 게 아니라, 그룹 전체를 꺼서 패널도 사라지게 합니다.
        if (tutorialUIGroup != null)
            tutorialUIGroup.SetActive(false);
    }

    // --- 기존 UI 함수들 (그대로 유지) ---
    public void UpdateBioSample(int amount) { if (bioSampleText != null) bioSampleText.text = $"Samples: {amount}"; }
    public void ShowUpgradePanel(bool show) { if (upgradePanel != null) upgradePanel.SetActive(show); }
    public void UpdateFloor(int floorIndex)
    {
        if (floorText == null) return;
        string floorString = "";
        if (floorIndex < 0) floorString = $"B{Mathf.Abs(floorIndex)}";
        else if (floorIndex == 0) floorString = "Lobby";
        else floorString = $"{floorIndex}F";
        floorText.text = floorString;
    }
    public void UpdateHealth(int currentHealth)
    {
        if (healthText == null) return;
        int displayHealth = Mathf.Max(0, currentHealth);
        healthText.text = $"HP {displayHealth}";
        if (displayHealth <= 30) healthText.color = Color.red;
        else healthText.color = Color.white;
    }
    public void UpdateWeaponName(string name) { if (weaponNameText != null) weaponNameText.text = name; }
    public void UpdateAmmo(int current, int max) { if (ammoText != null) ammoText.text = $"{current} / {max}"; }
    public void ShowReloading(bool isReloading) { if (reloadingObject != null) reloadingObject.SetActive(isReloading); }
}