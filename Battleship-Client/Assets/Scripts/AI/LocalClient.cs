using System;
using System.Data;
using BattleshipGame.Core;
using BattleshipGame.Network;
using UnityEngine;

namespace BattleshipGame.AI
{
    public class LocalClient : MonoBehaviour, IClient
    {
        private const string PlayerId = "player";
        private const string EnemyId = "enemy";
        private Enemy _enemy;
        private bool _isMatchFinished;
        private LocalRoom _room;

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            _enemy.enabled = true;
            _room = new LocalRoom(PlayerId, EnemyId);
            _room.State.OnChange += changes =>
            {
                foreach (var change in changes)
                    switch (change.Field)
                    {
                        case RoomState.Phase:
                            var phase = (string) change.Value;
                            GamePhaseChanged?.Invoke(phase);
                            if (phase.Equals(RoomPhase.Result)) _isMatchFinished = true;
                            break;
                        case RoomState.PlayerTurn:
                            // 检查是否是AI回合，如果是则触发AI行动
                            CheckAndTriggerAITurn();
                            break;
                        case RoomState.CurrentTurn:
                            // 添加：当回合数变化时，也检查是否需要触发AI行动
                            // 这样即使playerTurn没变（跳过回合的情况），也能正确触发AI
                            CheckAndTriggerAITurn();
                            break;
                    }
            };
            _room.State.players[PlayerId].ships.OnChange += (turn, part) => _enemy.UpdatePlayerShips(part, turn);
        }

        private void OnDisable()
        {
            _enemy.enabled = false;
            _room = null;
        }

        public event Action<string> GamePhaseChanged;

        public State GetRoomState()
        {
            return _room.State;
        }

        public string GetSessionId()
        {
            return PlayerId;
        }

        public void Connect(string endPoint, Action success, Action error)
        {
            _room.Start();
        }

        public void SendPlacement(int[] placement,int[] direction=null,int[][] basePositions=null)
        {
            _room.Place(PlayerId, placement,direction,basePositions);
            int[] ecell=_enemy.PlaceShipsRandomly();
            _room.Place(EnemyId, ecell,_enemy.GetDirections(),_enemy.GetBasePositions());
            
        }

        public void SendTurn(int[] targetIndexes)
        {
            _room.Turn(PlayerId, targetIndexes);
        }

        public void SendRematch(bool isRematching)
        {
            _enemy.ResetForRematch();
            _room.Rematch(isRematching);
        }
        public void SendGetOpponentInfoRequest()
        {
            var ships=_enemy.GetRules().ships;
            var directions=_enemy.GetDirections();
            var basePositions=_enemy.GetBasePositions();
            foreach(var ship in ships){
                ship._enemyDirection=(Direction)directions[ship.rankOrder];
                var basePosition=basePositions[ship.rankOrder];
                ship.EnemyCoordinate=new Vector2Int(basePosition[0],basePosition[1]);
            }           
        }
        public void LeaveRoom()
        {
            enabled = false;
        }
        public async void GetOpponentShipData(Action<int[], int[], int[]> callback)
        { 
            
        }

        // 新增辅助方法，检查并触发AI回合
        private void CheckAndTriggerAITurn()
        {
            if (!_isMatchFinished && EnemyId.Equals(_room.State.playerTurn))
                StartCoroutine(_enemy.GetShots(cells => _room.Turn(EnemyId, cells)));
        }
    }
}