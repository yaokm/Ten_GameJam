using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class ButtonSoundEffectSetup : MonoBehaviour
{
    [Header("批量设置")]
    [SerializeField] private bool includeInactiveButtons = true; // 是否包含未激活的按钮
    [SerializeField] private bool enableClickSound = true;
    [SerializeField] private bool enableHoverSound = false;
    
    [Header("过滤选项")]
    [SerializeField] private bool includeUIButtons = true;
    [SerializeField] private bool includeNonUIButtons = false;
    [SerializeField] private string[] excludeKeywords = { "Debug", "Test", "Temp" }; // 排除包含这些关键词的按钮

#if UNITY_EDITOR
    [ContextMenu("为所有按钮添加音效")]
    public void AddSoundEffectToAllButtons()
    {
        // 查找场景中所有的Button组件
        Button[] allButtons = includeInactiveButtons ? 
            FindObjectsOfType<Button>(true) : 
            FindObjectsOfType<Button>();

        List<Button> validButtons = new List<Button>();
        int addedCount = 0;
        int skippedCount = 0;
        int excludedCount = 0;

        foreach (Button button in allButtons)
        {
            // 检查是否应该排除
            if (ShouldExcludeButton(button))
            {
                excludedCount++;
                continue;
            }

            // 检查是否已经有ButtonSoundEffect组件
            if (button.GetComponent<ButtonSoundEffect>() != null)
            {
                skippedCount++;
                continue;
            }

            // 添加ButtonSoundEffect组件
            ButtonSoundEffect soundEffect = button.gameObject.AddComponent<ButtonSoundEffect>();
            
            // 直接设置public字段
            soundEffect.enableClickSound = enableClickSound;
            soundEffect.enableHoverSound = enableHoverSound;

            addedCount++;
            validButtons.Add(button);
        }

        Debug.Log($"=== 按钮音效设置完成 ===");
        Debug.Log($"总按钮数量: {allButtons.Length}");
        Debug.Log($"有效按钮数量: {validButtons.Count}");
        Debug.Log($"添加音效: {addedCount}");
        Debug.Log($"跳过已有音效: {skippedCount}");
        Debug.Log($"排除按钮: {excludedCount}");
        
        if (excludedCount > 0)
        {
            Debug.Log("注意：部分按钮被排除，如需包含请调整过滤选项");
        }
    }

    [ContextMenu("移除所有按钮音效")]
    public void RemoveAllButtonSoundEffects()
    {
        ButtonSoundEffect[] soundEffects = FindObjectsOfType<ButtonSoundEffect>();
        int removedCount = 0;

        foreach (ButtonSoundEffect soundEffect in soundEffects)
        {
            if (soundEffect != null)
            {
                DestroyImmediate(soundEffect);
                removedCount++;
            }
        }

        Debug.Log($"移除了 {removedCount} 个按钮音效组件。");
    }

    [ContextMenu("统计按钮音效")]
    public void CountButtonSoundEffects()
    {
        ButtonSoundEffect[] soundEffects = FindObjectsOfType<ButtonSoundEffect>();
        Button[] allButtons = FindObjectsOfType<Button>();
        
        int withSound = soundEffects.Length;
        int withoutSound = allButtons.Length - withSound;
        
        Debug.Log($"按钮音效统计：");
        Debug.Log($"- 总按钮数量：{allButtons.Length}");
        Debug.Log($"- 有音效的按钮：{withSound}");
        Debug.Log($"- 无音效的按钮：{withoutSound}");
    }

    private bool ShouldExcludeButton(Button button)
    {
        string buttonName = button.name.ToLower();
        
        // 检查是否包含排除关键词
        foreach (string keyword in excludeKeywords)
        {
            if (buttonName.Contains(keyword.ToLower()))
            {
                return true;
            }
        }
        
        // 检查是否在UI层级中
        bool isUIButton = button.GetComponent<Canvas>() != null || 
                         button.GetComponentInParent<Canvas>() != null;
        
        if (includeUIButtons && !includeNonUIButtons)
        {
            return !isUIButton;
        }
        else if (!includeUIButtons && includeNonUIButtons)
        {
            return isUIButton;
        }
        
        return false;
    }
#endif
} 