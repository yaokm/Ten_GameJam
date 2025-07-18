using System.Collections.Generic;
using System.Linq;
using BattleshipGame.AI;
using BattleshipGame.Core;
using BattleshipGame.Network;
using BattleshipGame.Tiling;
using BattleshipGame.UI;
using Colyseus.Schema;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static BattleshipGame.Core.StatusData.Status;
using static BattleshipGame.Core.GridUtils;
using UnityEngine.Tilemaps;

namespace BattleshipGame.Managers
{
    public class BattleManager : MonoBehaviour, IBattleMapClickListener, ITurnClickListener
    {
        [SerializeField] private Options options;
        [SerializeField] private Rules rules;
        [SerializeField] private BattleMap userMap;
        [SerializeField] private BattleMap opponentMap;
        [SerializeField] private PlacementMap placementMap;
        [SerializeField] private OpponentStatus opponentStatus;
        [SerializeField] private TurnHighlighter opponentTurnHighlighter;
        [SerializeField] private TurnHighlighter opponentStatusMapTurnHighlighter;
        [SerializeField] private ButtonController fireButton;
        [SerializeField] private ButtonController leaveButton;
        [SerializeField] private MessageDialog leaveMessageDialog;
        [SerializeField] private MessageDialog leaveNotRematchMessageDialog;
        [SerializeField] private OptionDialog winnerOptionDialog;
        [SerializeField] private OptionDialog loserOptionDialog;
        [SerializeField] private OptionDialog leaveConfirmationDialog;
        [SerializeField] private StatusData statusData;
        private readonly Dictionary<int, List<int>> _shots = new Dictionary<int, List<int>>();
        private readonly List<int> _shotsInCurrentTurn = new List<int>();
        private IClient _client;
        private string _enemy;
        private bool _leavePopUpIsOn;
        private string _player;
        private State _state;

        private void Awake()
        {
            Debug.Log("BattleManager Awake");
            if (GameManager.TryGetInstance(out var gameManager))
            {
                _client = gameManager.Client;
                _client.GamePhaseChanged += OnGamePhaseChanged;
            }
            else
            {
                SceneManager.LoadScene(0);
            }
        }
        // 添加处理敌方船只信息的方法
        private void SetEnemyShipPositionsAndDirections(int[][] basePositions, int[] directions)
        {
            // 获取所有船只
            var ships = rules.ships;
            // 为每艘船设置敌方位置和方向
            for (int i = 0; i < ships.Count; i++)
            {

                    // 设置敌方坐标和方向
                    ships[i].EnemyCoordinate = new Vector2Int(
                        basePositions[ships[i].rankOrder][0],
                        basePositions[ships[i].rankOrder][1]
                    );
                    ships[i]._enemyDirection = (Direction)directions[ships[i].rankOrder];
                
            }
            foreach (var ship in ships){
                Debug.Log("ship:"+ship+"EnemyCoordinate:"+ship.EnemyCoordinate+"_enemyDirection:"+ship._enemyDirection);
                //opponentMap.SetShip(ship, new Vector3Int(ship.EnemyCoordinate.x, ship.EnemyCoordinate.y, 0));
            }
        }
        private void Start()
        {
            Debug.Log("BattleManager Start");
            opponentMap.SetClickListener(this);
            opponentTurnHighlighter.SetClickListener(this);
            opponentStatusMapTurnHighlighter.SetClickListener(this);

            foreach (var placement in placementMap.GetPlacements())
            {
                userMap.SetShip(placement.ship, placement.Coordinate);
            }
            // 注册敌方船只信息接收事件
            if (_client is NetworkClient networkClient)
            {
                networkClient.OnOpponentInfoReceived += SetEnemyShipPositionsAndDirections;
            }
            _client.SendGetOpponentInfoRequest();
            //如果是本地对战，则直接设置敌方船只信息，如果是网络对战，则需要等待服务器返回敌方船只信息，走SetEnemyShipPositionsAndDirections的回调
            // if (_client is LocalClient)
            // {
            //     foreach (var ship in rules.ships)
            //     {
            //         opponentMap.SetShip(ship, new Vector3Int(ship.EnemyCoordinate.x, ship.EnemyCoordinate.y, 0));//设置敌方船只信息
            //     }
            // }
            statusData.State = BeginBattle;
            leaveButton.AddListener(LeaveGame);
            fireButton.AddListener(FireShots);
            fireButton.SetInteractable(false);

            _state = _client.GetRoomState();
            _player = _state.players[_client.GetSessionId()].sessionId;

            foreach (string key in _state.players.Keys)
                if (key != _client.GetSessionId())
                {
                    _enemy = _state.players[key].sessionId;

                    // var Eships = _state.players[_enemy].ships.Items;
                    // foreach (var ship in Eships)
                    // {
                    //     Debug.Log("eship:"+ship);
                    // }

                    break;
                }

            RegisterToStateEvents();
            OnGamePhaseChanged(_state.phase);

            void RegisterToStateEvents()
            {
                _state.OnChange += OnStateChanged;
                _state.players[_player].shots.OnChange += OnPlayerShotsChanged;//我方被射击情况
                _state.players[_enemy].ships.OnChange += OnEnemyShipsChanged;//敌方船只被击中情况
                _state.players[_enemy].shots.OnChange += OnEnemyShotsChanged;//敌方被射击情况
            }
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame) LeaveGame();
        }

