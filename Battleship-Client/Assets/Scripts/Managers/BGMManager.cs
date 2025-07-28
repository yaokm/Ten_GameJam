using UnityEngine;

public class BGMManager : MonoBehaviour
{
    public static BGMManager Instance { get; private set; }

    public enum BGMType
    {
        Loading = 0,
        Battle = 1
    }

    [Header("BGM 音乐列表")]
    public AudioClip[] bgmClips;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.loop = true;
    }

    /// <summary>
    /// 播放指定索引的BGM
    /// </summary>
    public void PlayBGM(int index)
    {
        if (bgmClips == null || index < 0 || index >= bgmClips.Length)
        {
            Debug.LogWarning($"BGMManager: 索引 {index} 无效或未设置bgmClips");
            return;
        }
        if (audioSource.clip == bgmClips[index] && audioSource.isPlaying)
            return;
        audioSource.clip = bgmClips[index];
        audioSource.Play();
    }

    /// <summary>
    /// 播放指定类型的BGM
    /// </summary>
    public void PlayBGM(BGMType type)
    {
        PlayBGM((int)type);
    }

    /// <summary>
    /// 停止播放BGM
    /// </summary>
    public void StopBGM()
    {
        audioSource.Stop();
    }

    /// <summary>
    /// 设置BGM音量
    /// </summary>
    public void SetVolume(float volume)
    {
        audioSource.volume = Mathf.Clamp01(volume);
    }

    /// <summary>
    /// 暂停BGM（用于游戏胜负时给音效腾出空间）
    /// </summary>
    public void PauseBGM()
    {
        audioSource.Pause();
    }

    /// <summary>
    /// 恢复BGM播放
    /// </summary>
    public void ResumeBGM()
    {
        audioSource.UnPause();
    }
} 