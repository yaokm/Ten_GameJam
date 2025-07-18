using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BattleshipGame.AI
{
    public class Enemy : MonoBehaviour
    {
        private const int EmptyCell = -1;
        private const int OutOfMap = -1;
        [SerializeField] private Rules rules;
        [SerializeField] private Options options;
        private readonly List<int> _playerShipsHealth = new List<int>();
        private readonly SortedDictionary<int, Ship> _pool = new SortedDictionary<int, Ship>();
        private readonly WaitForSeconds _thinkingSeconds = new WaitForSeconds(1f);
        private readonly List<int> _uncheckedCells = new List<int>();
        private Prediction _prediction;

        private void OnEnable()
        {
            InitUncheckedCells();
            PopulatePool();
            _prediction = new Prediction(rules, _uncheckedCells, _playerShipsHealth);

            void InitUncheckedCells()
            {
                _uncheckedCells.Clear();
                int cellCount = rules.areaSize.x * rules.areaSize.y;
                for (var i = 0; i < cellCount; i++) _uncheckedCells.Add(i);
            }

            void PopulatePool()
            {
                _pool.Clear();
                _playerShipsHealth.Clear();
                var shipId = 0;
                foreach (var ship in rules.ships)
                    for (var i = 0; i < ship.amount; i++)
                    {
                        _pool.Add(shipId, ship);
                        _playerShipsHealth.Add(ship.partCoordinates.Count);
                        shipId++;
                    }
            }
        }

        private void OnDisable()
        {
            _prediction = null;
        }

        public void ResetForRematch()
        {
            OnEnable();
        }

        public void UpdatePlayerShips(int changedShipPart, int shotTurn)
        {
            var partIndex = 0;
            foreach (var ship in _pool)
            foreach (var _ in ship.Value.partCoordinates)
            {
                if (changedShipPart == partIndex)
                    _playerShipsHealth[ship.Key]--;
                partIndex++;
            }

            if (options.aiDifficulty == Difficulty.Hard) _prediction?.Update(_playerShipsHealth.ToList(), _pool);
        }

        public IEnumerator GetShots(Action<int[]> onComplete)
        {
            yield return _thinkingSeconds;
            switch (options.aiDifficulty)
            {
                case Difficulty.Easy:
                    onComplete?.Invoke(GetRandomCells());
                    break;
                case Difficulty.Hard:
                    onComplete?.Invoke(GetPredictedCells());
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private int[] GetPredictedCells()
        {
            if (_prediction == null || _uncheckedCells.Count == 0) return null;
            var shipsRemaining = new List<int>();
            for (var shipId = 0; shipId < _playerShipsHealth.Count; shipId++)
                if (_playerShipsHealth[shipId] > 0)
                    shipsRemaining.Add(shipId);

            int size = rules.shotsPerTurn;
            int[] cells = _prediction.GetMostProbableCells(size, shipsRemaining).ToArray();
            for (var i = 0; i < size; i++) _uncheckedCells.Remove(cells[i]);
            return cells;
        }
        public Rules GetRules()
        {
            return rules;
        }
        private int[] GetRandomCells()
        {
            if (_prediction == null || _uncheckedCells == null) return null;
            int size = rules.shotsPerTurn;
            var cells = new int[size];
            if (_uncheckedCells.Count == 0) return cells;
            for (var i = 0; i < size; i++)
            {
                int index = Random.Range(0, _uncheckedCells.Count);
                cells[i] = _uncheckedCells[index];
                _uncheckedCells.Remove(cells[i]);
            }

            return cells;
        }
        private int[] _directions=new int[8];
        private int[][] _basePositions=new int[8][];
        public int[] GetDirections()
        {
            return _directions;
        }
        public int[][] GetBasePositions()
        {
            return _basePositions;
        }
        /// <summary>
        /// 随机在网格中放置所有船只，并返回表示网格状态的数组。
        /// 数组中的每个元素对应一个网格单元格，值为船只ID或 EmptyCell（-1）表示空单元格。
        /// </summary>
        /// <returns>表示网格状态的整数数组，数组索引对应单元格索引，值为船只ID或 EmptyCell。</returns>
        public int[] PlaceShipsRandomly()
        {
            // 计算网格的总单元格数量
            int cellCount = rules.areaSize.x * rules.areaSize.y;
            // 初始化一个数组来表示网格，所有单元格初始值设为 EmptyCell（-1），表示空单元格
            var cells = new int[cellCount];
            for (var i = 0; i < cellCount; i++) cells[i] = EmptyCell;
            // 遍历船只池中的每一艘船
            foreach (var kvp in _pool)
            {
                // 为每艘船随机选择一个方向
                kvp.Value._enemyDirection = (Direction)Random.Range(0, 4);                
                // 创建一个列表，包含所有可用的单元格索引
                var from = new List<int>();
                for (var i = 0; i < cellCount; i++) from.Add(i);
                var isPlaced = false;
                // 尝试在随机位置放置船只，直到放置成功或没有可用位置
                while (!isPlaced)
                {
                    // 如果没有可用位置，停止尝试
                    if (from.Count == 0) break;
                    // 随机选择一个可用的单元格
                    int cell = from[Random.Range(0, from.Count)];
                    // 从可用列表中移除已选择的单元格
                    from.Remove(cell);
                    // 将单元格索引转换为坐标，并尝试在该位置放置船只
                    isPlaced = PlaceShip(kvp.Key, kvp.Value, GridUtils.CellIndexToCoordinate(cell, rules.areaSize.x));
                }
            }

            return cells;

            /// <summary>
            /// 尝试在指定位置放置一艘船。
            /// </summary>
            /// <param name="shipId">要放置的船只ID。</param>
            /// <param name="ship">要放置的船只对象。</param>
            /// <param name="pivot">船只放置的基准点坐标。</param>
            /// <returns>如果船只成功放置返回 true，否则返回 false。</returns>
            bool PlaceShip(int shipId, Ship ship, Vector3Int pivot)
            {
                // 获取船只的宽度和高度
                (int shipWidth, int shipHeight) = ship.GetShipSize(true);                    
                // 检查船只是否在网格边界内，如果不在则返回 false
                if (!GridUtils.IsInsideBoundaries(shipWidth, shipHeight, pivot, rules.areaSize,ship._enemyDirection)) return false;
                // 检查船只是否与其他已放置的船只碰撞，如果碰撞则返回 false
                if (DoesCollideWithOtherShip(shipId, pivot, ship)) return false;
                // 如果通过上述检查，将船只注册到对应的单元格中
                RegisterShipToCells(shipId, ship, pivot);
                _directions[ship.rankOrder] = (int)ship._enemyDirection;
                _basePositions[ship.rankOrder] = new int[]{pivot.x,pivot.y};
                Debug.Log("shipWidth:"+shipWidth+"shipHeight:"+shipHeight+"pivot:"+pivot+" ship.rankOrder:"+ship.rankOrder+"ship._enemyDirection:"+ship._enemyDirection);
                return true;    
            }

            /// <summary>
            /// 检查要放置的船只是否与其他已放置的船只发生碰撞。
            /// </summary>
            /// <param name="shipId">要放置的船只ID。</param>
            /// <param name="pivot">船只放置的基准点坐标。</param>
            /// <param name="ship">要放置的船只对象。</param>
            /// <returns>如果发生碰撞返回 true，否则返回 false。</returns>
            bool DoesCollideWithOtherShip(int shipId, Vector3Int pivot, Ship ship)
            {
                // 使用船只的实际部件坐标来检查重叠
                foreach (var part in ship.EpartCoordinates)
                {
                    // 计算部件的实际坐标
                    int checkX = pivot.x + part.x;
                    int checkY = pivot.y + part.y;
                    
                    // 检查坐标是否在网格边界内，如果不在则跳过
                    if (checkX < 0 || checkX >= rules.areaSize.x || checkY < 0 || checkY >= rules.areaSize.y) continue;
                    
                    // 将坐标转换为单元格索引
                    int cellIndex = GridUtils.CoordinateToCellIndex(new Vector3Int(checkX, checkY, 0), rules.areaSize);
                    // 检查单元格是否已被其他船只占用
                    if (cellIndex != OutOfMap && cells[cellIndex] != EmptyCell && cells[cellIndex] != shipId)
                    {
                        return true; // 发现重叠
                    }
                }
                return false;
            }

            /// <summary>
            /// 将船只注册到对应的单元格中，并清除该船只之前的放置位置。
            /// </summary>
            /// <param name="shipId">要注册的船只ID。</param>
            /// <param name="ship">要注册的船只对象。</param>
            /// <param name="pivot">船只放置的基准点坐标。</param>
            void RegisterShipToCells(int shipId, Ship ship, Vector3Int pivot)
            {
                // 清除该船只之前的放置位置
                for (var i = 0; i < cellCount; i++)
                    if (cells[i] == shipId)
                        cells[i] = EmptyCell;

                // 找到船只覆盖的每个单元格，并在这些单元格上注册该船只
                foreach (int cellIndex in ship.EpartCoordinates
                    .Select(part => new Vector3Int(pivot.x + part.x, pivot.y + part.y, 0))
                    .Select(coordinate => GridUtils.CoordinateToCellIndex(coordinate, rules.areaSize)))
                    if (cellIndex != OutOfMap)
                        cells[cellIndex] = shipId;
            }
        }
        // {
        //     int cellCount = rules.areaSize.x * rules.areaSize.y;
        //     var cells = new int[cellCount];
        //     for (var i = 0; i < cellCount; i++) cells[i] = EmptyCell;
        //     foreach (var kvp in _pool)
        //     {
        //         var from = new List<int>();
        //         for (var i = 0; i < cellCount; i++) from.Add(i);
        //         var isPlaced = false;
        //         while (!isPlaced)
        //         {
        //             if (from.Count == 0) break;
        //             int cell = from[Random.Range(0, from.Count)];
        //             from.Remove(cell);
        //             isPlaced = PlaceShip(kvp.Key, kvp.Value, GridUtils.CellIndexToCoordinate(cell, rules.areaSize.x));
        //         }
        //     }

        //     return cells;

        //     bool PlaceShip(int shipId, Ship ship, Vector3Int pivot)
        //     {
        //         (int shipWidth, int shipHeight) = ship.GetShipSize();
        //         if (!GridUtils.IsInsideBoundaries(shipWidth, shipHeight, pivot, rules.areaSize)) return false;
        //         if (DoesCollideWithOtherShip(shipId, pivot, ship)) return false;
        //         RegisterShipToCells(shipId, ship, pivot);
        //         return true;
        //     }

        //     bool DoesCollideWithOtherShip(int shipId, Vector3Int pivot, Ship ship)
        //     {
        //         // 使用船只的实际部件坐标来检查重叠
        //         foreach (var part in ship.partCoordinates)
        //         {
        //             int checkX = pivot.x + part.x;
        //             int checkY = pivot.y + part.y;
                    
        //             if (checkX < 0 || checkX >= rules.areaSize.x || checkY < 0 || checkY >= rules.areaSize.y) continue;
                    
        //             int cellIndex = GridUtils.CoordinateToCellIndex(new Vector3Int(checkX, checkY, 0), rules.areaSize);
        //             if (cellIndex != OutOfMap && cells[cellIndex] != EmptyCell && cells[cellIndex] != shipId)
        //             {
        //                 return true; // 发现重叠
        //             }
        //         }
        //         return false;
        //     }

        //     void RegisterShipToCells(int shipId, Ship ship, Vector3Int pivot)
        //     {
        //         // Clear the previous placement of this ship
        //         for (var i = 0; i < cellCount; i++)
        //             if (cells[i] == shipId)
        //                 cells[i] = EmptyCell;

        //         // Find each cell the ship covers and register the ship on them
        //         foreach (int cellIndex in ship.partCoordinates
        //             .Select(part => new Vector3Int(pivot.x + part.x, pivot.y + part.y, 0))
        //             .Select(coordinate => GridUtils.CoordinateToCellIndex(coordinate, rules.areaSize)))
        //             if (cellIndex != OutOfMap)
        //                 cells[cellIndex] = shipId;
        //     }
        // }
    }
}