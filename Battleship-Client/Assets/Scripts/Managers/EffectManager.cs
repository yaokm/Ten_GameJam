using UnityEngine;
using System;
using System.Collections;

namespace BattleshipGame.Managers
{
    public class EffectManager : MonoBehaviour
    {
        [Header("特效预制体")]
        [SerializeField] private ParticleSystem hitEffect; // 命中特效
        [SerializeField] private ParticleSystem missEffect; // 未命中特效
        [SerializeField] private ParticleSystem sunkEffect; // 击沉特效

        private static EffectManager _instance;
        public static EffectManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EffectManager>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("EffectManager");
                        _instance = go.AddComponent<EffectManager>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
                Debug.Log("EffectManager 已初始化");
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            Debug.Log($"EffectManager Start - 预制体状态: Hit={hitEffect != null}, Miss={missEffect != null}, Sunk={sunkEffect != null}");
        }

        /// <summary>
        /// 播放命中特效
        /// </summary>
        /// <param name="position">播放位置</param>
        /// <param name="onComplete">播放完成回调</param>
        public void PlayHitEffect(Vector3 position, Action onComplete = null)
        {
            if (hitEffect != null)
            {
                // 确保特效在正确的层级播放
                ParticleSystem effect = Instantiate(hitEffect, position, Quaternion.identity);
                
                // 设置特效的层级，确保在UI之上
                effect.transform.SetParent(null);
                effect.transform.position = position;
                
                // 确保特效在正确的渲染层级
                var renderer = effect.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = 100; // 确保在UI之上
                }
                
                effect.Play();
                Debug.Log($"播放命中特效在位置: {position}, 特效对象: {effect.name}, 层级: {effect.transform.position}");
                
                // 如果有回调，在特效播放完成后调用
                if (onComplete != null)
                {
                    StartCoroutine(WaitForEffectComplete(effect, onComplete));
                }
                else
                {
                    Destroy(effect.gameObject, effect.main.duration);
                }
            }
            else
            {
                Debug.LogWarning("命中特效预制体未设置！");
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 播放未命中特效
        /// </summary>
        /// <param name="position">播放位置</param>
        /// <param name="onComplete">播放完成回调</param>
        public void PlayMissEffect(Vector3 position, Action onComplete = null)
        {
            if (missEffect != null)
            {
                // 确保特效在正确的层级播放
                ParticleSystem effect = Instantiate(missEffect, position, Quaternion.identity);
                
                // 设置特效的层级，确保在UI之上
                effect.transform.SetParent(null);
                effect.transform.position = position;
                
                // 确保特效在正确的渲染层级
                var renderer = effect.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = 100; // 确保在UI之上
                }
                
                effect.Play();
                Debug.Log($"播放未命中特效在位置: {position}, 特效对象: {effect.name}, 层级: {effect.transform.position}");
                
                // 如果有回调，在特效播放完成后调用
                if (onComplete != null)
                {
                    StartCoroutine(WaitForEffectComplete(effect, onComplete));
                }
                else
                {
                    Destroy(effect.gameObject, effect.main.duration);
                }
            }
            else
            {
                Debug.LogWarning("未命中特效预制体未设置！");
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 播放击沉特效
        /// </summary>
        /// <param name="position">播放位置（船头位置）</param>
        /// <param name="onComplete">播放完成回调</param>
        public void PlaySunkEffect(Vector3 position, Action onComplete = null)
        {
            if (sunkEffect != null)
            {
                // 确保特效在正确的层级播放
                ParticleSystem effect = Instantiate(sunkEffect, position, Quaternion.identity);
                
                // 设置特效的层级，确保在UI之上
                effect.transform.SetParent(null);
                effect.transform.position = position;
                
                // 确保特效在正确的渲染层级
                var renderer = effect.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    renderer.sortingOrder = 100; // 确保在UI之上
                }
                
                effect.Play();
                Debug.Log($"播放击沉特效在位置: {position}, 特效对象: {effect.name}, 层级: {effect.transform.position}");
                
                // 如果有回调，在特效播放完成后调用
                if (onComplete != null)
                {
                    StartCoroutine(WaitForEffectComplete(effect, onComplete));
                }
                else
                {
                    Destroy(effect.gameObject, effect.main.duration);
                }
            }
            else
            {
                Debug.LogWarning("击沉特效预制体未设置！");
                onComplete?.Invoke();
            }
        }
        
        /// <summary>
        /// 等待特效播放完成的协程
        /// </summary>
        /// <param name="effect">特效对象</param>
        /// <param name="onComplete">播放完成回调</param>
        private IEnumerator WaitForEffectComplete(ParticleSystem effect, Action onComplete)
        {
            Debug.Log($"开始等待特效播放完成: {effect?.name}");
            
            // 等待特效播放完成
            while (effect != null && effect.isPlaying)
            {
                yield return null;
            }
            
            Debug.Log($"特效播放完成: {effect?.name}");
            
            // 特效播放完成，调用回调
            onComplete?.Invoke();
            
            // 销毁特效对象
            if (effect != null)
            {
                Destroy(effect.gameObject);
            }
        }
    }
} 