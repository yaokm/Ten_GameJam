using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Network;
using Colyseus.Schema;
using UnityEngine;

namespace BattleshipGame.AI
{
    public class LocalRoom
    {
        // 游戏常量定义
        private const int StartingFleetHealth = 25; // 初始舰队总生命值（所有船只部件总和）
        private const int GridSize = 10;           // 棋盘网格尺寸（10x10）
        private const int ShotsSize = GridSize * GridSize; // 总射击位置数量（100个）
    
        // 核心游戏状态（公开只读）
        public readonly State State;                // 房间状态机，包含玩家、回合等核心数据
    
        // 玩家状态数据
        private Dictionary<string, int> _health;    // 玩家ID到剩余生命值的映射
        private Dictionary<string, int[]> _placements; // 玩家ID到舰队布局数组的映射
        private Dictionary<string, int[]> _directions; // 玩家ID到舰队方向的映射
        
        // 回合控制相关
        private string _startingPlayerLastTurn;     // 上一局的先手玩家ID（用于重新匹配时交换先手）
        private bool _isRematching;                // 是否处于重新匹配状态
        private int _placementCompleteCounter;     // 已完成舰队布置的玩家计数（达到2时开始战斗）
    
        public LocalRoom(string playerId, string enemyId)
        {
            State = new State {players = new MapSchema<Player>(), phase = RoomPhase.Waiting, currentTurn = 1};
            var player = new Player {sessionId = playerId};
            var enemy = new Player {sessionId = enemyId};
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
            ResetPlayers();
        }

        public void Start()
        {
            State.phase = RoomPhase.Place;
            State.TriggerAll();
        }

        public void Place(string clientId, int[] placement,int[] directions=null)
        {
            var player = State.players[clientId];
            _placements[player.sessionId] = placement;
            _directions[player.sessionId] = directions;
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
                string startingPlayer = keys[Random.Range(0, keys.Length)];
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

        public void Turn(string clientId, int[] targetIndexes)
        {
            if (!State.playerTurn.Equals(clientId)) return;
            if (targetIndexes == null || targetIndexes.Length != 1) return;

            int targetIndex = targetIndexes[0];
            var player = State.players[clientId];
            var opponent = GetOpponent(player);
            var playerShots = player.shots;
            var opponentShips = opponent.ships;
            int[] opponentPlacements = _placements[opponent.sessionId];

            bool hit = false;

            if (playerShots[targetIndex] == -1)
            {
                playerShots[targetIndex] = State.currentTurn;
                State.players[clientId].shots.InvokeOnChange(State.currentTurn, targetIndex);

                if (opponentPlacements[targetIndex] >= 0)
                {
                    hit = true;
                    _health[opponent.sessionId]--;

                    switch (opponentPlacements[targetIndex])
                    {
                        case 0: // F0
                            UpdateShips(opponentShips, 0, 6, State.currentTurn);
                            break;
                        case 1: // E0
                            UpdateShips(opponentShips, 6,11, State.currentTurn);
                            break;
                        case 2: // D0
                            UpdateShips(opponentShips, 11, 15, State.currentTurn);
                            break;
                        case 3: // C0
                            UpdateShips(opponentShips, 15, 18, State.currentTurn);
                            break;
                        case 4: // B0
                            UpdateShips(opponentShips, 18, 20, State.currentTurn);
                            break;
                        case 5: // A0
                            UpdateShips(opponentShips, 20, 21, State.currentTurn);
                            break;
                        case 6: // D1
                            UpdateShips(opponentShips, 21, 25, State.currentTurn);
                            break;
                        case 7: // Scout
                        case 8: // Scout
                            break;
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
                if (!hit)
                {
                    State.playerTurn = opponent.sessionId;
                    State.currentTurn++;
                }
                else
                {
                    State.playerTurn = player.sessionId; // 保持自己回合方便客户端逻辑
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
        }
    }
}