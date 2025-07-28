using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SoundEffectManagerSetup : MonoBehaviour
{
    [Header("基础音效文件设置")]
    [SerializeField] private AudioClip fireSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip missSound;
    [SerializeField] private AudioClip shipSunkSound;
    [SerializeField] private AudioClip skillSound;
    [SerializeField] private AudioClip buttonClickSound;
    [SerializeField] private AudioClip turnSwitchSound;
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private AudioClip defeatSound;
    [SerializeField] private AudioClip heroSelectSound;  // 通用武将选择音效

    [Header("武将技能音效文件设置")]
    [SerializeField] private AudioClip guoJiaSkillSound;
    [SerializeField] private AudioClip chengyuSkillSound;
    [SerializeField] private AudioClip zhugeliangSkillSound;
    [SerializeField] private AudioClip zhouyuSkillSound;

    [Header("武将选择音效文件设置")]
    [SerializeField] private AudioClip chooseGuoJia;     // 郭嘉选择音效
    [SerializeField] private AudioClip chooseChengyu;    // 程昱选择音效
    [SerializeField] private AudioClip chooseZhugeliang; // 诸葛亮选择音效
    [SerializeField] private AudioClip chooseZhouyu;     // 周瑜选择音效

#if UNITY_EDITOR
    [ContextMenu("设置音效管理器")]
    public void SetupSoundEffectManager()
    {
        // 查找或创建SoundEffectManager
        SoundEffectManager soundManager = FindObjectOfType<SoundEffectManager>();
        if (soundManager == null)
        {
            GameObject managerObject = new GameObject("SoundEffectManager");
            soundManager = managerObject.AddComponent<SoundEffectManager>();
            Debug.Log("已创建SoundEffectManager");
        }

        // 设置基础音效数组（现在包含10个音效）
        AudioClip[] clips = new AudioClip[10];
        clips[0] = fireSound;
        clips[1] = hitSound;
        clips[2] = missSound;
        clips[3] = shipSunkSound;
        clips[4] = victorySound;
        clips[5] = defeatSound;
        clips[6] = heroSelectSound;  // 通用武将选择音效
        // 注意：skillSound, buttonClickSound, turnSwitchSound 已移除或不再使用

        // 设置武将技能音效数组
        AudioClip[] heroClips = new AudioClip[4];
        heroClips[0] = guoJiaSkillSound;
        heroClips[1] = chengyuSkillSound;
        heroClips[2] = zhugeliangSkillSound;
        heroClips[3] = zhouyuSkillSound;

        // 设置武将选择音效数组
        AudioClip[] heroSelectClips = new AudioClip[4];
        heroSelectClips[0] = chooseGuoJia;
        heroSelectClips[1] = chooseChengyu;
        heroSelectClips[2] = chooseZhugeliang;
        heroSelectClips[3] = chooseZhouyu;

        // 直接设置public字段
        soundManager.soundEffectClips = clips;
        soundManager.heroSkillClips = heroClips;
        soundManager.heroSelectClips = heroSelectClips;  // 新增：武将选择音效数组

        // 标记为已修改
        EditorUtility.SetDirty(soundManager);
        Debug.Log("音效管理器设置完成！");
        Debug.Log($"基础音效数量: {clips.Length}");
        Debug.Log($"武将技能音效数量: {heroClips.Length}");
        Debug.Log($"武将选择音效数量: {heroSelectClips.Length}");
    }

    [ContextMenu("创建音效管理器预制体")]
    public void CreateSoundEffectManagerPrefab()
    {
        // 查找SoundEffectManager
        SoundEffectManager soundManager = FindObjectOfType<SoundEffectManager>();
        if (soundManager == null)
        {
            Debug.LogError("未找到SoundEffectManager，请先运行'设置音效管理器'");
            return;
        }

        // 创建预制体
        string prefabPath = "Assets/Prefabs/SoundEffectManager.prefab";
        
        // 确保目录存在
        string directory = System.IO.Path.GetDirectoryName(prefabPath);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        // 创建预制体
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(soundManager.gameObject, prefabPath);
        Debug.Log($"音效管理器预制体已创建: {prefabPath}");
    }
#endif
} 