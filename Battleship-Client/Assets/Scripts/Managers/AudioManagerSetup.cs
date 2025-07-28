using UnityEngine;

namespace BattleshipGame.Managers
{
    /// <summary>
    /// 音频管理器设置脚本，用于在游戏启动时自动创建音频管理器实例
    /// </summary>
    public class AudioManagerSetup : MonoBehaviour
    {
        [Header("音频管理器预制体")]
        [SerializeField] private GameObject bgmManagerPrefab;
        [SerializeField] private GameObject soundEffectManagerPrefab;
        
        private void Awake()
        {
            // 确保BGMManager存在
            if (BGMManager.Instance == null)
            {
                if (bgmManagerPrefab != null)
                {
                    Instantiate(bgmManagerPrefab);
                }
                else
                {
                    // 如果没有预制体，直接创建一个
                    GameObject bgmManager = new GameObject("BGMManager");
                    bgmManager.AddComponent<BGMManager>();
                }
            }
            
            // 确保SoundEffectManager存在
            if (SoundEffectManager.Instance == null)
            {
                if (soundEffectManagerPrefab != null)
                {
                    Instantiate(soundEffectManagerPrefab);
                }
                else
                {
                    // 如果没有预制体，直接创建一个
                    GameObject soundEffectManager = new GameObject("SoundEffectManager");
                    soundEffectManager.AddComponent<SoundEffectManager>();
                }
            }
            
            Debug.Log("音频管理器设置完成");
        }
    }
} 