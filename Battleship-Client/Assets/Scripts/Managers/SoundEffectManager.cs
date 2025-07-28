using UnityEngine;
using System.Collections.Generic;

public class SoundEffectManager : MonoBehaviour
{
    public static SoundEffectManager Instance { get; private set; }

    [System.Serializable]
    public enum SoundEffectType
    {
        Fire = 0,           // 射击音效
        Hit = 1,            // 击中音效
        Miss = 2,           // 未击中音效
        ShipSunk = 3,       // 船只沉没音效
        Skill = 4,          // 技能音效（通用）
        ButtonClick = 5,    // 按钮点击音效
        TurnSwitch = 6,     // 回合切换音效
        Victory = 7,        // 胜利音效
        Defeat = 8          // 失败音效
    }

    [System.Serializable]
    public enum HeroSkillType
    {
        GuoJia = 0,         // 郭嘉技能音效
        Chengyu = 1,        // 程昱技能音效
        Zhugeliang = 2,     // 诸葛亮技能音效
        Zhouyu = 3,         // 周瑜技能音效
        // 可以继续添加更多武将
    }

    [Header("基础音效列表")]
    public AudioClip[] soundEffectClips;

    [Header("武将技能音效列表")]
    public AudioClip[] heroSkillClips;

    [Header("音效设置")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Range(0f, 1f)]
    public float sfxVolume = 0.8f;

    private AudioSource[] audioSources;
    private const int MAX_AUDIO_SOURCES = 8; // 同时播放的最大音效数量
    private Queue<AudioSource> availableSources;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 创建多个AudioSource用于同时播放多个音效
        audioSources = new AudioSource[MAX_AUDIO_SOURCES];
        availableSources = new Queue<AudioSource>();

