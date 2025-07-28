using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SoundEffectSettingsUI : MonoBehaviour
{
    [Header("音量滑块")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider heroSelectVolumeSlider;
    [SerializeField] private Slider heroSkillVolumeSlider;
    [SerializeField] private Slider gamePlayVolumeSlider;
    [SerializeField] private Slider resultVolumeSlider;

    [Header("音量显示文本")]
    [SerializeField] private TextMeshProUGUI masterVolumeText;
    [SerializeField] private TextMeshProUGUI heroSelectVolumeText;
    [SerializeField] private TextMeshProUGUI heroSkillVolumeText;
    [SerializeField] private TextMeshProUGUI gamePlayVolumeText;
    [SerializeField] private TextMeshProUGUI resultVolumeText;

    [Header("测试按钮")]
    [SerializeField] private Button testHeroSelectButton;
    [SerializeField] private Button testHeroSkillButton;
    [SerializeField] private Button testGamePlayButton;
    [SerializeField] private Button testResultButton;

    private SoundEffectManager soundManager;

    private void Start()
    {
        soundManager = SoundEffectManager.Instance;
        if (soundManager == null)
        {
            Debug.LogError("SoundEffectManager 实例不存在！");
            return;
        }

        InitializeSliders();
        SetupButtonListeners();
        UpdateVolumeDisplays();
    }

    private void InitializeSliders()
    {
        // 初始化主音量滑块
        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = soundManager.GetMasterVolume();
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        // 初始化武将选择音效音量滑块
        if (heroSelectVolumeSlider != null)
        {
            heroSelectVolumeSlider.value = soundManager.GetHeroSelectVolume();
            heroSelectVolumeSlider.onValueChanged.AddListener(OnHeroSelectVolumeChanged);
        }

        // 初始化武将技能音效音量滑块
        if (heroSkillVolumeSlider != null)
        {
            heroSkillVolumeSlider.value = soundManager.GetHeroSkillVolume();
            heroSkillVolumeSlider.onValueChanged.AddListener(OnHeroSkillVolumeChanged);
        }

        // 初始化局内音效音量滑块
        if (gamePlayVolumeSlider != null)
        {
            gamePlayVolumeSlider.value = soundManager.GetGamePlayVolume();
            gamePlayVolumeSlider.onValueChanged.AddListener(OnGamePlayVolumeChanged);
        }

        // 初始化结算音效音量滑块
        if (resultVolumeSlider != null)
        {
            resultVolumeSlider.value = soundManager.GetResultVolume();
            resultVolumeSlider.onValueChanged.AddListener(OnResultVolumeChanged);
        }
    }

    private void SetupButtonListeners()
    {
        // 设置测试按钮监听器
        if (testHeroSelectButton != null)
        {
            testHeroSelectButton.onClick.AddListener(TestHeroSelectSound);
        }

        if (testHeroSkillButton != null)
        {
            testHeroSkillButton.onClick.AddListener(TestHeroSkillSound);
        }

        if (testGamePlayButton != null)
        {
            testGamePlayButton.onClick.AddListener(TestGamePlaySound);
        }

        if (testResultButton != null)
        {
            testResultButton.onClick.AddListener(TestResultSound);
        }
    }

    // 音量改变事件处理
    private void OnMasterVolumeChanged(float value)
    {
        soundManager.SetMasterVolume(value);
        UpdateVolumeDisplays();
    }

    private void OnHeroSelectVolumeChanged(float value)
    {
        soundManager.SetHeroSelectVolume(value);
        UpdateVolumeDisplays();
    }

    private void OnHeroSkillVolumeChanged(float value)
    {
        soundManager.SetHeroSkillVolume(value);
        UpdateVolumeDisplays();
    }

    private void OnGamePlayVolumeChanged(float value)
    {
        soundManager.SetGamePlayVolume(value);
        UpdateVolumeDisplays();
    }

    private void OnResultVolumeChanged(float value)
    {
        soundManager.SetResultVolume(value);
        UpdateVolumeDisplays();
    }

    // 更新音量显示
    private void UpdateVolumeDisplays()
    {
        if (masterVolumeText != null)
        {
            masterVolumeText.text = $"主音量: {soundManager.GetMasterVolume():F2}";
        }

        if (heroSelectVolumeText != null)
        {
            heroSelectVolumeText.text = $"武将选择: {soundManager.GetHeroSelectVolume():F2}";
        }

        if (heroSkillVolumeText != null)
        {
            heroSkillVolumeText.text = $"武将技能: {soundManager.GetHeroSkillVolume():F2}";
        }

        if (gamePlayVolumeText != null)
        {
            gamePlayVolumeText.text = $"局内音效: {soundManager.GetGamePlayVolume():F2}";
        }

        if (resultVolumeText != null)
        {
            resultVolumeText.text = $"结算音效: {soundManager.GetResultVolume():F2}";
        }
    }

    // 测试音效功能
    private void TestHeroSelectSound()
    {
        if (soundManager != null)
        {
            soundManager.PlayHeroSelectSoundById(1);
        }
    }

    private void TestHeroSkillSound()
    {
        if (soundManager != null)
        {
            soundManager.PlayHeroSkillSoundById(1);
        }
    }

    private void TestGamePlaySound()
    {
        if (soundManager != null)
        {
            soundManager.PlaySoundEffect(SoundEffectManager.SoundEffectType.Fire);
        }
    }

    private void TestResultSound()
    {
        if (soundManager != null)
        {
            soundManager.PlaySoundEffect(SoundEffectManager.SoundEffectType.Victory);
        }
    }

    // 重置所有音量为默认值
    [ContextMenu("重置所有音量为默认值")]
    public void ResetAllVolumes()
    {
        if (soundManager != null)
        {
            soundManager.SetMasterVolume(1f);
            soundManager.SetHeroSelectVolume(1f);
            soundManager.SetHeroSkillVolume(1f);
            soundManager.SetGamePlayVolume(1f);
            soundManager.SetResultVolume(1f);

            // 更新滑块值
            if (masterVolumeSlider != null) masterVolumeSlider.value = 1f;
            if (heroSelectVolumeSlider != null) heroSelectVolumeSlider.value = 1f;
            if (heroSkillVolumeSlider != null) heroSkillVolumeSlider.value = 1f;
            if (gamePlayVolumeSlider != null) gamePlayVolumeSlider.value = 1f;
            if (resultVolumeSlider != null) resultVolumeSlider.value = 1f;

            UpdateVolumeDisplays();
            Debug.Log("所有音量已重置为默认值");
        }
    }

    // 静音所有音效
    [ContextMenu("静音所有音效")]
    public void MuteAllSounds()
    {
        if (soundManager != null)
        {
            soundManager.SetMasterVolume(0f);
            soundManager.SetHeroSelectVolume(0f);
            soundManager.SetHeroSkillVolume(0f);
            soundManager.SetGamePlayVolume(0f);
            soundManager.SetResultVolume(0f);

            // 更新滑块值
            if (masterVolumeSlider != null) masterVolumeSlider.value = 0f;
            if (heroSelectVolumeSlider != null) heroSelectVolumeSlider.value = 0f;
            if (heroSkillVolumeSlider != null) heroSkillVolumeSlider.value = 0f;
            if (gamePlayVolumeSlider != null) gamePlayVolumeSlider.value = 0f;
            if (resultVolumeSlider != null) resultVolumeSlider.value = 0f;

            UpdateVolumeDisplays();
            Debug.Log("所有音效已静音");
        }
    }
} 