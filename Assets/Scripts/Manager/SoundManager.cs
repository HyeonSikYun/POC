using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Source")]
    [SerializeField] AudioSource musicSource;
    [SerializeField] AudioSource SFXSource;
    [SerializeField] AudioSource UISource;

    [Header("Audio Clip")]
    public AudioClip mainBgm;
    public AudioClip tutorialBgm;
    public AudioClip calmEndingBGM;
    public AudioClip btnClick;
    public AudioClip gunPickup;
    public AudioClip Rifle;
    public AudioClip Bazooka;
    public AudioClip flameThrower;
    public AudioClip explosion;
    public AudioClip reload;
    public AudioClip zombieChase;
    public AudioClip generateOn;
    public AudioClip elevatorAmbience;
    public AudioClip elevatorDing;
    public AudioClip footStep;
    public AudioClip gunHit;
    public AudioClip zombieExplosion;
    public AudioClip kingZombieSound;
    public AudioClip zombieChase2;
    public AudioClip zombieChase3;
    public AudioClip zombieDie;
    public AudioClip zombieDie2;
    public AudioClip zombieDie3;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        UISource.ignoreListenerPause = true;
        // 게임 시작 시 튜토리얼 BGM 재생
        PlayBGM(tutorialBgm);
    }

    // 1. 효과음 재생 (기존)
    public void PlaySFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null) SFXSource.PlayOneShot(clip, volume);
    }

    // 2. [추가됨] 모든 효과음 즉시 정지
    // PlayOneShot으로 재생 중인 소리들이 뚝 끊깁니다.
    public void StopSFX()
    {
        SFXSource.Stop();
    }

    // 3. [추가됨] BGM 재생 (엘리베이터 웅~ 소리용)
    // 기존 BGM을 끄고 새로운 걸 반복 재생합니다.
    public void PlayBGM(AudioClip clip)
    {
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true; // BGM은 무조건 무한반복
        musicSource.Play();
    }

    // 4. [추가됨] BGM 정지
    public void StopBGM()
    {
        musicSource.Stop();
    }

    public void PlayUISFX(AudioClip clip, float volume = 1.0f)
    {
        if (clip != null && UISource != null)
        {
            UISource.PlayOneShot(clip, volume);
        }
    }

    // 3. [추가] 게임 소리 일시정지 (BGM + SFX)
    public void PauseAllGameSounds()
    {
        musicSource.Pause();
        AudioListener.pause = true;
    }

    // 4. [추가] 게임 소리 재개
    public void ResumeAllGameSounds()
    {
        musicSource.UnPause();
        AudioListener.pause = false;
    }
}