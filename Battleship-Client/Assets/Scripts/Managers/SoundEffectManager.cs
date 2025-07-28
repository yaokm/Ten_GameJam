using UnityEngine;

public class SoundEffectManager : MonoBehaviour
{
    public static SoundEffectManager Instance { get; private set; }

    [System.Serializable]
    public enum SoundEffectType
    {
        Fire = 0,
        Hit = 1,
        Miss = 2,
        ShipSunk = 3,
        Victory = 4,
        Defeat = 5,
        HeroSelect = 6  // 新增：武将选择音效
    }

    [System.Serializable]
    public enum HeroSkillType
    {
        GuoJia = 0,
        Chengyu = 1,
        Zhugeliang = 2,
        Zhouyu = 3
    }

    [Header("基础音效列表")]
    public AudioClip[] soundEffectClips;

    [Header("武将技能音效列表")]
    public AudioClip[] heroSkillClips;

    [Header("武将选择音效列表")]
    public AudioClip[] heroSelectClips;  // 新增：武将选择音效数组

    [Header("主音量设置")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;

    [Header("分类音量设置")]
    [Range(0f, 1f)]
    public float heroSelectVolume = 1f;      // 武将选择音效音量
    [Range(0f, 1f)]
    public float heroSkillVolume = 1f;       // 武将技能音效音量
    [Range(0f, 1f)]
    public float gamePlayVolume = 1f;        // 局内音效音量（射击、击中、未击中、击沉）
    [Range(0f, 1f)]
    public float resultVolume = 1f;          // 结算音效音量（胜利、失败）

    [Header("音效设置UI")]
    [SerializeField] private bool showVolumeSettings = true;
    [SerializeField] private bool enableVolumeSettings = true;

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
        audioSource.playOnAwake = false;
        UpdateAudioSourceVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // 更新AudioSource音量
    private void UpdateAudioSourceVolume()
    {
        if (audioSource != null)
        {
            audioSource.volume = masterVolume;
        }
    }

    public void PlaySoundEffect(SoundEffectType type)
    {
        PlaySoundEffect((int)type);
    }

    public void PlaySoundEffect(int index)
    {
        if (soundEffectClips == null || index < 0 || index >= soundEffectClips.Length)
        {
            return;
        }

        if (soundEffectClips[index] == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.clip = soundEffectClips[index];
            
            // 根据音效类型设置不同的音量
            float volumeMultiplier = GetVolumeMultiplierForSoundEffect((SoundEffectType)index);
            audioSource.volume = masterVolume * volumeMultiplier;
            
            audioSource.Play();
        }
    }

    // 根据音效类型获取音量倍数
    private float GetVolumeMultiplierForSoundEffect(SoundEffectType type)
    {
        switch (type)
        {
            case SoundEffectType.Fire:
            case SoundEffectType.Hit:
            case SoundEffectType.Miss:
            case SoundEffectType.ShipSunk:
                return gamePlayVolume;  // 局内音效
            case SoundEffectType.Victory:
            case SoundEffectType.Defeat:
                return resultVolume;    // 结算音效
            case SoundEffectType.HeroSelect:
                return heroSelectVolume; // 武将选择音效
            default:
                return 1f;
        }
    }

    public void PlayHeroSkillSound(HeroSkillType heroType)
    {
        if (heroSkillClips == null || (int)heroType < 0 || (int)heroType >= heroSkillClips.Length)
        {
            return;
        }

        if (heroSkillClips[(int)heroType] == null)
        {
            return;
        }

        if (audioSource != null)
        {
            audioSource.clip = heroSkillClips[(int)heroType];
            // 使用武将技能音效音量
            audioSource.volume = masterVolume * heroSkillVolume;
            audioSource.Play();
        }
    }

    public void PlayHeroSkillSoundById(int heroId)
    {
        HeroSkillType heroType = HeroSkillType.GuoJia;
        
        switch (heroId)
        {
            case 1: heroType = HeroSkillType.GuoJia; break;
            case 2: heroType = HeroSkillType.Chengyu; break;
            case 3: heroType = HeroSkillType.Zhugeliang; break;
            case 4: heroType = HeroSkillType.Zhouyu; break;
            default: break;
        }
        
        PlayHeroSkillSound(heroType);
    }

    // 新增：播放武将选择音效（根据武将ID）
    public void PlayHeroSelectSoundById(int heroId)
    {
        if (heroSelectClips == null || heroId < 1 || heroId > heroSelectClips.Length)
        {
            Debug.LogWarning($"无法播放武将 {heroId} 的选择音效：音效数组未设置或武将ID超出范围");
            return;
        }

        AudioClip selectClip = heroSelectClips[heroId - 1]; // 武将ID从1开始，数组索引从0开始
        if (selectClip == null)
        {
            Debug.LogWarning($"武将 {heroId} 的选择音效文件未设置");
            return;
        }

        if (audioSource != null)
        {
            audioSource.clip = selectClip;
            // 使用武将选择音效音量
            audioSource.volume = masterVolume * heroSelectVolume;
            audioSource.Play();
            Debug.Log($"播放武将 {heroId} 的选择音效");
        }
    }

    // 新增：播放武将选择音效
    public void PlayHeroSelectSound()
    {
        if (audioSource != null && soundEffectClips != null && (int)SoundEffectType.HeroSelect < soundEffectClips.Length)
        {
            if (soundEffectClips[(int)SoundEffectType.HeroSelect] != null)
            {
                audioSource.clip = soundEffectClips[(int)SoundEffectType.HeroSelect];
                // 使用武将选择音效音量
                audioSource.volume = masterVolume * heroSelectVolume;
                audioSource.Play();
                Debug.Log("播放通用武将选择音效");
            }
            else
            {
                Debug.LogWarning("通用武将选择音效文件未设置");
            }
        }
        else
        {
            Debug.LogWarning("无法播放通用武将选择音效：音效管理器或音效文件未正确设置");
        }
    }

    // 主音量设置
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        UpdateAudioSourceVolume();
    }

