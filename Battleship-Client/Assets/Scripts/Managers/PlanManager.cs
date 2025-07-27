using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Core;
using BattleshipGame.Network;
using BattleshipGame.Tiling;
using BattleshipGame.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using static BattleshipGame.Core.StatusData.Status;
using static BattleshipGame.Core.GridUtils;
using System.IO;
using UnityEngine.UI;
using TMPro;

namespace BattleshipGame.Managers
{
    public class PlanManager : MonoBehaviour, IPlanMapMoveListener
    {
        private const int EmptyCell = -1;
        [SerializeField] private MessageDialog leaveMessageDialog;
        [SerializeField] private ButtonController leaveButton;
        [SerializeField] private ButtonController clearButton;
        [SerializeField] private ButtonController randomButton;
        [SerializeField] private ButtonController continueButton;
        [SerializeField] private PlanMap planMap;
        [SerializeField] private GridSpriteMapper gridSpriteMapper;
        [SerializeField] private GridSpriteMapper poolGridSpriteMapper;
        [SerializeField] private Rules rules;
        [SerializeField] private PlacementMap placementMap;
        [SerializeField] private StatusData statusData;
        [SerializeField] private Button btnHero;
        [SerializeField] private TextMeshProUGUI txtHero;

        [SerializeField] private GameObject HeroBox;
        [SerializeField] private ButtonController okHeroButton;
        [SerializeField] private ButtonController Hero0Button;
        [SerializeField] private ButtonController Hero1Button;
        [SerializeField] private ButtonController Hero2Button;
        [SerializeField] private ButtonController Hero3Button;
        [SerializeField] private Image HeroName;
        [SerializeField] private Image skillContent;
        // 新增：保存选择的武将编号
        private int selectedHeroId = 1;
        private int _cellCount;
        private int[] _cells;
        private IClient _client;
        private List<PlacementMap.Placement> _placements = new List<PlacementMap.Placement>();
        private SortedDictionary<int, Ship> _pool;
        private List<int> _shipsNotDragged = new List<int>();
        private Vector2Int MapAreaSize => rules.areaSize;
        
        // 用于跟踪每艘船的状态
        private Dictionary<int, ShipState> _shipStates = new Dictionary<int, ShipState>();
        
        // 添加标志来跟踪是否正在随机放置
        private bool _isRandomPlacing = false;
        
        // 船只状态类
        private class ShipState
        {
            public int ShipId;
            public int RankOrder;
            public Vector3Int Position;
            public Direction Direction;
            public bool IsValid;
            public bool IsOutOfBounds;
            public bool HasCollision;
            public List<int> CollidingWithShips = new List<int>();
            
            public ShipState(int shipId, int rankOrder)
            {
                ShipId = shipId;
                RankOrder = rankOrder;
                IsValid = true;
            }
        }