        private void OnDestroy()
        {
            placementMap.Clear();
            if (_client == null) return;
            _client.GamePhaseChanged -= OnGamePhaseChanged;

            UnRegisterFromStateEvents();

            void UnRegisterFromStateEvents()
            {
                if (_state == null) return;
                _state.OnChange -= OnStateChanged;
                if (_state.players[_player] == null) return;
                _state.players[_player].shots.OnChange -= OnPlayerShotsChanged;
                if (_state.players[_enemy] == null) return;
                _state.players[_enemy].ships.OnChange -= OnEnemyShipsChanged;
                _state.players[_enemy].shots.OnChange -= OnEnemyShotsChanged;
            }
        }

        public void OnOpponentMapClicked(Vector3Int cell)
        {
            if (_state.playerTurn != _client.GetSessionId()) return;
            int cellIndex = CoordinateToCellIndex(cell, rules.areaSize);
            if (_shotsInCurrentTurn.Contains(cellIndex))
            {
                _shotsInCurrentTurn.Remove(cellIndex);
                opponentMap.ClearMarker(cell);
            }
            else if (_shotsInCurrentTurn.Count < rules.shotsPerTurn &&
                     opponentMap.SetMarker(cellIndex, Marker.MarkedTarget))
            {
                _shotsInCurrentTurn.Add(cellIndex);
            }

            fireButton.SetInteractable(_shotsInCurrentTurn.Count == rules.shotsPerTurn);
            opponentMap.IsMarkingTargets = _shotsInCurrentTurn.Count != rules.shotsPerTurn;
        }

        public void HighlightShotsInTheSameTurn(Vector3Int coordinate)
        {
            int cellIndex = CoordinateToCellIndex(coordinate, rules.areaSize);
            foreach (var keyValuePair in from keyValuePair in _shots
                                         from cell in keyValuePair.Value
                                         where cell == cellIndex
                                         select keyValuePair)
            {
                HighlightTurn(keyValuePair.Key);
                return;
            }
        }

        public void HighlightTurn(int turn)
        {
            if (!_shots.ContainsKey(turn)) return;
            opponentTurnHighlighter.HighlightTurnShotsOnOpponentMap(_shots[turn]);
            opponentStatusMapTurnHighlighter.HighlightTurnShotsOnOpponentStatusMap(turn);
        }

        private void OnGamePhaseChanged(string phase)
        {
            switch (phase)
            {
                case RoomPhase.Battle:
                    SwitchTurns();
                    break;
                case RoomPhase.Result:
                    ShowResult();
                    break;
                case RoomPhase.Waiting:
                    if (_leavePopUpIsOn) break;
                    leaveMessageDialog.Show(GoBackToLobby);
                    break;
                case RoomPhase.Leave:
                    _leavePopUpIsOn = true;
                    leaveNotRematchMessageDialog.Show(GoBackToLobby);
                    break;
            }

            static void GoBackToLobby()
            {
                GameSceneManager.Instance.GoToLobby();
            }

            void ShowResult()
            {
                statusData.State = BattleResult;
                if (_state.winningPlayer == _client.GetSessionId())
                    winnerOptionDialog.Show(Rematch, Leave);
                else
                    loserOptionDialog.Show(Rematch, Leave);

                void Rematch()
                {
                    _client.SendRematch(true);
                    statusData.State = WaitingOpponentRematchDecision;
                }

                void Leave()
                {
                    _client.SendRematch(false);
                    LeaveGame();
                }
            }
        }