    // 武将选择音效音量设置
    public void SetHeroSelectVolume(float volume)
    {
        heroSelectVolume = Mathf.Clamp01(volume);
    }

    // 武将技能音效音量设置
    public void SetHeroSkillVolume(float volume)
    {
        heroSkillVolume = Mathf.Clamp01(volume);
    }

    // 局内音效音量设置
    public void SetGamePlayVolume(float volume)
    {
        gamePlayVolume = Mathf.Clamp01(volume);
    }

    // 结算音效音量设置
    public void SetResultVolume(float volume)
    {
        resultVolume = Mathf.Clamp01(volume);
    }

    // 获取当前音量设置
    public float GetMasterVolume() => masterVolume;
    public float GetHeroSelectVolume() => heroSelectVolume;
    public float GetHeroSkillVolume() => heroSkillVolume;
    public float GetGamePlayVolume() => gamePlayVolume;
    public float GetResultVolume() => resultVolume;

    // 测试音效功能
    [ContextMenu("测试所有音效音量")]
    public void TestAllSoundVolumes()
    {
        Debug.Log("=== 测试所有音效音量 ===");
        Debug.Log($"主音量: {masterVolume}");
        Debug.Log($"武将选择音效音量: {heroSelectVolume}");
        Debug.Log($"武将技能音效音量: {heroSkillVolume}");
        Debug.Log($"局内音效音量: {gamePlayVolume}");
        Debug.Log($"结算音效音量: {resultVolume}");
        
        // 测试各种音效
        PlaySoundEffect(SoundEffectType.Fire);
        Invoke(nameof(TestHeroSelect), 0.5f);
        Invoke(nameof(TestHeroSkill), 1f);
        Invoke(nameof(TestResult), 1.5f);
    }

    private void TestHeroSelect()
    {
        PlayHeroSelectSoundById(1);
    }

    private void TestHeroSkill()
    {
        PlayHeroSkillSoundById(1);
    }

    private void TestResult()
    {
        PlaySoundEffect(SoundEffectType.Victory);
    }

    public void StopAllSoundEffects()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
} 