        private void Awake()
        {
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

        private void Start()
        {
            planMap.SetPlaceListener(this);
            foreach (var ship in rules.ships)
            {
                ship.Reset();
            }
            _cellCount = MapAreaSize.x * MapAreaSize.y;
            leaveButton.AddListener(LeaveGame);
            clearButton.AddListener(OnClearButtonPressed);
            randomButton.AddListener(PlaceShipsRandomly);
            continueButton.AddListener(CompletePlacement);
            btnHero.onClick.AddListener(OnHeroButtonClicked);
            
            // 绑定okHeroButton点击事件
            if (okHeroButton != null)
            {
                okHeroButton.AddListener(OnOkHeroButtonClicked);
            }
            else
            {
                Debug.LogWarning("okHeroButton 未在Inspector中赋值！");
            }
            
            // 初始化HeroBox为打开状态
            if (HeroBox != null)
            {
                HeroBox.SetActive(true);
            }
            else
            {
                Debug.LogWarning("HeroBox 未在Inspector中赋值！");
            }

            BeginShipPlacement();

            void OnClearButtonPressed()
            {
                _shipsNotDragged.Clear();
                ResetPlacementMap();
            }

            void PlaceShipsRandomly()
            {
                Debug.Log("开始随机放置船只");
                _isRandomPlacing = true; // 标记开始随机放置
                ResetPlacementMap();

                if (_shipsNotDragged.Count == 0) _shipsNotDragged = _pool.Keys.ToList();

                // 避免放置玩家已拖拽的船只
                foreach (var placement in _placements.Where(placement => !_shipsNotDragged.Contains(placement.shipId)))
                {
                    planMap.SetShip(placement.ship, placement.Coordinate);
                    RegisterShipToCells(placement.shipId, placement.ship, placement.Coordinate);
                    placementMap.PlaceShip(placement.shipId, placement.ship, placement.Coordinate);
                }

                // 随机放置剩余船只
                List<int> placedShipIds = new List<int>();
                foreach (int shipId in _shipsNotDragged)
                {
                    var uncheckedCells = new List<int>();
                    for (var i = 0; i < _cellCount; i++) uncheckedCells.Add(i);
                    var isPlaced = false;
                    while (!isPlaced)
                    {
                        if (uncheckedCells.Count == 0) break;

                        int cell = uncheckedCells[Random.Range(0, uncheckedCells.Count)];
                        uncheckedCells.Remove(cell);
                        isPlaced = PlaceShip(_pool[shipId], default, CellIndexToCoordinate(cell, MapAreaSize.x), false, false,
                            shipId);
                        
                        if (isPlaced)
                        {
                            placedShipIds.Add(shipId);
                            Debug.Log($"成功随机放置船只 {shipId} (rankOrder={_pool[shipId].rankOrder})");
                        }
                    }

                    if (!isPlaced)
                    {
                        _isRandomPlacing = false; // 重置标志
                        statusData.State = PlacementImpossible;
                        clearButton.SetInteractable(true);
                        randomButton.SetInteractable(false);
                        Debug.LogError($"无法随机放置船只 {shipId}");
                        return;
                    }
                }

                // 从池中移除所有已放置的船只
                foreach (int shipId in placedShipIds)
                {
                    _pool.Remove(shipId);
                    Debug.Log($"移除船只 {shipId} 从池中");
                }
                _shipsNotDragged.Clear();
                
                _isRandomPlacing = false; // 标记随机放置结束
                
                // 确保_placements是最新的
                _placements = placementMap.GetPlacements();
                
                Debug.Log($"随机放置完成，已放置 {_placements.Count} 艘船，池中剩余 {_pool.Count} 艘船");

                gridSpriteMapper.CacheSpritePositions();
                poolGridSpriteMapper.CacheSpritePositions();
                
                // 更新所有船只状态
                UpdateAllShipStates();
                
                // 更新Continue按钮状态
                UpdateContinueButtonState();
                
                statusData.State = PlacementReady;
                
                // 如果所有船只都已放置且有效，则启用Continue按钮
                if (_pool.Count == 0)
                {
                    randomButton.SetInteractable(false);
                    continueButton.SetInteractable(true);
                    Debug.Log("所有船只已成功放置，Continue按钮已启用");
                }
            }

            void CompletePlacement()
            {
                continueButton.SetInteractable(false);
                randomButton.SetInteractable(false);
                clearButton.SetInteractable(false);
                
                var placements = placementMap.GetPlacements();
                var coordinates = new int[7][];
                var directions = new int[7];
                foreach (var placement in placements)
                {
                    int idx = placement.ship.rankOrder;
                    coordinates[idx] = new int[] { placements[idx].Coordinate.x, placements[idx].Coordinate.y };
                    directions[idx] = (int)placements[idx].ship.CurrentDirection;
                    Debug.Log($"Placement: rankOrder={placement.ship.rankOrder}, position={placement.Coordinate}, direction={placement.ship.CurrentDirection}");
                }
                
                // 发送舰队位置、方向和单元格数据
                _client.SendPlacement(_cells, directions, coordinates);
                statusData.State = WaitingOpponentPlacement;
            }

            void ResetPlacementMap()
            {
                BeginShipPlacement();
                planMap.ClearAllShips();
                gridSpriteMapper.ClearSpritePositions();
                gridSpriteMapper.CacheSpritePositions();
                poolGridSpriteMapper.ClearSpritePositions();
                poolGridSpriteMapper.CacheSpritePositions();
            }

            void BeginShipPlacement()
            {
                randomButton.SetInteractable(true);
                clearButton.SetInteractable(false);
                continueButton.SetInteractable(false);
                statusData.State = BeginPlacement;
                placementMap.Clear();
                _cells = new int[_cellCount];
                for (var i = 0; i < _cellCount; i++) _cells[i] = EmptyCell;
                _shipStates.Clear();
                _placements.Clear();
                PopulateShipPool();

                void PopulateShipPool()
                {
                    _pool = new SortedDictionary<int, Ship>();
                    var shipId = 0;
                    foreach (var ship in rules.ships)
                        for (var i = 0; i < ship.amount; i++)
                        {
                            _pool.Add(shipId, ship);
                            // 初始化船只状态
                            _shipStates[shipId] = new ShipState(shipId, ship.rankOrder);
                            shipId++;
                        }
                    
                    Debug.Log($"初始化船只池，共 {_pool.Count} 艘船");
                }
            }
        }

        private void Update()
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame) LeaveGame();
        }

