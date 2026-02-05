using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // TextMeshPro를 쓴다면 필수 (기본 Text라면 UnityEngine.UI 사용)

public class GraphicSettings : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TMP_Dropdown resolutionDropdown; // 해상도 드롭다운
    [SerializeField] private TMP_Dropdown displayModeDropdown; // 전체화면/창모드 드롭다운

    private Resolution[] resolutions; // 지원 가능한 해상도 목록
    private List<Resolution> filteredResolutions; // 중복 제거된 해상도 목록

    private float currentRefreshRate;
    private int currentResolutionIndex = 0;

    void Start()
    {
        // 1. 디스플레이 모드 옵션을 언어에 맞춰 생성
        RefreshDisplayModeOptions();

        // 2. 해상도 초기화
        InitResolution();
    }

    // =========================================================
    // 디스플레이 모드 (전체화면 / 창모드)
    // =========================================================
    public void RefreshDisplayModeOptions()
    {
        // 1. 현재 선택된 모드 번호 저장 (0: 전체, 1: 창)
        int currentMode = PlayerPrefs.GetInt("DisplayMode", 0);

        // 2. 기존 옵션 싹 지우기
        displayModeDropdown.ClearOptions();

        // 3. 언어 매니저에서 단어 가져와서 리스트 만들기
        List<string> options = new List<string>();

        if (LanguageManager.Instance != null)
        {
            options.Add(LanguageManager.Instance.GetText("Opt_DisplayFull"));   // 전체화면
            options.Add(LanguageManager.Instance.GetText("Opt_DisplayWindow")); // 창모드
        }
        else
        {
            // 매니저가 없을 때를 대비한 기본값
            options.Add("Fullscreen");
            options.Add("Windowed");
        }

        // 4. 드롭다운에 새 옵션 넣기
        displayModeDropdown.AddOptions(options);

        // 5. 아까 저장해둔 선택값 복구
        displayModeDropdown.value = currentMode;
        displayModeDropdown.RefreshShownValue();
    }

    public void SetDisplayMode(int index)
    {
        // index 0: 전체화면, 1: 창모드
        bool isFullscreen = (index == 0);
        Screen.fullScreen = isFullscreen;

        // 전체화면 모드 변경 (ExclusiveFullScreen이 가장 안정적)
        Screen.fullScreenMode = isFullscreen ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.Windowed;

        PlayerPrefs.SetInt("DisplayMode", index);
    }

    // =========================================================
    // 해상도 (자동 감지 및 설정)
    // =========================================================
    void InitResolution()
    {
        resolutions = Screen.resolutions;

        // 중복 제거를 위해 기존 리스트 초기화
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResIndex = 0;

        // [핵심 수정] HashSet을 사용하여 "가로x세로"가 이미 등록되었는지 확인 (중복 제거)
        HashSet<string> addedResolutions = new HashSet<string>();
        filteredResolutions = new List<Resolution>();

        for (int i = 0; i < resolutions.Length; i++)
        {
            // 1. 해상도 문자열 생성 (예: "1920 x 1080")
            string option = resolutions[i].width + " x " + resolutions[i].height;

            // 2. 이미 등록된 해상도라면 건너뜀 (주사율만 다른 경우 무시)
            if (addedResolutions.Contains(option)) continue;

            // 3. 새로 발견한 해상도라면 목록에 추가
            addedResolutions.Add(option);
            options.Add(option);
            filteredResolutions.Add(resolutions[i]);

            // 4. 현재 내 화면 크기와 같다면 인덱스 저장
            if (resolutions[i].width == Screen.width &&
                resolutions[i].height == Screen.height)
            {
                currentResIndex = filteredResolutions.Count - 1; // 방금 추가한 게 현재 해상도
            }
        }

        // 드롭다운에 옵션 추가 및 현재값 선택
        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = PlayerPrefs.GetInt("ResolutionIndex", currentResIndex);
        resolutionDropdown.RefreshShownValue();
    }

    public void SetResolution(int resolutionIndex)
    {
        Resolution resolution = filteredResolutions[resolutionIndex];

        // 마지막 인자는 전체화면 여부 (현재 설정 따라감)
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);

        PlayerPrefs.SetInt("ResolutionIndex", resolutionIndex);
    }
}