using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSoundEffect : MonoBehaviour
{
    [Header("音效设置")]
    [SerializeField] public bool enableClickSound = true;
    [SerializeField] public bool enableHoverSound = false; // 可选：鼠标悬停音效

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button != null)
        {
            _button.onClick.AddListener(PlayClickSound);
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(PlayClickSound);
        }
    }

    /// <summary>
    /// 播放按钮点击音效
    /// </summary>
    private void PlayClickSound()
    {
        if (!enableClickSound) return;
        
        if (SoundEffectManager.Instance != null)
        {
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.ButtonClick);
        }
    }

    /// <summary>
    /// 手动播放点击音效（用于外部调用）
    /// </summary>
    public void PlayClickSoundManually()
    {
        PlayClickSound();
    }

    /// <summary>
    /// 启用/禁用点击音效
    /// </summary>
    public void SetClickSoundEnabled(bool enabled)
    {
        enableClickSound = enabled;
    }
} 