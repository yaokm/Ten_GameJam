using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BattleshipGame.UI
{
    public class ButtonAnalyzer : MonoBehaviour
    {
        [Header("分析设置")]
        [SerializeField] private bool includeInactiveButtons = true;
        [SerializeField] private bool showDetailedInfo = true;
        
        [Header("过滤选项")]
        [SerializeField] private bool includeUIButtons = true;
        [SerializeField] private bool includeNonUIButtons = false;
        [SerializeField] private string[] excludeKeywords = { "Debug", "Test", "Temp" }; // 排除包含这些关键词的按钮

#if UNITY_EDITOR
        [ContextMenu("分析场景中的按钮")]
        public void AnalyzeButtons()
        {
            Debug.Log("=== 按钮分析报告 ===");
            
            // 查找所有Button组件
            Button[] allButtons = includeInactiveButtons ? 
                FindObjectsOfType<Button>(true) : 
                FindObjectsOfType<Button>();
            
            List<Button> validButtons = new List<Button>();
            List<Button> excludedButtons = new List<Button>();
            
            foreach (Button button in allButtons)
            {
                // 检查是否应该排除
                if (ShouldExcludeButton(button))
                {
                    excludedButtons.Add(button);
                    continue;
                }
                
                validButtons.Add(button);
            }
            
            // 输出统计信息
            Debug.Log($"总按钮数量: {allButtons.Length}");
            Debug.Log($"有效按钮数量: {validButtons.Count}");
            Debug.Log($"排除按钮数量: {excludedButtons.Count}");
            
            // 输出有效按钮详情
            if (showDetailedInfo)
            {
                Debug.Log("=== 有效按钮列表 ===");
                foreach (Button button in validButtons)
                {
                    string path = GetGameObjectPath(button.gameObject);
                    bool hasButtonController = button.GetComponent<ButtonController>() != null;
                    bool hasButtonSoundEffect = button.GetComponent<ButtonSoundEffect>() != null;
                    
                    Debug.Log($"按钮: {button.name}");
                    Debug.Log($"  路径: {path}");
                    Debug.Log($"  激活状态: {(button.gameObject.activeInHierarchy ? "激活" : "未激活")}");
                    Debug.Log($"  有ButtonController: {hasButtonController}");
                    Debug.Log($"  有ButtonSoundEffect: {hasButtonSoundEffect}");
                    Debug.Log($"  交互性: {button.interactable}");
                    Debug.Log("---");
                }
                
                // 输出排除按钮详情
                if (excludedButtons.Count > 0)
                {
                    Debug.Log("=== 排除按钮列表 ===");
                    foreach (Button button in excludedButtons)
                    {
                        string path = GetGameObjectPath(button.gameObject);
                        Debug.Log($"排除按钮: {button.name} ({path})");
                    }
                }
            }
            
            // 输出建议
            Debug.Log("=== 建议 ===");
            int buttonsWithoutSound = 0;
            foreach (Button button in validButtons)
            {
                if (button.GetComponent<ButtonSoundEffect>() == null)
                {
                    buttonsWithoutSound++;
                }
            }
            
            Debug.Log($"需要添加音效的按钮: {buttonsWithoutSound}");
            if (buttonsWithoutSound > 0)
            {
                Debug.Log("建议运行 '为所有按钮添加音效' 来添加缺失的音效组件");
            }
        }

        [ContextMenu("检查按钮音效覆盖情况")]
        public void CheckButtonSoundCoverage()
        {
            Button[] allButtons = FindObjectsOfType<Button>();
            int totalButtons = allButtons.Length;
            int withButtonController = 0;
            int withButtonSoundEffect = 0;
            int withBoth = 0;
            int withNeither = 0;
            
            foreach (Button button in allButtons)
            {
                bool hasButtonController = button.GetComponent<ButtonController>() != null;
                bool hasButtonSoundEffect = button.GetComponent<ButtonSoundEffect>() != null;
                
                if (hasButtonController && hasButtonSoundEffect)
                    withBoth++;
                else if (hasButtonController)
                    withButtonController++;
                else if (hasButtonSoundEffect)
                    withButtonSoundEffect++;
                else
                    withNeither++;
            }
            
            Debug.Log("=== 按钮音效覆盖情况 ===");
            Debug.Log($"总按钮数量: {totalButtons}");
            Debug.Log($"只有ButtonController: {withButtonController}");
            Debug.Log($"只有ButtonSoundEffect: {withButtonSoundEffect}");
            Debug.Log($"两者都有: {withBoth}");
            Debug.Log($"都没有: {withNeither}");
            
            // 输出没有音效的按钮
            if (withNeither > 0)
            {
                Debug.Log("=== 没有音效的按钮 ===");
                foreach (Button button in allButtons)
                {
                    bool hasButtonController = button.GetComponent<ButtonController>() != null;
                    bool hasButtonSoundEffect = button.GetComponent<ButtonSoundEffect>() != null;
                    
                    if (!hasButtonController && !hasButtonSoundEffect)
                    {
                        Debug.Log($"- {button.name} ({GetGameObjectPath(button.gameObject)})");
                    }
                }
            }
        }

        [ContextMenu("列出所有按钮")]
        public void ListAllButtons()
        {
            Button[] allButtons = FindObjectsOfType<Button>(true);
            
            Debug.Log("=== 所有按钮列表 ===");
            for (int i = 0; i < allButtons.Length; i++)
            {
                Button button = allButtons[i];
                string path = GetGameObjectPath(button.gameObject);
                bool hasButtonController = button.GetComponent<ButtonController>() != null;
                bool hasButtonSoundEffect = button.GetComponent<ButtonSoundEffect>() != null;
                
                Debug.Log($"{i + 1}. {button.name}");
                Debug.Log($"   路径: {path}");
                Debug.Log($"   激活: {button.gameObject.activeInHierarchy}");
                Debug.Log($"   ButtonController: {hasButtonController}");
                Debug.Log($"   ButtonSoundEffect: {hasButtonSoundEffect}");
                Debug.Log($"   交互性: {button.interactable}");
                Debug.Log("");
            }
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

        private string GetGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }
#endif
    }
} 