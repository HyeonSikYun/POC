using UnityEngine;
using TMPro;

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
    public TextMeshProUGUI bioSampleText; // "Samples: 0" 형태로 표시

    [Header("업그레이드 패널")]
    public GameObject upgradePanel; // Tab 누르면 켜질 전체 패널

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // --- 재화 및 업그레이드 관련 ---
    public void UpdateBioSample(int amount)
    {
        if (bioSampleText != null)
            bioSampleText.text = $"Samples: {amount}";
    }

    public void ShowUpgradePanel(bool show)
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(show);
    }

    // --- 기존 UI 함수들 ---
    public void UpdateFloor(int floorIndex)
    {
        if (floorText == null) return;

        string floorString = "";

        if (floorIndex < 0)
            floorString = $"B{Mathf.Abs(floorIndex)}";
        else if (floorIndex == 0)
            floorString = "Lobby";
        else
            floorString = $"{floorIndex}F";

        floorText.text = floorString;
    }

    public void UpdateHealth(int currentHealth)
    {
        if (healthText == null) return;

        int displayHealth = Mathf.Max(0, currentHealth);
        healthText.text = $"HP {displayHealth}";

        if (displayHealth <= 30)
            healthText.color = Color.red;
        else
            healthText.color = Color.white;
    }

    public void UpdateWeaponName(string name)
    {
        if (weaponNameText != null)
        {
            weaponNameText.text = name;
        }
    }

    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
        {
            ammoText.text = $"{current} / {max}";
        }
    }

    public void ShowReloading(bool isReloading)
    {
        if (reloadingObject != null)
        {
            reloadingObject.SetActive(isReloading);
        }
    }
}