        for (int i = 0; i < MAX_AUDIO_SOURCES; i++)
        {
            audioSources[i] = gameObject.AddComponent<AudioSource>();
            audioSources[i].playOnAwake = false;
            audioSources[i].volume = sfxVolume * masterVolume;
            availableSources.Enqueue(audioSources[i]);
        }
    }

    /// <summary>
    /// 播放指定类型的音效
    /// </summary>
    public void PlaySoundEffect(SoundEffectType type)
    {
        PlaySoundEffect((int)type);
    }

    /// <summary>
    /// 播放指定索引的音效
    /// </summary>
    public void PlaySoundEffect(int index)
    {
        if (soundEffectClips == null || index < 0 || index >= soundEffectClips.Length)
        {
            Debug.LogWarning($"SoundEffectManager: 索引 {index} 无效或未设置soundEffectClips");
            return;
        }

        if (soundEffectClips[index] == null)
        {
            Debug.LogWarning($"SoundEffectManager: 索引 {index} 的音效文件为空");
            return;
        }

        if (availableSources.Count == 0)
        {
            Debug.LogWarning($"SoundEffectManager: 没有可用的AudioSource，当前可用: {availableSources.Count}/{MAX_AUDIO_SOURCES}");
            // 强制回收所有正在播放的AudioSource
            ForceRecycleAudioSources();
            if (availableSources.Count == 0)
            {
                Debug.LogError("SoundEffectManager: 强制回收后仍无可用的AudioSource");
                return;
            }
        }

        AudioSource source = availableSources.Dequeue();
        source.clip = soundEffectClips[index];
        source.volume = sfxVolume * masterVolume;
        source.Play();

        Debug.Log($"SoundEffectManager: 播放音效 {index}，剩余可用AudioSource: {availableSources.Count}");

        // 使用协程在音效播放完成后将AudioSource放回队列
        StartCoroutine(ReturnAudioSourceToPool(source));
    }

    /// <summary>
    /// 播放指定音效并等待完成
    /// </summary>
    public System.Collections.IEnumerator PlaySoundEffectAsync(SoundEffectType type)
    {
        if (soundEffectClips == null || (int)type < 0 || (int)type >= soundEffectClips.Length)
        {
            yield break;
        }

        if (availableSources.Count == 0)
        {
            yield break;
        }

        AudioSource source = availableSources.Dequeue();
        source.clip = soundEffectClips[(int)type];
        source.volume = sfxVolume * masterVolume;
        source.Play();

        // 等待音效播放完成
        while (source.isPlaying)
        {
            yield return null;
        }

        // 将AudioSource放回队列
        availableSources.Enqueue(source);
    }

    /// <summary>
    /// 播放指定武将的技能音效
    /// </summary>
    public void PlayHeroSkillSound(HeroSkillType heroType)
    {
        if (heroSkillClips == null || (int)heroType < 0 || (int)heroType >= heroSkillClips.Length)
        {
            Debug.LogWarning($"SoundEffectManager: 武将技能音效索引 {(int)heroType} 无效或未设置heroSkillClips");
            return;
        }

        if (heroSkillClips[(int)heroType] == null)
        {
            Debug.LogWarning($"SoundEffectManager: 武将技能音效索引 {(int)heroType} 的音效文件为空");
            return;
        }

        if (availableSources.Count == 0)
        {
            Debug.LogWarning($"SoundEffectManager: 没有可用的AudioSource，当前可用: {availableSources.Count}/{MAX_AUDIO_SOURCES}");
            // 强制回收所有正在播放的AudioSource
            ForceRecycleAudioSources();
            if (availableSources.Count == 0)
            {
                Debug.LogError("SoundEffectManager: 强制回收后仍无可用的AudioSource");
                return;
            }
        }

        AudioSource source = availableSources.Dequeue();
        source.clip = heroSkillClips[(int)heroType];
        source.volume = sfxVolume * masterVolume;
        source.Play();

        Debug.Log($"SoundEffectManager: 播放武将技能音效 {heroType}，剩余可用AudioSource: {availableSources.Count}");

        // 使用协程在音效播放完成后将AudioSource放回队列
        StartCoroutine(ReturnAudioSourceToPool(source));
    }

    /// <summary>
    /// 播放指定武将的技能音效并等待完成
    /// </summary>
    public System.Collections.IEnumerator PlayHeroSkillSoundAsync(HeroSkillType heroType)
    {
        if (heroSkillClips == null || (int)heroType < 0 || (int)heroType >= heroSkillClips.Length)
        {
            yield break;
        }

        if (availableSources.Count == 0)
        {
            yield break;
        }

        AudioSource source = availableSources.Dequeue();
        source.clip = heroSkillClips[(int)heroType];
        source.volume = sfxVolume * masterVolume;
        source.Play();

        // 等待音效播放完成
        while (source.isPlaying)
        {
            yield return null;
        }

        // 将AudioSource放回队列
        availableSources.Enqueue(source);
    }

    /// <summary>
    /// 根据武将ID播放对应的技能音效
    /// </summary>
    public void PlayHeroSkillSoundById(int heroId)
    {
        // 武将ID映射到技能音效类型
        // 武将ID: 1=郭嘉, 2=程昱, 3=诸葛亮, 4=周瑜
        HeroSkillType heroType = HeroSkillType.GuoJia; // 默认郭嘉
        
        switch (heroId)
        {
            case 1:
                heroType = HeroSkillType.GuoJia;
                break;
            case 2:
                heroType = HeroSkillType.Chengyu;
                break;
            case 3:
                heroType = HeroSkillType.Zhugeliang;
                break;
            case 4:
                heroType = HeroSkillType.Zhouyu;
                break;
            default:
                Debug.LogWarning($"SoundEffectManager: 未知的武将ID {heroId}，使用默认音效");
                break;
        }
        
        PlayHeroSkillSound(heroType);
    }

    /// <summary>
    /// 根据武将ID播放对应的技能音效并等待完成
    /// </summary>
    public System.Collections.IEnumerator PlayHeroSkillSoundByIdAsync(int heroId)
    {
        // 武将ID映射到技能音效类型
        HeroSkillType heroType = HeroSkillType.GuoJia; // 默认郭嘉
        
        switch (heroId)
        {
            case 1:
                heroType = HeroSkillType.GuoJia;
                break;
            case 2:
                heroType = HeroSkillType.Chengyu;
                break;
            case 3:
                heroType = HeroSkillType.Zhugeliang;
                break;
            case 4:
                heroType = HeroSkillType.Zhouyu;
                break;
            default:
                Debug.LogWarning($"SoundEffectManager: 未知的武将ID {heroId}，使用默认音效");
                break;
        }
        
        yield return StartCoroutine(PlayHeroSkillSoundAsync(heroType));
    }

    /// <summary>
    /// 设置主音量
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAllAudioSourcesVolume();
    }

    /// <summary>
    /// 设置音效音量
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        UpdateAllAudioSourcesVolume();
    }

    /// <summary>
    /// 停止所有音效
    /// </summary>
    public void StopAllSoundEffects()
    {
        foreach (var source in audioSources)
        {
            if (source.isPlaying)
            {
                source.Stop();
            }
        }
        // 重新填充可用池
        ForceRecycleAudioSources();
    }

    /// <summary>
    /// 调试：打印当前音效管理器状态
    /// </summary>
    [ContextMenu("调试音效管理器状态")]
    public void DebugSoundEffectManagerStatus()
    {
        Debug.Log($"=== SoundEffectManager 状态 ===");
        Debug.Log($"基础音效数量: {(soundEffectClips != null ? soundEffectClips.Length : 0)}");
        Debug.Log($"武将技能音效数量: {(heroSkillClips != null ? heroSkillClips.Length : 0)}");
        Debug.Log($"主音量: {masterVolume}");
        Debug.Log($"音效音量: {sfxVolume}");
        Debug.Log($"AudioSource状态: {GetAudioSourceStatus()}");
        
        // 检查音效文件
        if (soundEffectClips != null)
        {
            for (int i = 0; i < soundEffectClips.Length; i++)
            {
                if (soundEffectClips[i] == null)
                {
                    Debug.LogWarning($"基础音效索引 {i} 为空");
                }
            }
        }
        
        if (heroSkillClips != null)
        {
            for (int i = 0; i < heroSkillClips.Length; i++)
            {
                if (heroSkillClips[i] == null)
                {
                    Debug.LogWarning($"武将技能音效索引 {i} 为空");
                }
            }
        }
    }

    private void UpdateAllAudioSourcesVolume()
    {
        foreach (var source in audioSources)
        {
            source.volume = sfxVolume * masterVolume;
        }
    }

    /// <summary>
    /// 强制回收所有AudioSource到池中
    /// </summary>
    private void ForceRecycleAudioSources()
    {
        availableSources.Clear();
        foreach (var source in audioSources)
        {
            if (source != null)
            {
                source.Stop();
                availableSources.Enqueue(source);
            }
        }
        Debug.Log($"SoundEffectManager: 强制回收完成，可用AudioSource: {availableSources.Count}");
    }

    /// <summary>
    /// 获取当前AudioSource状态信息
    /// </summary>
    public string GetAudioSourceStatus()
    {
        int playingCount = 0;
        int availableCount = availableSources.Count;
        
        foreach (var source in audioSources)
        {
            if (source.isPlaying)
            {
                playingCount++;
            }
        }
        
        return $"总AudioSource: {MAX_AUDIO_SOURCES}, 正在播放: {playingCount}, 可用: {availableCount}";
    }

    private System.Collections.IEnumerator ReturnAudioSourceToPool(AudioSource source)
    {
        if (source.clip != null)
        {
            yield return new WaitForSeconds(source.clip.length);
        }
        else
        {
            yield return new WaitForSeconds(0.1f); // 默认等待时间
        }
        
        if (source != null && !availableSources.Contains(source))
        {
            availableSources.Enqueue(source);
            Debug.Log($"SoundEffectManager: AudioSource已回收到池中，当前可用: {availableSources.Count}");
        }
    }
} 