using UnityEngine;

public class SoundEffectManagerTest : MonoBehaviour
{
    [Header("测试设置")]
    [SerializeField] private bool testOnStart = false;
    [SerializeField] private KeyCode testVictoryKey = KeyCode.V;
    [SerializeField] private KeyCode testDefeatKey = KeyCode.D;
    [SerializeField] private KeyCode testButtonClickKey = KeyCode.B;
    [SerializeField] private KeyCode testShipSunkKey = KeyCode.S;

    private void Start()
    {
        if (testOnStart)
        {
            TestSoundEffectManager();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(testVictoryKey))
        {
            TestVictorySound();
        }
        
        if (Input.GetKeyDown(testDefeatKey))
        {
            TestDefeatSound();
        }
        
        if (Input.GetKeyDown(testButtonClickKey))
        {
            TestButtonClickSound();
        }
        
        if (Input.GetKeyDown(testShipSunkKey))
        {
            TestShipSunkSound();
        }
    }

    [ContextMenu("测试音效管理器")]
    public void TestSoundEffectManager()
    {
        if (SoundEffectManager.Instance == null)
        {
            Debug.LogError("SoundEffectManager 实例不存在！");
            return;
        }

        Debug.Log("=== 音效管理器测试 ===");
        Debug.Log($"SoundEffectManager 实例: {SoundEffectManager.Instance}");
        Debug.Log($"基础音效数量: {SoundEffectManager.Instance.soundEffectClips?.Length ?? 0}");
        Debug.Log($"武将技能音效数量: {SoundEffectManager.Instance.heroSkillClips?.Length ?? 0}");
        
        // 按钮点击音效已移除
        // SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.ButtonClick);
        Debug.Log("按钮点击音效已移除");
    }

    [ContextMenu("测试胜利音效")]
    public void TestVictorySound()
    {
        if (SoundEffectManager.Instance != null)
        {
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.Victory);
            Debug.Log("测试播放胜利音效");
        }
    }

    [ContextMenu("测试失败音效")]
    public void TestDefeatSound()
    {
        if (SoundEffectManager.Instance != null)
        {
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.Defeat);
            Debug.Log("测试播放失败音效");
        }
    }

    [ContextMenu("测试按钮点击音效")]
    public void TestButtonClickSound()
    {
        // 按钮点击音效已移除
        // if (SoundEffectManager.Instance != null)
        // {
        //     SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.ButtonClick);
        //     Debug.Log("测试播放按钮点击音效");
        // }
        Debug.Log("按钮点击音效已移除");
    }

    [ContextMenu("测试击沉音效")]
    public void TestShipSunkSound()
    {
        if (SoundEffectManager.Instance != null)
        {
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.ShipSunk);
            Debug.Log("测试播放击沉音效");
        }
    }
} 