        private void OnDestroy()
        {
            if (_client == null) return;
            _client.GamePhaseChanged -= OnGamePhaseChanged;
        }

        public bool OnShipMoved(Ship ship, Vector3Int from, Vector3Int to, bool isMovedIn, bool isRotation = false)
        {
            bool result = PlaceShip(ship, from, to, isMovedIn, isRotation);
            
            // 每次移动船只后，更新所有船只的状态
            if (result || isRotation)
            {
                // 确保_placements是最新的
                _placements = placementMap.GetPlacements();
                UpdateAllShipStates();
            }
            
            return result;
        }

        private void LeaveGame()
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
        }

        private void OnGamePhaseChanged(string phase)
        {
            switch (phase)
            {
                case RoomPhase.Battle:
                    GameSceneManager.Instance.GoToBattleScene();
                    break;
                case RoomPhase.Result:
                    break;
                case RoomPhase.Waiting:
                    leaveMessageDialog.Show(() => GameSceneManager.Instance.GoToLobby());
                    break;
                case RoomPhase.Leave:
                    break;
            }
        }
        
        // 更新所有船只的状态
        private void UpdateAllShipStates()
        {
            Debug.Log("开始更新所有船只状态");
            
            // 先重置所有船只的碰撞状态
            foreach (var state in _shipStates.Values)
            {
                state.HasCollision = false;
                state.CollidingWithShips.Clear();
            }
            
            // 确保_placements是最新的
            _placements = placementMap.GetPlacements();
            Debug.Log($"已放置的船只数量: {_placements.Count}");
            
            // 检查每艘船的边界和碰撞状态
            foreach (var placement in _placements)
            {
                int shipId = placement.shipId;
                Ship ship = placement.ship;
                Vector3Int position = placement.Coordinate;
                
                // 确保船只状态存在
                if (!_shipStates.ContainsKey(shipId))
                {
                    Debug.LogWarning($"船只 {shipId} 的状态不存在，创建新状态");
                    _shipStates[shipId] = new ShipState(shipId, ship.rankOrder);
                }
                
                // 更新船只位置和方向
                _shipStates[shipId].Position = position;
                _shipStates[shipId].Direction = ship.CurrentDirection;
                
                // 检查是否超出边界
                (int shipWidth, int shipHeight) = ship.GetShipSize();
                bool isInsideBoundaries = IsInsideBoundaries(shipWidth, shipHeight, position, MapAreaSize, ship.CurrentDirection);
                _shipStates[shipId].IsOutOfBounds = !isInsideBoundaries;
                
                if (!isInsideBoundaries)
                {
                    Debug.LogWarning($"船只 {shipId} (rankOrder={ship.rankOrder}) 超出边界: 位置={position}, 尺寸={shipWidth}x{shipHeight}, 方向={ship.CurrentDirection}");
                }
                
                // 检查是否与其他船只重叠
                foreach (var otherPlacement in _placements)
                {
                    // 跳过自己
                    if (otherPlacement.shipId == shipId) continue;
                    
                    // 检查碰撞
                    if (DoShipsCollide(ship, position, otherPlacement.ship, otherPlacement.Coordinate))
                    {
                        _shipStates[shipId].HasCollision = true;
                        _shipStates[shipId].CollidingWithShips.Add(otherPlacement.shipId);
                        Debug.LogWarning($"船只 {shipId} (rankOrder={ship.rankOrder}) 与船只 {otherPlacement.shipId} (rankOrder={otherPlacement.ship.rankOrder}) 重叠");
                    }
                }
                
                // 更新有效性
                _shipStates[shipId].IsValid = !_shipStates[shipId].IsOutOfBounds && !_shipStates[shipId].HasCollision;
                
                Debug.Log($"更新船只 {shipId} (rankOrder={ship.rankOrder}): 位置={position}, 有效={_shipStates[shipId].IsValid}, 超出边界={_shipStates[shipId].IsOutOfBounds}, 有碰撞={_shipStates[shipId].HasCollision}");
            }
            
            // 更新继续按钮状态
            UpdateContinueButtonState();
        }
        
        // 检查两艘船是否碰撞
        private bool DoShipsCollide(Ship ship1, Vector3Int pos1, Ship ship2, Vector3Int pos2)
        {
            // 获取船只1的所有部件位置
            var parts1 = new List<Vector3Int>();
            foreach (var part in ship1.partCoordinates)
            {
                parts1.Add(new Vector3Int(pos1.x + part.x, pos1.y + part.y, 0));
            }
            
            // 获取船只2的所有部件位置
            var parts2 = new List<Vector3Int>();
            foreach (var part in ship2.partCoordinates)
            {
                parts2.Add(new Vector3Int(pos2.x + part.x, pos2.y + part.y, 0));
            }
            
            // 检查是否有任何部件重叠
            foreach (var part1 in parts1)
            {
                foreach (var part2 in parts2)
                {
                    if (part1.x == part2.x && part1.y == part2.y)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }
        
        // 更新继续按钮状态
        private void UpdateContinueButtonState()
        {
            Debug.Log("更新Continue按钮状态");
            
            // 检查是否所有船只都已放置且有效
            bool allShipsValid = true;
            bool allShipsPlaced = true;
            
            // 检查是否所有必要的船只都已放置
            var placedRankOrders = new HashSet<int>();
            foreach (var placement in _placements)
            {
                placedRankOrders.Add(placement.ship.rankOrder);
            }
            
            Debug.Log($"已放置的船只rankOrder: {string.Join(", ", placedRankOrders)}");
            
            // 检查是否所有必要的船只类型都已放置
            foreach (var ship in rules.ships)
            {
                if (!placedRankOrders.Contains(ship.rankOrder))
                {
                    allShipsPlaced = false;
                    Debug.LogWarning($"缺少rankOrder={ship.rankOrder}的船只");
                    break;
                }
            }
            
            // 检查所有已放置船只的有效性
            foreach (var state in _shipStates.Values)
            {
                // 只检查已放置的船只
                if (placedRankOrders.Contains(state.RankOrder))
                {
                    if (!state.IsValid)
                    {
                        allShipsValid = false;
                        Debug.LogWarning($"船只 {state.ShipId} (rankOrder={state.RankOrder}) 无效: 超出边界={state.IsOutOfBounds}, 有碰撞={state.HasCollision}");
                        break;
                    }
                }
            }
            
            // 检查池中是否还有船只
            bool poolEmpty = _pool.Count == 0;
            if (!poolEmpty)
            {
                Debug.LogWarning($"池中还有 {_pool.Count} 艘船未放置");
            }
            
            // 只有当所有必要的船只都已放置且有效时，才启用Continue按钮
            bool shouldEnableContinue = allShipsPlaced && allShipsValid && poolEmpty;
            continueButton.SetInteractable(shouldEnableContinue);
            
            Debug.Log($"Continue按钮状态: {(shouldEnableContinue ? "启用" : "禁用")}, allShipsPlaced={allShipsPlaced}, allShipsValid={allShipsValid}, poolEmpty={poolEmpty}");
            
            // 更新游戏状态
            if (allShipsPlaced && allShipsValid && poolEmpty)
            {
                statusData.State = PlacementReady;
            }
            else if (!allShipsPlaced || !poolEmpty)
            {
                statusData.State = BeginPlacement;
            }
            else
            {
                statusData.State = PlacementImpossible;
            }
        }
        
        private bool PlaceShip(Ship ship, Vector3Int from, Vector3Int to, bool isMovedIn, bool isRotation = false, int shipId = EmptyCell)
        {
            var shouldRemoveFromPool = false;
            if (shipId == EmptyCell)
            {
                shipId = GetShipId(from, ship, isMovedIn);
                shouldRemoveFromPool = true;
            }
            
            Debug.Log($"尝试放置船只 {shipId} (rankOrder={ship.rankOrder}): 从 {from} 到 {to}, isMovedIn={isMovedIn}, isRotation={isRotation}, shouldRemoveFromPool={shouldRemoveFromPool}");
            
            // 确保船只状态存在
            if (!_shipStates.ContainsKey(shipId))
            {
                _shipStates[shipId] = new ShipState(shipId, ship.rankOrder);
            }

            (int shipWidth, int shipHeight) = ship.GetShipSize();
            bool isInsideBoundaries = IsInsideBoundaries(shipWidth, shipHeight, to, MapAreaSize, ship.CurrentDirection);
            
            // 如果是旋转操作，即使超出边界或有重叠，也允许显示，但标记为无效
            if (isRotation)
            {
                Debug.Log($"旋转操作: 船只 {shipId} 在 {to}, 是否在边界内: {isInsideBoundaries}");
                
                // 无论是否有效，都先清除旧的单元格数据
                ClearShipFromCells(shipId);
                
                // 在地图上显示旋转后的船只
                planMap.SetShip(ship, to);
                
                // 更新PlacementMap
                placementMap.PlaceShip(shipId, ship, to);
                
                // 只在基点位置设置单元格，其他部分不设置
                int baseCellIndex = CoordinateToCellIndex(to, MapAreaSize);
                if (baseCellIndex != OutOfMap)
                {
                    _cells[baseCellIndex] = shipId;
                }
                
                // 更新船只状态
                _shipStates[shipId].Position = to;
                _shipStates[shipId].Direction = ship.CurrentDirection;
                
                // 确保_placements是最新的
                _placements = placementMap.GetPlacements();
                
                // 返回true表示旋转操作成功（即使船只位置无效）
                return true;
            }
            
            // 如果是拖拽操作，检查是否有效
            if (!isInsideBoundaries)
            {
                Debug.Log($"无效放置: 船只 {shipId} 在 {to} 超出边界");
                return false;
            }
            
            // 检查是否与其他船只重叠
            bool hasCollision = false;
            foreach (var placement in _placements)
            {
                // 跳过自己
                if (placement.shipId == shipId) continue;
                
                // 检查碰撞
                if (DoShipsCollide(ship, to, placement.ship, placement.Coordinate))
                {
                    hasCollision = true;
                    Debug.Log($"无效放置: 船只 {shipId} 在 {to} 与船只 {placement.shipId} 重叠");
                    break;
                }
            }
            
            if (hasCollision)
            {
                return false;
            }
            
            // 如果是有效的拖拽操作，清除旧位置
            ClearShipFromCells(shipId);
            
            // 设置新位置
            clearButton.SetInteractable(true);
            planMap.SetShip(ship, to);
            RegisterShipToCells(shipId, ship, to);
            placementMap.PlaceShip(shipId, ship, to);
            
            // 更新船只状态
            _shipStates[shipId].Position = to;
            _shipStates[shipId].Direction = ship.CurrentDirection;
            _shipStates[shipId].IsValid = true;
            _shipStates[shipId].IsOutOfBounds = false;
            _shipStates[shipId].HasCollision = false;
            _shipStates[shipId].CollidingWithShips.Clear();
            
            // 从池中移除船只（只有在非随机放置模式下才移除）
            if (!_isRandomPlacing && _pool.ContainsKey(shipId))
            {
                _pool.Remove(shipId);
                _shipsNotDragged = _pool.Keys.ToList();
                Debug.Log($"从池中移除船只 {shipId}，池中剩余 {_pool.Count} 艘船");
            }
            
            // 确保_placements是最新的
            _placements = placementMap.GetPlacements();
            
            if (_pool.Count == 0)
            {
                randomButton.SetInteractable(false);
                Debug.Log("所有船只已放置，禁用Random按钮");
            }
            
            return true;
        }

        private void ClearShipFromCells(int shipId)
        {
            // 清除指定船只的所有格子
            int clearedCount = 0;
            for (var i = 0; i < _cellCount; i++)
            {
                if (_cells[i] == shipId)
                {
                    _cells[i] = EmptyCell;
                    clearedCount++;
                }
            }
            Debug.Log($"清除了船只 {shipId} 的 {clearedCount} 个单元格");
        }

        private int GetShipId(Vector3Int grabbedFrom, Ship ship, bool isMovedIn)
        {
            if (!isMovedIn)
            {
                int cellIndex = CoordinateToCellIndex(grabbedFrom, MapAreaSize);
                if (cellIndex != OutOfMap && _cells[cellIndex] != EmptyCell)
                {
                    return _cells[cellIndex];
                }
            }

            foreach (var kvp in _pool.Where(kvp => kvp.Value.rankOrder == ship.rankOrder)) return kvp.Key;

            return EmptyCell;
        }

        bool DoesCollideWithOtherShip(int selfShipId, Vector3Int cellCoordinate, Ship ship)
        {
            // 使用船只的实际部件坐标来检查重叠
            foreach (var part in ship.partCoordinates)
            {
                int checkX = cellCoordinate.x + part.x;
                int checkY = cellCoordinate.y + part.y;

                // 如果超出边界，跳过这部分的碰撞检测
                if (checkX < 0 || checkX >= MapAreaSize.x || checkY < 0 || checkY >= MapAreaSize.y) continue;

                int cellIndex = CoordinateToCellIndex(new Vector3Int(checkX, checkY, 0), MapAreaSize);
                if (cellIndex != OutOfMap && _cells[cellIndex] != EmptyCell && _cells[cellIndex] != selfShipId)
                {
                    return true; // 发现重叠
                }
            }
            return false;
        }

        private void RegisterShipToCells(int shipId, Ship ship, Vector3Int pivot)
        {
            // 清除这个船只之前占用的所有单元格
            ClearShipFromCells(shipId);

            // 为船只的每个部件设置单元格
            int registeredCount = 0;
            foreach (var part in ship.partCoordinates)
            {
                int checkX = pivot.x + part.x;
                int checkY = pivot.y + part.y;
                
                // 只注册在地图边界内的部分
                if (checkX >= 0 && checkX < MapAreaSize.x && checkY >= 0 && checkY < MapAreaSize.y)
                {
                    int cellIndex = CoordinateToCellIndex(new Vector3Int(checkX, checkY, 0), MapAreaSize);
                    if (cellIndex != OutOfMap)
                    {
                        _cells[cellIndex] = shipId;
                        registeredCount++;
                    }
                }
            }
            
            Debug.Log($"注册了船只 {shipId} 在 {pivot} 方向 {ship.CurrentDirection}，共 {registeredCount} 个单元格");
        }

        // 新增：武将选择弹窗逻辑
        private void OnHeroButtonClicked()
        {
            // 显示HeroBox
            if (HeroBox != null)
            {
                HeroBox.SetActive(true);
                Debug.Log("显示HeroBox");
            }
            else
            {
                Debug.LogWarning("HeroBox 未在Inspector中赋值！");
            }
        }
        
        // 新增：确认武将选择按钮点击回调
        private void OnOkHeroButtonClicked()
        {
            // 隐藏HeroBox
            if (HeroBox != null)
            {
                HeroBox.SetActive(false);
                Debug.Log("隐藏HeroBox，确认武将选择");
            }
            else
            {
                Debug.LogWarning("HeroBox 为 null，无法隐藏！");
            }
            
            // 这里可以添加确认选择后的逻辑
            if (GameManager.TryGetInstance(out var gameManager))
            {
                gameManager.SelectedHeroId = selectedHeroId;
                Debug.Log($"确认选择武将：{selectedHeroId}");
            }
        }
    }
}