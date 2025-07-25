using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Network;
using Colyseus.Schema;
using UnityEngine;
using System;

namespace BattleshipGame.AI
{
    public class LocalRoom
    {
        // 游戏常量定义
        private const int StartingFleetHealth = 24; // 初始舰队总生命值（所有船只部件总和）
        private const int GridSize = 10;           // 棋盘网格尺寸（10x10）
        private const int ShotsSize = GridSize * GridSize; // 总射击位置数量（100个）
    
        // 核心游戏状态（公开只读）
        public readonly State State;                // 房间状态机，包含玩家、回合等核心数据
    
        // 玩家状态数据
        private Dictionary<string, int> _health;    // 玩家ID到剩余生命值的映射
        private Dictionary<string, int[]> _placements; // 玩家ID到舰队布局数组的映射
        private Dictionary<string, int[]> _directions; // 玩家ID到舰队方向的映射
        private Dictionary<string, int[][]> _basePositions; // 玩家ID到舰队基座位置的映射
        
        // 回合控制相关
        private string _startingPlayerLastTurn;     // 上一局的先手玩家ID（用于重新匹配时交换先手）
        private bool _isRematching;                // 是否处于重新匹配状态
        private int _placementCompleteCounter;     // 已完成舰队布置的玩家计数（达到2时开始战斗）

        // 添加新的字段跟踪跳过回合
        private Dictionary<string, bool> _skipNextTurn = new Dictionary<string, bool>();
        private Dictionary<string, int[]> _usedSkills = new Dictionary<string, int[]>(); // 记录每个玩家的技能使用情况
        private Dictionary<string, string> _multiShotDirections = new Dictionary<string, string>(); // 多方向参数
        public event Action<string, int, object> OnSkillUsed; // (playerId, skillType, param)
        
        public LocalRoom(string playerId, string enemyId)
        {
            State = new State {players = new MapSchema<Player>(), phase = RoomPhase.Waiting, currentTurn = 1};
            var player = new Player();// {sessionId = playerId};
            var enemy = new Player();// {sessionId = enemyId};
            player.sessionId = playerId;
            enemy.sessionId = enemyId;
            State.players.Add(playerId, player);
            State.players.Add(enemyId, enemy);
            _health = new Dictionary<string, int> {{playerId, StartingFleetHealth}, {enemyId, StartingFleetHealth}};
            _placements = new Dictionary<string, int[]>
            {
                {playerId, new int[ShotsSize]}, {enemyId, new int[ShotsSize]}
            };
            _directions = new Dictionary<string, int[]>{
                {playerId, new int[7]}, {enemyId, new int[7]}
            };
            _basePositions = new Dictionary<string, int[][]>
            {
                {playerId, new int[7][]}, {enemyId, new int[7][]}
            };
            ResetPlayers();

            // 初始化跳过回合字典
            _skipNextTurn = new Dictionary<string, bool>
            {
                {playerId, false},
                {enemyId, false}
            };
            _usedSkills = new Dictionary<string, int[]>
            {
                {playerId, new int[4]},
                {enemyId, new int[4]}
            };
            _multiShotDirections = new Dictionary<string, string>
            {
                {playerId, null},
                {enemyId, null}
            };
        }

        public void Start()
        {
            State.phase = RoomPhase.Place;
            State.TriggerAll();
        }

        public void Place(string clientId, int[] placement,int[] directions=null,int[][] basePositions=null)
        {
            var player = State.players[clientId];
            _placements[player.sessionId] = placement;//舰队位置  
            _directions[player.sessionId] = directions;//舰队方向
            _basePositions[player.sessionId] = basePositions;//舰队基座位置
            _placementCompleteCounter++;
            if (_placementCompleteCounter == 2)
            {
                State.players.ForEach((s, p) => _health[s] = StartingFleetHealth);
                State.playerTurn = _startingPlayerLastTurn = GetStartingPlayer();
                State.phase = RoomPhase.Battle;
                State.TriggerAll();
            }

            string GetStartingPlayer()
            {
                var keys = new string[State.players.Keys.Count];
                State.players.Keys.CopyTo(keys, 0);
                string startingPlayer = keys[UnityEngine.Random.Range(0, keys.Length)];
                if (_isRematching)
                {
                    _isRematching = false;
                    foreach (string key in keys)
                        if (!key.Equals(_startingPlayerLastTurn))
                            return key;
                }

                return startingPlayer;
            }
        }

