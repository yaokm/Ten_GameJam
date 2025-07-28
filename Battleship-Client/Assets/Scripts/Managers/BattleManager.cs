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
using Microsoft.Unity.VisualStudio.Editor;

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
        [SerializeField] private UnityEngine.UI.Image HeroImage;
        [SerializeField] private ParticleSystem shotEffect;
        [SerializeField] private Sprite[] heroSprites; // 武将图片数组，索引对应武将编号-1
        [SerializeField]
        private TextMeshProUGUI debugTip;
        [SerializeField] private UnityEngine.UI.Image EnemyHero;
        [SerializeField] private GameObject[] boomTxts;
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
        private List<Vector3Int> _validSecondCells = new List<Vector3Int>(); // 第二个格子的有效选择范围
        private bool _resultShown = false; // 防止重复显示结果
        private int _pendingEffects = 0; // 待播放的特效数量
        private bool _waitingForEffects = false; // 是否正在等待特效播放完成
        private bool _isPlayerStunned = false; // 我方是否被眩晕
        private bool _isEnemyStunned = false; // 敌方是否被眩晕
        private bool _willPlayerBeStunned = false; // 我方下回合是否会被眩晕
        private bool _willEnemyBeStunned = false; // 敌方下回合是否会被眩晕
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
        private void SetEnemyShipPositionsAndDirections(int[][] basePositions, int[] directions, int enemyHeroType)
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
            
            // 更新敌方武将图片
            UpdateEnemyHeroImage(enemyHeroType);
        }
        private void Start()
        {
            Debug.Log("BattleManager Start");
            opponentMap.SetClickListener(this);
            opponentTurnHighlighter.SetClickListener(this);
            opponentStatusMapTurnHighlighter.SetClickListener(this);
            
            // 初始化boomTxts，确保所有图片都隐藏
            InitializeBoomTxts();

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
                
                // 根据武将编号更换英雄图片
                UpdateHeroImage(mSkillType);
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
            
            // 清理所有boomTxts
            HideAllBoomTxts();

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
            // 检查是否是我方回合且没有被眩晕
            if (_state.playerTurn != _client.GetSessionId() || _isPlayerStunned) return;
            
            int cellIndex = CoordinateToCellIndex(cell, rules.areaSize);
            if (_isMultiShotActive && mSkillType == 4)
            {
                HandleMultiShotClick(cell, cellIndex, true);
                return;
            }
            if (_shotsInCurrentTurn.Contains(cellIndex))
            {
                _shotsInCurrentTurn.Remove(cellIndex);
                opponentMap.ClearMarker(cell);
            }
            else if (_shotsInCurrentTurn.Count < rules.shotsPerTurn)
            {
                // 普通技能
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
            Debug.Log($"OnGamePhaseChanged: phase = {phase}");
            switch (phase)
            {
                case RoomPhase.Battle:
                    SwitchTurns();
                    break;
                case RoomPhase.Result:
                    Debug.Log("OnGamePhaseChanged: 调用ShowResult");
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
        }
        
        private void ShowResult()
        {
            Debug.Log($"ShowResult called. Current state: {_state?.phase}, winningPlayer: {_state?.winningPlayer}, resultShown: {_resultShown}, pendingEffects: {_pendingEffects}, waitingForEffects: {_waitingForEffects}");
            
            if (_resultShown)
            {
                Debug.LogWarning("ShowResult called multiple times. Ignoring.");
                return;
            }
            
            // 检查游戏是否真的结束了
            if (_state == null || string.IsNullOrEmpty(_state.winningPlayer) || _state.phase != RoomPhase.Result)
            {
                Debug.LogWarning($"ShowResult called but game is not finished yet. State: {_state?.phase}, winningPlayer: {_state?.winningPlayer}. Ignoring.");
                return;
            }
            
            // 给特效一些时间来开始播放（防止游戏结束事件在特效开始播放之前触发）
            StartCoroutine(ShowResultWithDelay());
        }
        
        private System.Collections.IEnumerator ShowResultWithDelay()
        {
            // 等待一帧，确保所有特效都有机会开始播放
            yield return null;
            
            // 如果还有特效在播放，等待特效完成
            if (_pendingEffects > 0)
            {
                Debug.Log($"还有 {_pendingEffects} 个特效在播放，等待特效完成后再显示结果");
                _waitingForEffects = true;
                yield break;
            }
            
            Debug.Log("开始显示战斗结果");
            _resultShown = true;
            statusData.State = BattleResult;
            
            // 暂停BGM，为音效腾出空间
            if (BGMManager.Instance != null)
            {
                BGMManager.Instance.PauseBGM();
                Debug.Log("已暂停BGM，为胜负音效腾出空间");
            }
            
            // 播放胜利或失败音效
            if (SoundEffectManager.Instance != null)
            {
                if (_state.winningPlayer == _client.GetSessionId())
                {
                    // 播放胜利音效
                    SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.Victory);
                    Debug.Log("播放胜利音效");
                }
                else
                {
                    // 播放失败音效
                    SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.Defeat);
                    Debug.Log("播放失败音效");
                }
            }
            
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

        private void FireShots()
        {
            // 检查是否被眩晕
            if (_isPlayerStunned)
            {
                Debug.Log("我方被眩晕，无法开火");
                return;
            }
            
            fireButton.SetInteractable(false);
            if (_isMultiShotActive && _shotsInCurrentTurn.Count == 2)
            {
                _client.SendTurn(_shotsInCurrentTurn.ToArray());
                // 计算方向（从第一个格子到第二个格子的方向）
                Vector3Int firstCell = CellIndexToCoordinate(_shotsInCurrentTurn[0], rules.areaSize.x);
                Vector3Int secondCell = CellIndexToCoordinate(_shotsInCurrentTurn[1], rules.areaSize.x);
                Vector3Int direction = secondCell - firstCell;
                string directionStr = "";
                if (direction == Vector3Int.up) directionStr = "up";
                else if (direction == Vector3Int.down) directionStr = "down";
                else if (direction == Vector3Int.left) directionStr = "left";
                else if (direction == Vector3Int.right) directionStr = "right";
                
                _client.SendUseSkill(4, new { direction = directionStr });
                Debug.Log($"多方向开火：格子{_shotsInCurrentTurn[0]}和{_shotsInCurrentTurn[1]}，方向{directionStr}");
                
                // 清理多方向开火状态
                _isMultiShotActive = false;
                _multiShotDirection = null;
                _firstMultiShotCell = null;
                _validSecondCells.Clear();
                
                // 清除瞄准标记，但保持一个瞄准状态
                ClearMultiShotMarkers();
            }
            else if (_shotsInCurrentTurn.Count == rules.shotsPerTurn)
            {
                _client.SendTurn(_shotsInCurrentTurn.ToArray());
                _shotsInCurrentTurn.Clear();
            }
            else
            {
                _shotsInCurrentTurn.Clear();
            }
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
            // 检查眩晕状态
            CheckStunStatus();
            
            if (_state.playerTurn == _client.GetSessionId())
                TurnToPlayer();
            else
                TurnToEnemy();

            void TurnToPlayer()
            {
                // 检查我方是否被眩晕
                if (_isPlayerStunned)
                {
                    // 我方被眩晕，无法行动
                    ShowBoomTxt(0); // 显示"陷阱生效我方本回合不能行动"
                    statusData.State = OpponentTurn; // 直接切换到敌方回合
                    Debug.Log("我方被眩晕，跳过回合");
                    
                    // 清除眩晕状态
                    _isPlayerStunned = false;
                    HideBoomTxt(0);
                    
                    // 显示对方回合UI
                    myTurn.SetActive(false);
                    oppoTurn.SetActive(true);
                }
                else
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
            }

            void TurnToEnemy()
            {
                // 检查敌方是否被眩晕
                if (_isEnemyStunned)
                {
                    // 敌方被眩晕，无法行动
                    ShowBoomTxt(1); // 显示"陷阱生效中，敌方本回合不能行动"
                    statusData.State = PlayerTurn; // 直接切换到我方回合
                    Debug.Log("敌方被眩晕，跳过回合");
                    
                    // 清除眩晕状态
                    _isEnemyStunned = false;
                    HideBoomTxt(1);
                    
                    // 显示我方回合UI
                    myTurn.SetActive(true);
                    oppoTurn.SetActive(false);
                }
                else
                {
                    statusData.State = OpponentTurn;
                    
                    // 显示对方回合UI
                    myTurn.SetActive(false);
                    oppoTurn.SetActive(true);
                }
            }
        }
        
        // 检查眩晕状态
        private void CheckStunStatus()
        {
            // 检查是否需要应用眩晕效果
            if (_willPlayerBeStunned)
            {
                _isPlayerStunned = true;
                _willPlayerBeStunned = false;
                Debug.Log("应用我方眩晕效果");
            }
            
            if (_willEnemyBeStunned)
            {
                _isEnemyStunned = true;
                _willEnemyBeStunned = false;
                Debug.Log("应用敌方眩晕效果");
            }
        }

        private void OnStateChanged(List<DataChange> changes)
        {
            foreach (var change in changes)
            {
                Debug.Log($"State change detected: {change.Field} = {change.Value}");
                
                if (change.Field == RoomState.PlayerTurn)
                {
                    SwitchTurns();
                }
                else if (change.Field == "winningPlayer")
                {
                    Debug.Log($"Game finished! Winning player: {change.Value}");
                    // 只有当winningPlayer不为空且游戏阶段为Result时才显示结果
                    if (!string.IsNullOrEmpty(change.Value?.ToString()))
                    {
                        Debug.Log("OnStateChanged: 调用ShowResult");
                        ShowResult();
                    }
                }
            }
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
                // 播放命中特效
                PlayHitEffectOnOpponentMap(cellIndex);
            }
            else
            {
                opponentMap.SetMarker(cellIndex, Marker.ShotTarget);
                // 播放未命中特效
                PlayMissEffectOnOpponentMap(cellIndex);
            }
            // 记录回合与射击位置（用于后续回合高亮）
            if (_shots.ContainsKey(turn))
                _shots[turn].Add(cellIndex);
            else
                _shots.Add(turn, new List<int> { cellIndex });
        }

        // 在opponentMap格子中心播放命中特效
        private void PlayHitEffectOnOpponentMap(int cellIndex)
        {
            // 获取格子中心的世界坐标
            Vector3Int cell = CellIndexToCoordinate(cellIndex, rules.areaSize.x);
            var tilemap = opponentMap.markerLayer;
            if (tilemap == null) return;
            Vector3 worldPos = tilemap.GetCellCenterWorld(cell);
            
            // 往上移动半个格子的距离
            worldPos += Vector3.up * 1.3f;
            
            Debug.Log($"尝试播放命中特效，格子索引: {cellIndex}, 世界坐标: {worldPos}, 当前pendingEffects: {_pendingEffects}");
            
            // 增加待播放特效计数
            _pendingEffects++;
            Debug.Log($"命中特效开始播放，pendingEffects增加到: {_pendingEffects}");
            
            // 使用EffectManager播放命中特效
            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlayHitEffect(worldPos, OnHitEffectComplete);
            }
            else
            {
                Debug.LogError("EffectManager.Instance 为空！");
                OnHitEffectComplete();
            }
        }
        
        // 命中特效播放完成回调
        private void OnHitEffectComplete()
        {
            _pendingEffects--;
            Debug.Log($"命中特效播放完成，剩余特效数量: {_pendingEffects}, waitingForEffects: {_waitingForEffects}");
            
            // 如果正在等待特效完成且所有特效都播放完毕，显示结果
            if (_waitingForEffects && _pendingEffects <= 0)
            {
                Debug.Log("所有特效播放完成，现在显示结果");
                _waitingForEffects = false;
                ShowResult();
            }
        }
        
        // 在opponentMap格子中心播放未命中特效
        private void PlayMissEffectOnOpponentMap(int cellIndex)
        {
            // 获取格子中心的世界坐标
            Vector3Int cell = CellIndexToCoordinate(cellIndex, rules.areaSize.x);
            var tilemap = opponentMap.markerLayer;
            if (tilemap == null) return;
            Vector3 worldPos = tilemap.GetCellCenterWorld(cell);
            
            // 往上移动半个格子的距离
            worldPos += Vector3.up * 1.3f;
            
            Debug.Log($"尝试播放未命中特效，格子索引: {cellIndex}, 世界坐标: {worldPos}, 当前pendingEffects: {_pendingEffects}");
            
            // 增加待播放特效计数
            _pendingEffects++;
            Debug.Log($"未命中特效开始播放，pendingEffects增加到: {_pendingEffects}");
            
            // 使用EffectManager播放未命中特效
            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlayMissEffect(worldPos, OnMissEffectComplete);
            }
            else
            {
                Debug.LogError("EffectManager.Instance 为空！");
                OnMissEffectComplete();
            }
        }
        
        // 未命中特效播放完成回调
        private void OnMissEffectComplete()
        {
            _pendingEffects--;
            Debug.Log($"未命中特效播放完成，剩余特效数量: {_pendingEffects}, waitingForEffects: {_waitingForEffects}");
            
            // 如果正在等待特效完成且所有特效都播放完毕，显示结果
            if (_waitingForEffects && _pendingEffects <= 0)
            {
                Debug.Log("所有特效播放完成，现在显示结果");
                _waitingForEffects = false;
                ShowResult();
            }
        }
        
        // 新增：在opponentMap格子中心播放射击特效（保留原方法名以兼容）
        private void PlayShotEffectOnOpponentMap(int cellIndex)
        {
            // 获取格子中心的世界坐标
            Vector3Int cell = CellIndexToCoordinate(cellIndex, rules.areaSize.x);
            var tilemap = opponentMap.markerLayer;
            if (tilemap == null) return;
            Vector3 worldPos = tilemap.GetCellCenterWorld(cell);
            
            // 往上移动半个格子的距离
            worldPos += Vector3.up * 1.3f;
            
            Debug.Log($"尝试播放射击特效，格子索引: {cellIndex}, 世界坐标: {worldPos}, 当前pendingEffects: {_pendingEffects}");
            
            // 增加待播放特效计数
            _pendingEffects++;
            Debug.Log($"射击特效开始播放，pendingEffects增加到: {_pendingEffects}");
            
            // 使用EffectManager播放命中特效
            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlayHitEffect(worldPos, OnShotEffectComplete);
            }
            else
            {
                Debug.LogError("EffectManager.Instance 为空！");
                OnShotEffectComplete();
            }
        }
        
        // 射击特效播放完成回调
        private void OnShotEffectComplete()
        {
            _pendingEffects--;
            Debug.Log($"射击特效播放完成，剩余特效数量: {_pendingEffects}, waitingForEffects: {_waitingForEffects}");
            
            // 如果正在等待特效完成且所有特效都播放完毕，显示结果
            if (_waitingForEffects && _pendingEffects <= 0)
            {
                Debug.Log("所有特效播放完成，现在显示结果");
                _waitingForEffects = false;
                ShowResult();
            }
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
            // 检查是否击沉船只
            int rankOrder = opponentStatus.getShipRankOrder(part);
            bool wasShipSunk = opponentStatus.isAllShipPartShot(rankOrder);
            
            // 显示敌方船只被击中情况
            opponentStatus.DisplayShotEnemyShipParts(part, turn);
            
            // 如果船只被击沉，播放击沉特效
            if (wasShipSunk)
            {
                PlaySunkEffectOnShip(rankOrder);
                Debug.Log($"播放击沉特效：船只 {rankOrder} 被击沉");
            }
        }
        
        // 在船头位置播放击沉特效
        private void PlaySunkEffectOnShip(int rankOrder)
        {
            // 根据rankOrder找到对应的船只
            var ship = rules.ships.FirstOrDefault(s => s.rankOrder == rankOrder);
            if (ship == null) return;
            
            // 获取船头位置（EnemyCoordinate）
            Vector3Int shipHeadPosition = new Vector3Int(ship.EnemyCoordinate.x, ship.EnemyCoordinate.y, 0);
            var tilemap = opponentMap.markerLayer;
            if (tilemap == null) return;
            Vector3 worldPos = tilemap.GetCellCenterWorld(shipHeadPosition);
            
            // 往上移动半个格子的距离
            worldPos += Vector3.up * 1.3f;
            
            Debug.Log($"尝试播放击沉特效，船只rankOrder: {rankOrder}, 世界坐标: {worldPos}, 当前pendingEffects: {_pendingEffects}");
            
            // 增加待播放特效计数
            _pendingEffects++;
            Debug.Log($"击沉特效开始播放，pendingEffects增加到: {_pendingEffects}");
            
            // 使用EffectManager播放击沉特效
            if (EffectManager.Instance != null)
            {
                EffectManager.Instance.PlaySunkEffect(worldPos, OnSunkEffectComplete);
            }
            else
            {
                OnSunkEffectComplete();
            }
        }
        
        // 击沉特效播放完成回调
        private void OnSunkEffectComplete()
        {
            _pendingEffects--;
            Debug.Log($"击沉特效播放完成，剩余特效数量: {_pendingEffects}, waitingForEffects: {_waitingForEffects}");
            
            // 如果正在等待特效完成且所有特效都播放完毕，显示结果
            if (_waitingForEffects && _pendingEffects <= 0)
            {
                Debug.Log("所有特效播放完成，现在显示结果");
                _waitingForEffects = false;
                ShowResult();
            }
        }

        // 技能按钮点击回调
        private void OnUseSkillButtonClicked()
        {
            // 检查是否被眩晕
            if (_isPlayerStunned)
            {
                Debug.Log("我方被眩晕，无法使用技能");
                return;
            }
            
            // 使用技能时自动关闭二次确认框
            if (maskBox != null)
            {
                maskBox.SetActive(false);
            }
            Debug.Log("请选择技能：1-眩晕对手，2-照明2*3区域，3-爆出对方船点，4-多方向开火");
            // 这里实际应弹窗选择，暂用4号技能演示
            int skillType = this.mSkillType; // TODO: 替换为UI选择
            if (skillType == 4)
            {
                // 检查是否有普通瞄准的目标
                if (_shotsInCurrentTurn.Count > 0)
                {
                    // 使用第一个普通瞄准目标作为多方向开火的第一个目标
                    int firstTargetIndex = _shotsInCurrentTurn[0];
                    Vector3Int firstTargetCell = CellIndexToCoordinate(firstTargetIndex, rules.areaSize.x);
                    
                    _firstMultiShotCell = firstTargetCell;
                    _shotsInCurrentTurn.Clear();
                    _shotsInCurrentTurn.Add(firstTargetIndex);
                    
                    // 计算第二个格子的有效选择范围（上下左右）
                    _validSecondCells.Clear();
                    Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
                    foreach (var direction in directions)
                    {
                        Vector3Int secondCell = firstTargetCell + direction;
                        int secondIndex = CoordinateToCellIndex(secondCell, rules.areaSize);
                        if (secondIndex >= 0 && secondIndex < rules.areaSize.x * rules.areaSize.y)
                        {
                            _validSecondCells.Add(secondCell);
                        }
                    }
                    
                    _isMultiShotActive = true;
                    _multiShotDirection = null;
                    
                    Debug.Log($"多方向开火技能已激活，使用普通瞄准目标 {firstTargetCell} 作为第一个目标，第二个格子可选范围：{string.Join(", ", _validSecondCells)}");
                    _client.SendUseSkill(4, new { direction = "up" }); // 先发技能激活，方向后续再发
                }
                else
                {
                    // 没有普通瞄准目标，正常进入多方向开火模式
                    _isMultiShotActive = true;
                    _multiShotDirection = null;
                    _firstMultiShotCell = null;
                    _validSecondCells.Clear();
                    Debug.Log("多方向开火技能已激活，请点击第一个目标格子");
                    _client.SendUseSkill(4, new { direction = "up" }); // 先发技能激活，方向后续再发
                }
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
                    
                    // 处理眩晕效果
                    if (player == _client.GetSessionId())
                    {
                        // 我方使用眩晕技能，敌方下回合无法行动
                        _willEnemyBeStunned = true;
                        ShowBoomTxt(2); // 显示"陷阱生效中，敌方本回合不能行动"
                        Debug.Log("我方使用眩晕技能，敌方下回合将被眩晕");
                    }
                    else
                    {
                        // 敌方使用眩晕技能，我方下回合无法行动
                        _willPlayerBeStunned = true;
                        ShowBoomTxt(3); // 显示"技能已生效敌方下回合无法行动"
                        Debug.Log("敌方使用眩晕技能，我方下回合将被眩晕");
                    }
                    break;
                case "scan":
                    // 程昱照明技能：只有使用技能的玩家才能看到照明效果
                    if (player == _client.GetSessionId())
                    {
                        var region = paramDict["region"] as GameDevWare.Serialization.IndexedDictionary<string, object>;
                        int shipTypeCount = Convert.ToInt32(paramDict["shipTypeCount"]);
                        int x = Convert.ToInt32(region["x"]);
                        int y = Convert.ToInt32(region["y"]);
                        debugTip.text = $"你使用了照明技能，区域：{x},{y}，船只类型数量：{shipTypeCount}";
                        opponentMap.ShowChengyuScanArea(new Vector3Int(x, y, 0), shipTypeCount);
                        Invoke(nameof(ClearChengyuScanArea), 3f);
                    }
                    else
                    {
                        debugTip.text = $"敌方使用了照明技能";
                    }
                    break;
                case "reveal":
                    // 诸葛亮揭示技能：只有使用技能的玩家才能看到揭示效果
                    if (player == _client.GetSessionId())
                    {
                        int cellIndex = Convert.ToInt32(paramDict["cellIndex"]);
                        Debug.Log($"你使用了揭示技能，目标：{cellIndex}");
                        debugTip.text = $"你使用了揭示技能，目标：{cellIndex}";
                        // 诸葛亮揭示技能：在opponentMap的cursorLayer上显示zhugeliangTip
                        Vector3Int cell = CellIndexToCoordinate(cellIndex, rules.areaSize.x);
                        opponentMap.ShowZhugeliangTip(cell);
                        // 3秒后自动清除
                        Invoke(nameof(ClearZhugeliangTips), 3f);
                    }
                    else
                    {
                        debugTip.text = $"敌方使用了揭示技能";
                    }
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
        
        // 清除所有瞄准标记
        private void ClearAllMarkers()
        {
            // 清除当前回合的所有瞄准标记
            foreach (int shotIndex in _shotsInCurrentTurn)
            {
                Vector3Int shotCell = CellIndexToCoordinate(shotIndex, rules.areaSize.x);
                opponentMap.ClearMarker(shotCell);
            }
            
            // 重置按钮状态
            fireButton.SetInteractable(false);
            opponentMap.IsMarkingTargets = true;
            
            Debug.Log("已清除所有瞄准标记");
        }
        
        // 清除多方向开火标记，但保持一个瞄准状态
        private void ClearMultiShotMarkers()
        {
            // 清除当前回合的所有瞄准标记
            foreach (int shotIndex in _shotsInCurrentTurn)
            {
                Vector3Int shotCell = CellIndexToCoordinate(shotIndex, rules.areaSize.x);
                opponentMap.ClearMarker(shotCell);
            }
            
            // 清空瞄准列表，准备重新瞄准
            _shotsInCurrentTurn.Clear();
            
            // 重置按钮状态
            fireButton.SetInteractable(false);
            opponentMap.IsMarkingTargets = true;
            
            Debug.Log("已清除多方向开火标记，准备重新瞄准");
        }
        

        
        // 处理多方向开火的点击逻辑
        private void HandleMultiShotClick(Vector3Int cell, int cellIndex, bool useZhouyuFire)
        {
            // 检查格子是否在地图范围内
            if (cellIndex < 0 || cellIndex >= rules.areaSize.x * rules.areaSize.y)
            {
                Debug.Log("点击的格子超出地图范围");
                return;
            }
            if (_firstMultiShotCell == null)
            {
                // 第一次点击，选择第一个格子
                _firstMultiShotCell = cell;
                _shotsInCurrentTurn.Clear();
                _shotsInCurrentTurn.Add(cellIndex);
                opponentMap.SetMarker(cellIndex, Marker.MarkedTarget, useZhouyuFire);
                // 显示四象提示
                if (mSkillType == 4)
                    opponentMap.ShowZhouyuTips(cell);
                // 计算第二个格子的有效选择范围（上下左右）
                _validSecondCells.Clear();
                Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
                foreach (var direction in directions)
                {
                    Vector3Int secondCell = cell + direction;
                    int secondIndex = CoordinateToCellIndex(secondCell, rules.areaSize);
                    if (secondIndex >= 0 && secondIndex < rules.areaSize.x * rules.areaSize.y)
                    {
                        _validSecondCells.Add(secondCell);
                    }
                }
                Debug.Log($"已选择第一个格子 {cell}，第二个格子可选范围：{string.Join(", ", _validSecondCells)}");
                fireButton.SetInteractable(false);
                opponentMap.IsMarkingTargets = true;
            }
            else
            {
                // 已经选择了第一个格子，现在处理第二个格子的选择
                if (_validSecondCells.Contains(cell))
                {
                    // 清除之前第二个格子的标记（如果存在）
                    if (_shotsInCurrentTurn.Count > 1)
                    {
                        int oldSecondIndex = _shotsInCurrentTurn[1];
                        Vector3Int oldSecondCell = CellIndexToCoordinate(oldSecondIndex, rules.areaSize.x);
                        opponentMap.ClearMarker(oldSecondCell);
                    }
                    _shotsInCurrentTurn.Clear();
                    _shotsInCurrentTurn.Add(CoordinateToCellIndex(_firstMultiShotCell.Value, rules.areaSize));
                    _shotsInCurrentTurn.Add(cellIndex);
                    opponentMap.SetMarker(CoordinateToCellIndex(_firstMultiShotCell.Value, rules.areaSize), Marker.MarkedTarget, useZhouyuFire);
                    opponentMap.SetMarker(cellIndex, Marker.MarkedTarget, useZhouyuFire);
                    // 清除四象提示
                    if (mSkillType == 4)
                        opponentMap.ClearZhouyuTips(_firstMultiShotCell.Value);
                    Debug.Log($"已选择第二个格子 {cell}，准备开火");
                    fireButton.SetInteractable(true);
                    opponentMap.IsMarkingTargets = false;
                }
                else if (cell == _firstMultiShotCell.Value)
                {
                    Debug.Log($"取消当前多方向开火选择，但保持技能激活状态");
                    opponentMap.ClearMarker(cell);
                    if (_shotsInCurrentTurn.Count > 1)
                    {
                        int secondIndex = _shotsInCurrentTurn[1];
                        Vector3Int secondCell = CellIndexToCoordinate(secondIndex, rules.areaSize.x);
                        opponentMap.ClearMarker(secondCell);
                    }
                    // 清除四象提示
                    if (mSkillType == 4)
                        opponentMap.ClearZhouyuTips(_firstMultiShotCell.Value);
                    _firstMultiShotCell = null;
                    _shotsInCurrentTurn.Clear();
                    _validSecondCells.Clear();
                    fireButton.SetInteractable(false);
                    opponentMap.IsMarkingTargets = true;
                }
                else
                {
                    foreach (int shotIndex in _shotsInCurrentTurn)
                    {
                        Vector3Int shotCell = CellIndexToCoordinate(shotIndex, rules.areaSize.x);
                        opponentMap.ClearMarker(shotCell);
                    }
                    // 清除四象提示
                    if (mSkillType == 4 && _firstMultiShotCell.HasValue)
                        opponentMap.ClearZhouyuTips(_firstMultiShotCell.Value);
                    _firstMultiShotCell = cell;
                    _shotsInCurrentTurn.Clear();
                    _shotsInCurrentTurn.Add(cellIndex);
                    opponentMap.SetMarker(cellIndex, Marker.MarkedTarget, useZhouyuFire);
                    // 重新显示四象提示
                    if (mSkillType == 4)
                        opponentMap.ShowZhouyuTips(cell);
                    _validSecondCells.Clear();
                    Vector3Int[] directions = { Vector3Int.up, Vector3Int.down, Vector3Int.left, Vector3Int.right };
                    foreach (var direction in directions)
                    {
                        Vector3Int secondCell = cell + direction;
                        int secondIndex = CoordinateToCellIndex(secondCell, rules.areaSize);
                        if (secondIndex >= 0 && secondIndex < rules.areaSize.x * rules.areaSize.y)
                        {
                            _validSecondCells.Add(secondCell);
                        }
                    }
                    Debug.Log($"重新选择第一个格子 {cell}，第二个格子可选范围：{string.Join(", ", _validSecondCells)}");
                    fireButton.SetInteractable(false);
                    opponentMap.IsMarkingTargets = true;
                }
            }
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
        
        // 根据武将编号更新英雄图片
        private void UpdateHeroImage(int heroId)
        {
            if (HeroImage == null)
            {
                Debug.LogWarning("HeroImage 未在Inspector中赋值！");
                return;
            }
            
            // 优先使用Inspector中设置的图片数组
            if (heroSprites != null && heroSprites.Length > 0)
            {
                int spriteIndex = heroId - 1; // 武将编号1对应数组索引0
                if (spriteIndex >= 0 && spriteIndex < heroSprites.Length && heroSprites[spriteIndex] != null)
                {
                    HeroImage.sprite = heroSprites[spriteIndex];
                    Debug.Log($"成功更换武将 {heroId} 的图片（使用Inspector设置）");
                    return;
                }
            }
            // 如果Inspector中没有设置，则尝试从Resources加载
            string imagePath = $"Heroes/hero_{heroId}"; // 假设图片路径格式为 "Heroes/hero_1", "Heroes/hero_2" 等
            Sprite heroSprite = Resources.Load<Sprite>(imagePath);
            
            if (heroSprite != null)
            {
                HeroImage.sprite = heroSprite;
                Debug.Log($"成功更换武将 {heroId} 的图片（使用Resources加载）");
            }
            else
            {
                Debug.LogWarning($"未找到武将 {heroId} 的图片资源：{imagePath}");
            }
        }
        
        // 新增：更新敌方武将图片
        private void UpdateEnemyHeroImage(int enemyHeroId)
        {
            if (EnemyHero == null)
            {
                Debug.LogWarning("EnemyHero 未在Inspector中赋值！");
                return;
            }
            
            // 优先使用Inspector中设置的图片数组
            if (heroSprites != null && heroSprites.Length > 0)
            {
                int spriteIndex = enemyHeroId - 1; // 武将编号1对应数组索引0
                if (spriteIndex >= 0 && spriteIndex < heroSprites.Length && heroSprites[spriteIndex] != null)
                {
                    EnemyHero.sprite = heroSprites[spriteIndex];
                    Debug.Log($"成功更换敌方武将 {enemyHeroId} 的图片（使用Inspector设置）");
                    return;
                }
            }
            // 如果Inspector中没有设置，则尝试从Resources加载
            string imagePath = $"Heroes/hero_{enemyHeroId}"; // 假设图片路径格式为 "Heroes/hero_1", "Heroes/hero_2" 等
            Sprite heroSprite = Resources.Load<Sprite>(imagePath);
            
            if (heroSprite != null)
            {
                EnemyHero.sprite = heroSprite;
                Debug.Log($"成功更换敌方武将 {enemyHeroId} 的图片（使用Resources加载）");
            }
            else
            {
                Debug.LogWarning($"未找到敌方武将 {enemyHeroId} 的图片资源：{imagePath}");
            }
        }

        private void ClearZhugeliangTips()
        {
            opponentMap.ClearAllZhugeliangTips();
        }

        private void ClearChengyuScanArea()
        {
            opponentMap.ClearChengyuScanArea();
        }
        
        // 初始化boomTxts，隐藏所有图片
        private void InitializeBoomTxts()
        {
            if (boomTxts != null && boomTxts.Length >= 4)
            {
                for (int i = 0; i < boomTxts.Length; i++)
                {
                    if (boomTxts[i] != null)
                    {
                        boomTxts[i].SetActive(false);
                    }
                }
            }
        }
        
        // 显示boomTxts图片
        private void ShowBoomTxt(int index)
        {
            if (boomTxts != null && index >= 0 && index < boomTxts.Length && boomTxts[index] != null)
            {
                // 先隐藏所有图片
                InitializeBoomTxts();
                // 显示指定图片
                boomTxts[index].SetActive(true);
                Debug.Log($"显示boomTxts图片 {index}");
            }
        }
        
        // 隐藏boomTxts图片
        private void HideBoomTxt(int index)
        {
            if (boomTxts != null && index >= 0 && index < boomTxts.Length && boomTxts[index] != null)
            {
                boomTxts[index].SetActive(false);
                Debug.Log($"隐藏boomTxts图片 {index}");
            }
        }
        
        // 隐藏所有boomTxts图片
        private void HideAllBoomTxts()
        {
            InitializeBoomTxts();
        }
    }
      

        
    
}