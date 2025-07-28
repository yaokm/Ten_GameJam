using UnityEngine;

namespace BattleshipGame.Core
{
    [CreateAssetMenu(fileName = "Options", menuName = "Battleship/Options")]
    public class Options : ScriptableObject
    {
        public Difficulty aiDifficulty = Difficulty.Easy;
        public bool vibration = true;
        
        // AI模式配置
        [Header("AI模式配置")]
        [SerializeField]
        public bool enableAiMode = true; // 是否在创建房间时启用AI模式
    }
}