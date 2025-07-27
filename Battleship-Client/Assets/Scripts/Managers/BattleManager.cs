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
using System;
using TMPro;

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
        [SerializeField] private ButtonController useSkillButton;
        [SerializeField] private MessageDialog leaveMessageDialog;
        [SerializeField] private MessageDialog leaveNotRematchMessageDialog;
        [SerializeField] private OptionDialog winnerOptionDialog;
        [SerializeField] private OptionDialog loserOptionDialog;
        [SerializeField] private OptionDialog leaveConfirmationDialog;
        [SerializeField] private StatusData statusData;
        [SerializeField] private int mSkillType;
        [SerializeField] private GameObject myTurn;
        [SerializeField] private GameObject oppoTurn;
        [SerializeField] private ButtonController HeroButton;
        [SerializeField] private GameObject maskBox;//二次确认框
        [SerializeField] private ButtonController closeMaskBoxButton;
        [SerializeField]
        private TextMeshProUGUI debugTip;
        private readonly Dictionary<int, List<int>> _shots = new Dictionary<int, List<int>>();
        private readonly List<int> _shotsInCurrentTurn = new List<int>();
        private IClient _client;
        private string _enemy;
        private bool _leavePopUpIsOn;
        private string _player;
        private State _state;
        private Skill mSkill;
        private bool _isMultiShotActive = false;
        private string _multiShotDirection = null;
        private Vector3Int? _firstMultiShotCell = null;
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

            // 技能按钮绑定
            useSkillButton.AddListener(OnUseSkillButtonClicked);
            if (_client is NetworkClient netClient)
            {
                netClient.OnSkillUsed += OnSkillUsed;
            }
            
            // 英雄按钮和关闭按钮绑定
            if (HeroButton != null)
            {
                HeroButton.AddListener(OnHeroButtonClicked);
                HeroButton.SetInteractable(true);
            }
            else
            {
                Debug.LogWarning("HeroButton 未在Inspector中赋值！");
            }
            
            if (closeMaskBoxButton != null)
            {
                closeMaskBoxButton.AddListener(OnCloseMaskBoxButtonClicked);
            }
            else
            {
                Debug.LogWarning("closeMaskBoxButton 未在Inspector中赋值！");
            }
            
            // 初始化maskBox为隐藏状态
            if (maskBox != null)
            {
                maskBox.SetActive(false);
            }
            else
            {
                Debug.LogWarning("maskBox 未在Inspector中赋值！");
            }

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
            
            // 初始化回合UI状态
            if (_state.playerTurn == _client.GetSessionId())
            {
                myTurn.SetActive(true);
                oppoTurn.SetActive(false);
            }
            else
            {
                myTurn.SetActive(false);
                oppoTurn.SetActive(true);
            }

            void RegisterToStateEvents()
            {
                _state.OnChange += OnStateChanged;
                _state.players[_player].shots.OnChange += OnPlayerShotsChanged;//我方被射击情况
                _state.players[_enemy].ships.OnChange += OnEnemyShipsChanged;//敌方船只被击中情况
                _state.players[_enemy].shots.OnChange += OnEnemyShotsChanged;//敌方被射击情况
            }
            if (GameManager.TryGetInstance(out var gameManager))
            {
                mSkillType = gameManager.SelectedHeroId;
                Debug.Log("BattleManager 读取到武将编号：" + mSkillType);
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
            if (_isMultiShotActive)
            {
                if (_firstMultiShotCell == null)
                {
                    _firstMultiShotCell = cell;
                    Debug.Log("请选择方向：up/down/left/right（此处用up演示）");
                    _multiShotDirection = "up"; // TODO: UI选择
                    // 计算第二个格子
                    Vector3Int secondCell = cell;
                    if (_multiShotDirection == "up") secondCell += Vector3Int.up;
                    else if (_multiShotDirection == "down") secondCell += Vector3Int.down;
                    else if (_multiShotDirection == "left") secondCell += Vector3Int.left;
                    else if (_multiShotDirection == "right") secondCell += Vector3Int.right;
                    int secondIndex = CoordinateToCellIndex(secondCell, rules.areaSize);
                    // 标记两个格子
                    _shotsInCurrentTurn.Clear();
                    _shotsInCurrentTurn.Add(cellIndex);
                    if (secondIndex >= 0 && secondIndex < rules.areaSize.x * rules.areaSize.y)
                    {
                        _shotsInCurrentTurn.Add(secondIndex);
                        opponentMap.SetMarker(secondIndex, Marker.MarkedTarget);
                    }
                    opponentMap.SetMarker(cellIndex, Marker.MarkedTarget);
                    fireButton.SetInteractable(true);
                    opponentMap.IsMarkingTargets = false;
                }
                return;
            }
            if (_shotsInCurrentTurn.Contains(cellIndex))
            {
                _shotsInCurrentTurn.Remove(cellIndex);
                opponentMap.ClearMarker(cell);
            }
            else if (_shotsInCurrentTurn.Count < rules.shotsPerTurn)
            {
                if (opponentMap.SetMarker(cellIndex, Marker.MarkedTarget))
                {
                    _shotsInCurrentTurn.Add(cellIndex);
                }
            }
            else
            {
                // 如果已达到最大射击次数，移除第一个目标并添加新目标
                Vector3Int oldCell = CellIndexToCoordinate(_shotsInCurrentTurn[0], rules.areaSize.x);
                _shotsInCurrentTurn.RemoveAt(0);
                opponentMap.ClearMarker(oldCell);
                
                if (opponentMap.SetMarker(cellIndex, Marker.MarkedTarget))
                {
                    _shotsInCurrentTurn.Add(cellIndex);
                }
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
            if (_isMultiShotActive && _shotsInCurrentTurn.Count == 2)
            {
                _client.SendTurn(_shotsInCurrentTurn.ToArray());
                _client.SendUseSkill(4, new { direction = _multiShotDirection }); // 补发方向参数
                Debug.Log($"多方向开火：格子{_shotsInCurrentTurn[0]}和{_shotsInCurrentTurn[1]}，方向{_multiShotDirection}");
                _isMultiShotActive = false;
                _multiShotDirection = null;
                _firstMultiShotCell = null;
            }
            else if (_shotsInCurrentTurn.Count == rules.shotsPerTurn)
            {
                _client.SendTurn(_shotsInCurrentTurn.ToArray());
            }
            _shotsInCurrentTurn.Clear();
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
                
                // 显示我方回合UI
                myTurn.SetActive(true);
                oppoTurn.SetActive(false);

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
                
                // 显示对方回合UI
                myTurn.SetActive(false);
                oppoTurn.SetActive(true);
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

            // 判断是否命中
            var coordinate = CellIndexToCoordinate(cellIndex, rules.areaSize.x);
            Debug.Log("OnPlayerShotsChanged:coordinate:"+coordinate);
            bool isHit = false;
            foreach (var placement in placementMap.GetPlacements())
            {
                foreach (var part in placement.ship.EpartCoordinates)
                {
                    var ecoord = new Vector3Int(placement.ship.EnemyCoordinate.x, placement.ship.EnemyCoordinate.y, 0);
                    if (ecoord + (Vector3Int)part == coordinate)
                    {
                        isHit = true;
                        break;
                    }
                }
                if (isHit) break;
            }
            if (isHit)
            {
                opponentMap.SetMarker(cellIndex, Marker.ShotFleet);
            }
            else
            {
                opponentMap.SetMarker(cellIndex, Marker.ShotTarget);
            }
            // 记录回合与射击位置（用于后续回合高亮）
            if (_shots.ContainsKey(turn))
                _shots[turn].Add(cellIndex);
            else
                _shots.Add(turn, new List<int> { cellIndex });
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

        // 技能按钮点击回调
        private void OnUseSkillButtonClicked()
        {
            Debug.Log("请选择技能：1-眩晕对手，2-照明2*3区域，3-爆出对方船点，4-多方向开火");
            // 这里实际应弹窗选择，暂用4号技能演示
            int skillType = this.mSkillType; // TODO: 替换为UI选择
            if (skillType == 4)
            {
                _isMultiShotActive = true;
                _multiShotDirection = null;
                _firstMultiShotCell = null;
                Debug.Log("多方向开火技能已激活，请点击第一个目标格子");
                _client.SendUseSkill(4, new { direction = "up" }); // 先发技能激活，方向后续再发
            }
            else
            {
                _client.SendUseSkill(skillType);
                Debug.Log($"已请求使用技能{skillType}");
            }
        }
        // 技能广播回调
        private void OnSkillUsed(string player, int skillType, object param)
        {
            Debug.Log($"玩家{player}使用了技能{skillType}，原始参数：{param}");

               
            var paramDict = param as GameDevWare.Serialization.IndexedDictionary<string, object>;
            string effect = paramDict["effect"] as string;
            switch (effect)
            {
                case "stun":
                    string target = paramDict["target"] as string;
                    Debug.Log($"玩家{player}使用了眩晕技能，目标：{target}");
                    debugTip.text = $"玩家{player}使用了眩晕技能，目标：{target}";
                    break;
                case "scan":
                    var region = paramDict["region"] as GameDevWare.Serialization.IndexedDictionary<string, object>;
                    int shipTypeCount = Convert.ToInt32(paramDict["shipTypeCount"]);
                    int x = Convert.ToInt32(region["x"]);
                    int y = Convert.ToInt32(region["y"]);
                    Debug.Log($"玩家{player}使用了照明技能，区域：{x},{y}，船只类型数量：{shipTypeCount}");
                    debugTip.text = $"玩家{player}使用了照明技能，区域：{x},{y}，船只类型数量：{shipTypeCount}";
                    break;
                case "reveal":
                    int cellIndex = Convert.ToInt32(paramDict["cellIndex"]);
                    Debug.Log($"玩家{player}使用了揭示技能，目标：{cellIndex}");
                    debugTip.text = $"玩家{player}使用了揭示技能，目标：{cellIndex}";
                    break;
                case "multishot":
                    string direction = paramDict["direction"] as string;
                    Debug.Log($"玩家{player}使用了多方向开火技能，方向：{direction}");
                    debugTip.text = $"玩家{player}使用了多方向开火技能，方向：{direction}";
                    break;
            }
            if (skillType == 4 && player == _client.GetSessionId())
            {
                Debug.Log("你已使用多方向开火技能，本局不能再用");
                useSkillButton?.SetInteractable(false);
            }
            Invoke(nameof(ClearDebugTip), 3f);
        }

        private void ClearDebugTip()
        {
            debugTip.text = "";
        }
        
        // 英雄按钮点击回调
        private void OnHeroButtonClicked()
        {
            Debug.Log("英雄按钮被点击，显示maskBox");
            maskBox.SetActive(true);
        }
        
        // 关闭maskBox按钮点击回调
        private void OnCloseMaskBoxButtonClicked()
        {
            Debug.Log("关闭按钮被点击，隐藏maskBox");
            maskBox.SetActive(false);
        }
    }
      

        
    
}