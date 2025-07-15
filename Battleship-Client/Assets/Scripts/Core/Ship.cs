using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
namespace BattleshipGame.Core
{
    [CreateAssetMenu(fileName = "New Ship", menuName = "Battleship/Ship", order = 0)]
    public class Ship : ScriptableObject
    {
        public List<Tile> tiles=new List<Tile>();
        //public Tile tile;
        public Tile tile{
            get{
                if (tiles.Count == 0) return null;
                if (tiles.Count == 1) return tiles[0];
                return _currentDirection switch
                {
                    Direction.Right => tiles[0],
                    Direction.Up => tiles[1],
                    Direction.Left => tiles[2],
                    Direction.Down => tiles[3],
                    _ => tiles[0]
                };
            }
            set{
                tiles[0]=value;
            }
        }
        [Tooltip("Smallest number means the highest rank.")]
        public int rankOrder;

        [Tooltip("How many of this ship does each player have?")]
        public int amount;

        [Tooltip("Start with the sprite's pivot. First value must be (0, 0)")]
        // ReSharper disable once FieldCanBeMadeReadOnly.Global
        public List<Vector2Int> PartCoordinates = new List<Vector2Int>();
        /// <summary>
        /// 根据当前方向获取船只部件的坐标
        /// </summary>
        public List<Vector2Int> partCoordinates
        {
            get
            {
                return PartCoordinates.Select(p =>
                {
                    return _currentDirection switch
                    {
                        Direction.Right => new Vector2Int(p.x, p.y),       // 原始方向（右）
                        Direction.Up => new Vector2Int(-p.y, p.x),         // 逆时针90°：(x,y)→(-y,x)（向上延伸）
                        Direction.Left => new Vector2Int(-p.x, -p.y),      // 逆时针180°：(x,y)→(-x,-y)（向左延伸）
                        Direction.Down => new Vector2Int(p.y, -p.x),       // 逆时针270°：(x,y)→(y,-x)（向下延伸）
                        _ => new Vector2Int(p.x, p.y)
                    };
                }).ToList();
            }
        }

        private void OnValidate()
        {
            if (PartCoordinates.Count == 0)
                PartCoordinates.Add(Vector2Int.zero);
            else
                PartCoordinates[0] = Vector2Int.zero;
        }

        [SerializeField] 
        private Direction _currentDirection = Direction.Right; // 当前方向
        private void OnEnable()
        {
            _currentDirection = Direction.Right;
        }
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
            var minX = PartCoordinates.Min(p => p.x);
            var maxX = PartCoordinates.Max(p => p.x);
            var minY = PartCoordinates.Min(p => p.y);
            var maxY = PartCoordinates.Max(p => p.y);
            var originalSize = (shipWidth: maxX - minX + 1, shipHeight: maxY - minY + 1);

            // 旋转后宽高互换（仅当方向为Up或Down时）
            return _currentDirection == Direction.Right || _currentDirection == Direction.Left 
                ? (originalSize.shipWidth, originalSize.shipHeight) 
                : (originalSize.shipHeight, originalSize.shipWidth);
        }

        // 根据方向计算实际占据的单元格（修复坐标变换逻辑）
        public List<Vector3Int> GetOccupiedCells(Vector3Int anchor)
        {
            var result=PartCoordinates.Select(p =>
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
            return result;
        }

    }
}