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
        Defeat = 5
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

    [Header("音效设置")]
    [Range(0f, 1f)]
    public float masterVolume = 1f;
    [Range(0f, 1f)]
    public float sfxVolume = 0.8f;

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
        audioSource.volume = sfxVolume * masterVolume;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
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
            audioSource.volume = sfxVolume * masterVolume;
            audioSource.Play();
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
            audioSource.volume = sfxVolume * masterVolume;
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

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = sfxVolume * masterVolume;
        }
    }

    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        if (audioSource != null)
        {
            audioSource.volume = sfxVolume * masterVolume;
        }
    }

    public void StopAllSoundEffects()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }
} 