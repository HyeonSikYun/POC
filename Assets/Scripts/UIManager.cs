using UnityEngine.UI;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("탄약 UI")]
    public TextMeshProUGUI ammoText;
    public Slider ammoSlider;

    [Header("무기 UI")]
    public TextMeshProUGUI weaponNameText;
    public Image weaponIcon;

    [Header("무기 아이콘")]
    public Sprite heavyMGIcon;
    public Sprite shotgunIcon;
    public Sprite bazookaIcon;

    [Header("장전 UI")]
    public GameObject reloadingPanel;
    public TextMeshProUGUI reloadingText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void UpdateAmmo(int current, int total)
    {
        if (ammoText != null)
        {
            ammoText.text = $"{current} / {total}";
        }

        if (ammoSlider != null)
        {
            ammoSlider.maxValue = total;
            ammoSlider.value = current;
        }
    }

    public void UpdateWeaponType(string weaponName)
    {
        if (weaponNameText != null)
        {
            string displayName = weaponName switch
            {
                "HeavyMG" => "헤비 머신건",
                "Shotgun" => "샷건",
                "Bazooka" => "바주카포",
                _ => weaponName
            };
            weaponNameText.text = displayName;
        }

        if (weaponIcon != null)
        {
            weaponIcon.sprite = weaponName switch
            {
                "HeavyMG" => heavyMGIcon,
                "Shotgun" => shotgunIcon,
                "Bazooka" => bazookaIcon,
                _ => null
            };
        }
    }

    public void ShowReloading(bool show)
    {
        if (reloadingPanel != null)
        {
            reloadingPanel.SetActive(show);
        }
    }
}