        public void UseSkill(string clientId, int skillType, object param = null)
        {
            if (skillType < 1 || skillType > 4) return;
            if (_usedSkills[clientId][skillType - 1] == 1) return;
            _usedSkills[clientId][skillType - 1] = 1;
            string opponentId = null;
            foreach (var id in State.players.Keys)
            {
                if (id != clientId)
                {
                    opponentId = id.ToString();
                    break;
                }
            }
            object skillParam = null;
            if (skillType == 1) // 眩晕
            {
                _skipNextTurn[opponentId] = true;
                skillParam = new { effect = "stun", target = opponentId };
            }
            else if (skillType == 2) // 照明
            {
                var shots = State.players[clientId].shots;
                int areaSize = GridSize;
                bool found = false;
                int tryCount = 0;
                int regionX = 0, regionY = 0;
                var shipTypes = new HashSet<int>();
                var placement = _placements[opponentId];
                while (!found && tryCount < 100)
                {
                    int x = UnityEngine.Random.Range(0, areaSize - 2);
                    int y = UnityEngine.Random.Range(0, areaSize - 3);
                    bool allUnshot = true;
                    var localTypes = new HashSet<int>();
                    for (int dx = 0; dx < 2; dx++)
                    {
                        for (int dy = 0; dy < 3; dy++)
                        {
                            int idx = (y + dy) * areaSize + (x + dx);
                            if (shots[idx] != -1) { allUnshot = false; break; }
                            if (placement[idx] >= 0) localTypes.Add(placement[idx]);
                        }
                        if (!allUnshot) break;
                    }
                    if (allUnshot)
                    {
                        found = true;
                        regionX = x; regionY = y;
                        shipTypes = localTypes;
                    }
                    tryCount++;
                }
                skillParam = new { effect = "scan", region = new { x = regionX, y = regionY }, shipTypeCount = shipTypes.Count };
            }
            else if (skillType == 3) // 爆点
            {
                var shots = State.players[clientId].shots;
                var placement = _placements[opponentId];
                var unshotShipCells = new List<int>();
                for (int i = 0; i < placement.Length; i++)
                    if (placement[i] >= 0 && shots[i] == -1) unshotShipCells.Add(i);
                int? revealIdx = null;
                if (unshotShipCells.Count > 0)
                    revealIdx = unshotShipCells[UnityEngine.Random.Range(0, unshotShipCells.Count)];
                skillParam = new { effect = "reveal", cellIndex = revealIdx };
            }
            else if (skillType == 4) // 多方向
            {
                string dir = param != null ? (string)param.GetType().GetProperty("direction")?.GetValue(param, null) : null;
                _multiShotDirections[clientId] = dir;
                skillParam = new { effect = "multishot", direction = dir };
            }
            OnSkillUsed?.Invoke(clientId, skillType, skillParam);
        }

        public void Turn(string clientId, int[] targetIndexes)
        {
            if (!State.playerTurn.Equals(clientId)) return;
            if (targetIndexes == null || (targetIndexes.Length != 1 && targetIndexes.Length != 2)) return;
            var player = State.players[clientId];
            var opponent = GetOpponent(player);
            var playerShots = player.shots;
            var opponentShips = opponent.ships;
            int[] opponentPlacements = _placements[opponent.sessionId];
            bool hit = false;
            bool isXBomb = false;
            // 多方向开火
            if (_multiShotDirections[clientId] != null && targetIndexes.Length == 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    int idx = targetIndexes[i];
                    if (playerShots[idx] == -1)
                    {
                        playerShots[idx] = State.currentTurn;
                        State.players[clientId].shots.InvokeOnChange(State.currentTurn, idx);
                        if (opponentPlacements[idx] >= 0)
                        {
                            hit = true;
                            _health[opponent.sessionId]--;
                        }
                    }
                }
                _multiShotDirections[clientId] = null;
            }
            else if (targetIndexes.Length == 1)
            {
                int targetIndex = targetIndexes[0];
                if (playerShots[targetIndex] == -1)
                {
                    playerShots[targetIndex] = State.currentTurn;
                    State.players[clientId].shots.InvokeOnChange(State.currentTurn, targetIndex);
                    if (opponentPlacements[targetIndex] >= 0)
                    {
                        hit = true;
                        _health[opponent.sessionId]--;
                    }
                }
            }

