using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq; 
namespace BattleshipGame.Core
{
    [CreateAssetMenu(fileName = "New Ship", menuName = "Battleship/Ship", order = 0)]
    public class Ship : ScriptableObject
    {
        public Tile tile;

        [Tooltip("Smallest number means the highest rank.")]
        public int rankOrder;

        [Tooltip("How many of this ship does each player have?")]
        public int amount;

        [Tooltip("Start with the sprite's pivot. First value must be (0, 0)")]
        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        public List<Vector2Int> partCoordinates = new List<Vector2Int>();

        private void OnValidate()
        {
            if (partCoordinates.Count == 0)
                partCoordinates.Add(Vector2Int.zero);
            else
                partCoordinates[0] = Vector2Int.zero;
        }

        [SerializeField] private Direction _currentDirection = Direction.Right; // 当前方向
        public Direction CurrentDirection => _currentDirection;

        // 逆时针旋转90°
        public void RotateCounterclockwise()
        {
            _currentDirection = (Direction)(((int)_currentDirection + 1) % 4);
        }

        // 根据方向返回船只尺寸（修复尺寸计算逻辑）
        public (int width, int height) GetShipSize()
        {
            // 计算原始尺寸（考虑正负坐标）
            var minX = partCoordinates.Min(p => p.x);
            var maxX = partCoordinates.Max(p => p.x);
            var minY = partCoordinates.Min(p => p.y);
            var maxY = partCoordinates.Max(p => p.y);
            var originalSize = (shipWidth: maxX - minX + 1, shipHeight: maxY - minY + 1);

            // 旋转后宽高互换（仅当方向为Up或Down时）
            return _currentDirection == Direction.Right || _currentDirection == Direction.Left 
                ? (originalSize.shipWidth, originalSize.shipHeight) 
                : (originalSize.shipHeight, originalSize.shipWidth);
        }

        // 根据方向计算实际占据的单元格（修复坐标变换逻辑）
        public List<Vector3Int> GetOccupiedCells(Vector3Int anchor)
        {
            return partCoordinates.Select(p =>
            {
                // 修正逆时针旋转的坐标变换公式（符合Unity 2D坐标系）
                return _currentDirection switch
                {
                    Direction.Right => new Vector3Int(p.x, p.y, 0),       // 原始方向（右）
                    Direction.Up => new Vector3Int(-p.y, p.x, 0),         // 逆时针90°：(x,y)→(-y,x)（向上延伸）
                    Direction.Left => new Vector3Int(-p.x, -p.y, 0),      // 逆时针180°：(x,y)→(-x,-y)（向左延伸）
                    Direction.Down => new Vector3Int(p.y, -p.x, 0),       // 逆时针270°：(x,y)→(y,-x)（向下延伸）
                    _ => new Vector3Int(p.x, p.y, 0)
                };
            }).Select(offset => anchor + offset).ToList();
        }
    }
}