using BattleshipGame.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BattleshipGame.UI
{
    [RequireComponent(typeof(Button))]
    public class ButtonController : MonoBehaviour
    {
        [SerializeField] private TMP_Text buttonText;
        private Button _button;
        private Color _colorCache;
        private Image _buttonImage;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _buttonImage = _button.GetComponent<Image>();
            _colorCache = _buttonImage.color;
        }

        public void SetInteractable(bool state)
        {
            if (_button.interactable == state) return;
            if (buttonText)
            {
                var buttonTextColor = buttonText.color;
                if (state)
                {
                    buttonTextColor.a /= 0.5f;
                    _buttonImage.color = _colorCache;
                }
                else
                {
                    buttonTextColor.a *= 0.5f;
                    _buttonImage.color = new Color(0.5f, 0.5f, 0.55f, 0.5f);
                }

                buttonText.color = buttonTextColor;
            }

            _button.interactable = state;
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void AddListener(UnityAction call)
        {
            _button.onClick.AddListener(call);
            // 添加点击音效
            _button.onClick.AddListener(PlayClickSound);
        }

        /// <summary>
        /// 播放按钮点击音效
        /// </summary>
        private void PlayClickSound()
        {
            // 按钮音效已移除，暂时静音
            // if (SoundEffectManager.Instance != null)
            // {
            //     SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.ButtonClick);
            // }
        }

        public void ChangeText(Key text)
        {
            var localizedText = _button.GetComponentInChildren<LocalizedText>();
            if (localizedText)
            {
                localizedText.SetText(text);
            }
        }

        public void ChangeColor(ColorVariable color)
        {
            _buttonImage.color = color.Value;
        }

        public void ResetColor()
        {
            _buttonImage.color = _colorCache;
        }
    }
}