            if (_health[opponent.sessionId] <= 0)
            {
                State.winningPlayer = player.sessionId;
                State.phase = RoomPhase.Result;
            }
            else
            {
                // 炸弹船处理需要特殊逻辑
                if (isXBomb)//如果击中炸弹船
                {
                    // 炸弹船：标记跳过，切换回合，增加回合数
                    _skipNextTurn[player.sessionId] = true;
                    // 被击中后，且回合
                     State.playerTurn = opponent.sessionId;
                    State.currentTurn++;
                    Debug.Log($"命中炸弹! 玩家{player.sessionId}继续, 回合数:{State.currentTurn}");
                }
                else if (!hit)
                {
                    // 未命中：检查是否需要跳过回合
                    if (_skipNextTurn[opponent.sessionId])
                    {
                        // 对手需要跳过回合，保持当前玩家的回合
                        _skipNextTurn[opponent.sessionId] = false; // 重置跳过标记
                        State.playerTurn = player.sessionId; // 回合不变
                        State.currentTurn++; // 回合数增加
                        Debug.Log($"跳过玩家{opponent.sessionId}的回合! 玩家{player.sessionId}继续, 回合数:{State.currentTurn}");
                    }
                    else
                    {
                        // 正常切换回合
                        State.playerTurn = opponent.sessionId;
                        State.currentTurn++;
                    }
                }
                else
                {
                    // 命中普通船：保持当前玩家回合
                    State.playerTurn = player.sessionId;
                }
            }

            State.TriggerAll();

            void UpdateShips(ArraySchema<int> ships, int start, int end, int turn)
            {
                for (int i = start; i < end; i++)
                    if (ships[i] == -1)
                    {
                        ships[i] = turn;
                        State.players[opponent.sessionId].ships.InvokeOnChange(State.currentTurn, i);
                        break;
                    }
            }
        }

        private Player GetOpponent(Player player)
        {
            var opponent = new Player();
            State.players.ForEach((id, p) =>
            {
                if (p != player) opponent = p;
            });
            return opponent;
        }

        public void Rematch(bool isRematching)
        {
            if (!isRematching)
            {
                State.phase = RoomPhase.Leave;
                return;
            }

            ResetPlayers();
            _placementCompleteCounter = 0;
            State.playerTurn = string.Empty;
            State.winningPlayer = string.Empty;
            State.currentTurn = 1;
            _isRematching = true;
            Start();
        }

        private void ResetPlayers()
        {
            State.players.ForEach((id, p) =>
            {
                var shots = new Dictionary<int, int>();
                for (var i = 0; i < ShotsSize; i++) shots.Add(i, -1);
                p.shots = new ArraySchema<int>(shots);

                var ships = new Dictionary<int, int>();
                for (var i = 0; i < StartingFleetHealth; i++) ships.Add(i, -1);
                p.ships = new ArraySchema<int>(ships);
            });

            _health = _health.ToDictionary(kvp => kvp.Key, kvp => StartingFleetHealth);
            _placements = _placements.ToDictionary(kvp => kvp.Key, kvp => new int[ShotsSize]);
            _directions = _directions.ToDictionary(kvp => kvp.Key, kvp => new int[7]);

            // 重置跳过回合状态
            foreach (var key in _skipNextTurn.Keys.ToList())
            {
                _skipNextTurn[key] = false;
            }
        }
    }
}