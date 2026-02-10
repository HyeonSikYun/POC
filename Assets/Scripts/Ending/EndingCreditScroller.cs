using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // 씬 이동 필수
using System.Collections;

public class EndingCreditScroller : MonoBehaviour
{
    public RectTransform scrollContent; // 위로 올라갈 내용물
    public float scrollSpeed = 50f;     // 속도 (천천히 올라가야 분위기 있음)

    // 로고가 화면 중앙에 멈추길 원하면 이 값을 조정 (예: 1500), 
    // 화면 위로 싹 다 올라가길 원하면 더 큰 값 입력
    public float targetPosY = 2000f;

    public void StartScroll()
    {
        StartCoroutine(ScrollRoutine());
    }

    IEnumerator ScrollRoutine()
    {
        // 1. 목표 높이까지 스크롤 올리기
        // (현재 Y값이 목표값보다 작을 동안 계속 실행)
        while (scrollContent.anchoredPosition.y < targetPosY)
        {
            scrollContent.anchoredPosition += Vector2.up * scrollSpeed * Time.deltaTime;
            yield return null;
        }

        // 2. 다 올라가면(혹은 로고가 멈추면) 잠시 대기 (여운)
        Debug.Log("크레딧 스크롤 종료. 3초 대기 후 재시작합니다.");
        yield return new WaitForSeconds(1.0f);

        // 3. 게임 완전 초기화 및 재시작
        RestartGame();
    }

    private void RestartGame()
    {
        Debug.Log("?? 게임 리셋 및 튜토리얼 재시작");

        // 1. 시간 정상화
        Time.timeScale = 1f;

        // 2. [중요] 살아있는 싱글톤 매니저들 강제 삭제
        // (삭제하지 않으면 재시작했을 때 예전 데이터가 남아서 꼬임)
        if (GameManager.Instance != null) Destroy(GameManager.Instance.gameObject);
        if (SoundManager.Instance != null) Destroy(SoundManager.Instance.gameObject);
        if (UIManager.Instance != null) Destroy(UIManager.Instance.gameObject);
        if (EndingSceneManager.Instance != null) Destroy(EndingSceneManager.Instance.gameObject);
        // 혹시 InventoryManager나 QuestManager가 있다면 여기 추가하세요.

        // 3. 현재 씬(MainScene)을 다시 로드 -> 튜토리얼 상태로 시작됨
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}