        private void FireShots()
        {
            fireButton.SetInteractable(false);
            if (_shotsInCurrentTurn.Count == rules.shotsPerTurn)
                _client.SendTurn(_shotsInCurrentTurn.ToArray());
            _shotsInCurrentTurn.Clear();
            // 若服务器仍然保持本方回合（命中），继续允许标记
            opponentMap.IsMarkingTargets = true;
        }

        private void LeaveGame()
        {
            leaveConfirmationDialog.Show(() =>
            {
                _client.LeaveRoom();
                if (_client is NetworkClient)
                {
                    GameSceneManager.Instance.GoToLobby();
                }
                else
                {
                    statusData.State = MainMenu;
                    GameSceneManager.Instance.GoToMenu();
                }
            });
        }

        private void SwitchTurns()
        {
            if (_state.playerTurn == _client.GetSessionId())
                TurnToPlayer();
            else
                TurnToEnemy();

            void TurnToPlayer()
            {
                opponentMap.IsMarkingTargets = true;
                statusData.State = PlayerTurn;
                opponentMap.FlashGrids();

#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
                if (options.vibration && _client is NetworkClient _)
                {
                    Handheld.Vibrate();
                }
#endif
            }

            void TurnToEnemy()
            {
                statusData.State = OpponentTurn;
            }
        }

        private void OnStateChanged(List<DataChange> changes)
        {
            foreach (var _ in changes.Where(change => change.Field == RoomState.PlayerTurn))
                SwitchTurns();
        }

        private void OnPlayerShotsChanged(int turn, int cellIndex)
        {
            if (turn <= 0) return;

            // // 将目标索引转换为网格坐标（用于查询敌方船只状态）
            // var coordinate = CellIndexToCoordinate(cellIndex, rules.areaSize.x);

            // // 通过OpponentStatus查询该坐标是否是敌方船只的部件（未被击中时返回-1）
            // int shotTurn = opponentStatus.GetShotTurn(coordinate);

            // // 根据是否命中选择标记类型
            // Marker marker = shotTurn != OpponentStatus.NotShot 
            //     ? Marker.ShotFleet    // 命中船只部件时使用shotFleetMarker
            //     : Marker.ShotTarget;  // 未命中时使用shotTargetMarker
            // Debug.Log($"turn: {turn}, cellIndex: {cellIndex}, coordinate: {coordinate}, shotTurn: {shotTurn}");
            // 在对手地图设置标记
            SetMarker(cellIndex, turn, true);

            // // 记录回合与射击位置（用于后续回合高亮）
            // if (_shots.ContainsKey(turn))
            //     _shots[turn].Add(cellIndex);
            // else
            //     _shots.Add(turn, new List<int> { cellIndex });
        }

        private void OnEnemyShotsChanged(int turn, int cellIndex)
        {
            if (turn <= 0) return;
            SetMarker(cellIndex, turn, false);
        }

        private void SetMarker(int cellIndex, int turn, bool player)
        {
            if (player)
            {
                opponentMap.SetMarker(cellIndex, Marker.ShotTarget);
                if (_shots.ContainsKey(turn))
                    _shots[turn].Add(cellIndex);
                else
                    _shots.Add(turn, new List<int> { cellIndex });

                return;
            }

            userMap.SetMarker(cellIndex, !(from placement in placementMap.GetPlacements()
                                           from part in placement.ship.partCoordinates
                                           select placement.Coordinate + (Vector3Int)part
                into partCoordinate
                                           let shot = CellIndexToCoordinate(cellIndex, rules.areaSize.x)
                                           where partCoordinate.Equals(shot)
                                           select partCoordinate).Any()
                ? Marker.Missed
                : Marker.Hit);
        }

        private void OnEnemyShipsChanged(int turn, int part)
        {
            opponentStatus.DisplayShotEnemyShipParts(part, turn);
        }
    }
}