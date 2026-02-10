using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class EndingSceneManager : MonoBehaviour
{
    public static EndingSceneManager Instance;
    public AudioClip calmEndingBGM;

    [Header("화이트 엔딩 설정")]
    public Image fadeImage; // UIManager의 FadeCanvas에 있는 Image를 꼭 연결해주세요.
    private bool isEndingTriggered = false;

    [Header("엔딩 크레딧 설정")]
    public GameObject endingCreditCanvas; // 아까 만든 EndingCreditCanvas 연결
    public EndingCreditScroller creditScroller; // 그 안에 있는 스크립트 연결

    private void Awake() { Instance = this; }

    public void TriggerWhiteEnding()
    {
        if (isEndingTriggered) return;
        isEndingTriggered = true;

        // 1. 플레이어 이동 및 조작 완전 차단
        FreezePlayer();

        // 2. 화이트 페이드 아웃 시퀀스 시작
        StartCoroutine(WhiteFadeOutRoutine());
    }

    private void FreezePlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // CharacterController를 꺼서 물리 이동 차단
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc) cc.enabled = false;

            // PlayerController(입력 스크립트)를 꺼서 마우스 회전 및 키보드 입력 차단
            var pc = player.GetComponent<PlayerController>();
            if (pc) pc.enabled = false;

            // (선택) 카메라 회전 스크립트가 따로 있다면 그것도 꺼야 합니다.
            Debug.Log("플레이어 이동 및 조작이 차단되었습니다.");
        }
    }

    IEnumerator WhiteFadeOutRoutine()
    {
        if (UIManager.Instance != null)
        {
            // [중요] 페이드 이미지의 색상을 코드로 직접 하얀색으로 변경
            // UIManager 내부에서 이미지를 찾아서 색상을 바꿉니다.
            if (fadeImage == null)
            {
                // 인스펙터에서 연결 안 했을 경우를 대비해 찾기 시도
                fadeImage = GameObject.Find("FadeImage")?.GetComponent<Image>();
            }

            if (fadeImage != null)
            {
                fadeImage.color = Color.white; // 여기서 확실하게 하얀색으로 설정
                Debug.Log("페이드 색상을 하얀색으로 변경했습니다.");
            }

            // UIManager의 페이드 아웃 실행 (0 -> 1)
            // UIManager.FadeOut() 내부에서 색상을 검은색으로 초기화하는 코드가 있는지 확인해보세요.
            yield return StartCoroutine(UIManager.Instance.FadeOut());
        }

        // 3. 완전히 하얘진 후 처리 (예: 타이틀 이동)
        yield return new WaitForSeconds(2.0f);
        if (endingCreditCanvas != null)
        {
            // 페이드 이미지가 덮고 있으니, 크레딧 캔버스를 켜서 그 위로 덮어씌움
            endingCreditCanvas.SetActive(true);

            if (creditScroller != null)
            {
                creditScroller.StartScroll();
            }
        }
    }
}