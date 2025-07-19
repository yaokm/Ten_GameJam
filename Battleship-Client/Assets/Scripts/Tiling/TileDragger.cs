using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

namespace BattleshipGame.Tiling
{
    [RequireComponent(typeof(Grid), typeof(BoxCollider2D))]
    public class TileDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] private GameObject dragShipPrefab;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Rules rules;
        [SerializeField] private Map targetMap;
        [SerializeField] private Tilemap sourceTileMap;
        [SerializeField] private bool removeFromSource;
        [SerializeField] private bool removeIfDraggedOut;
        [SerializeField] private bool isCanRotate=false;
        private GameObject _grabbedShip;
        private Vector3Int _grabCell;
        private Vector3 _grabOffset;
        private Grid _grid;
        private bool _isGrabbedFromTarget;
        private GridSpriteMapper _selfGridSpriteMapper;
        private Sprite _sprite;
        private GridSpriteMapper _targetGridSpriteMapper;
        [SerializeField] private PlacementMap placementMap;
        
        // 旋转相关标志
        private bool _recentlyRotated = false;
        private Ship _lastRotatedShip = null;
        private Vector3Int _lastRotatedPosition = Vector3Int.zero;
        
        // 拖拽相关标志
        private bool _isDraggingAfterRotation = false;

        private void Start()
        {
            _grid = GetComponent<Grid>();
            _selfGridSpriteMapper = GetComponent<GridSpriteMapper>();
            _targetGridSpriteMapper = targetMap.GetComponent<GridSpriteMapper>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isGrabbedFromTarget = eventData.hovered.Contains(targetMap.gameObject);
            _grabCell = GridUtils.ScreenToCell(eventData.position, sceneCamera, _grid, rules.areaSize);
            
            Debug.Log($"开始拖拽: 单元格 {_grabCell}, 是否从目标地图拖拽: {_isGrabbedFromTarget}");
            
            _sprite = _selfGridSpriteMapper.GetSpriteAt(ref _grabCell);

            var grabPoint = transform.position + _grabCell + new Vector3(0.5f, 0.5f, 0);
            _grabOffset = grabPoint - GetWorldPoint(eventData.position);
            if (!_sprite)
            {
                Debug.Log("在拖拽位置未找到精灵");
                return;
            }
            
            // 检查是否是船只
            if (SpriteRepresentsShip(out var ship))
            {
                Debug.Log($"找到船只: rankOrder={ship.rankOrder}, 方向={ship.CurrentDirection}");
                
                // 检查是否是刚刚旋转过的船只
                bool isRotatedShip = _recentlyRotated && _lastRotatedShip != null && 
                                    ship.rankOrder == _lastRotatedShip.rankOrder && 
                                    _grabCell == _lastRotatedPosition;
                
                if (isRotatedShip)
                {
                    _isDraggingAfterRotation = true;
                    Debug.Log($"拖拽刚旋转过的船只: {_grabCell}, rankOrder={ship.rankOrder}");
                }
                
                // 创建拖动时显示的船只
                _grabbedShip = Instantiate(dragShipPrefab, _grabCell, Quaternion.identity);
                float rotationAngle = (int)ship.CurrentDirection * 90f;
                _grabbedShip.transform.rotation = Quaternion.Euler(0, 0, rotationAngle);
                _grabbedShip.GetComponent<SpriteRenderer>().sprite = _sprite;
                
                // 如果是从源地图拖动，并且不是刚旋转过的船只，则移除源地图上的瓦片
                if (removeFromSource && !isRotatedShip)
                {
                    Debug.Log($"从源地图移除瓦片: {_grabCell}");
                    sourceTileMap.SetTile(_grabCell, null);
                }
                else if (isRotatedShip)
                {
                    Debug.Log($"不移除瓦片，因为是刚旋转过的船只");
                }
            }
            else
            {
                Debug.Log("未找到对应的船只");
                return;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_grabbedShip) _grabbedShip.transform.position = GetWorldPoint(eventData.position) + _grabOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_grabbedShip) return;

            Debug.Log("结束拖拽");
            
            var isOverTheTarget = false;
#if UNITY_ANDROID || UNITY_IOS
            var ray = sceneCamera.ScreenPointToRay(eventData.position);
            var raycast2d = Physics2D.Raycast(ray.origin, ray.direction, 100);
            if (raycast2d) isOverTheTarget = raycast2d.transform.gameObject.Equals(targetMap.gameObject);
#else
            isOverTheTarget = eventData.hovered.Contains(targetMap.gameObject);
#endif

            // 如果拖出目标区域且设置了移除
            if (removeIfDraggedOut && !isOverTheTarget)
            {
                if (_targetGridSpriteMapper)
                    _targetGridSpriteMapper.RemoveSpritePosition(_sprite, _grabCell);
                Destroy(_grabbedShip);
                _isDraggingAfterRotation = false;
                return;
            }

            if (SpriteRepresentsShip(out var ship))
            {
                var grid = targetMap.GetComponent<Grid>();
                var dropWorldPoint = GetWorldPoint(eventData.position) + _grabOffset;
                var dropCell = GridUtils.WorldToCell(dropWorldPoint, sceneCamera, grid, rules.areaSize);
                
                Debug.Log($"拖拽结束: 从 {_grabCell} 到 {dropCell}, rankOrder={ship.rankOrder}, 是否旋转后拖拽={_isDraggingAfterRotation}");
                
                // 尝试移动船只
                bool moveSuccess = false;
                
                // 如果在目标区域上方
                if (isOverTheTarget && _targetGridSpriteMapper)
                {
                    // 如果是旋转后的拖拽，先清除原位置的船只
                    if (_isDraggingAfterRotation)
                    {
                        Debug.Log($"特殊处理旋转后的拖拽: 从 {_grabCell} 到 {dropCell}");
                        sourceTileMap.SetTile(_grabCell, null);
                    }
                    
                    // 尝试移动船只
                    moveSuccess = targetMap.MoveShip(ship, _grabCell, dropCell, !_isGrabbedFromTarget);
                    
                    if (moveSuccess)
                    {
                        Debug.Log($"成功移动船只到 {dropCell}");
                        
                        // 更新网格位置
                        if (_isDraggingAfterRotation || removeFromSource)
                        {
                            _selfGridSpriteMapper.RemoveSpritePosition(_sprite, _grabCell);
                        }
                        
                        _targetGridSpriteMapper.ChangeSpritePosition(_sprite, _grabCell, dropCell);
                    }
                    else
                    {
                        Debug.Log($"移动船只到 {dropCell} 失败");
                        
                        // 如果移动失败，恢复原位置的船只
                        if (removeFromSource && !_isDraggingAfterRotation)
                        {
                            RestoreShipTile(ship, _grabCell);
                        }
                        else if (_isDraggingAfterRotation)
                        {
                            RestoreShipTile(ship, _grabCell);
                        }
                    }
                }
                else if (removeFromSource && !_isDraggingAfterRotation)
                {
                    // 如果不在目标上方，恢复原位置的船只
                    RestoreShipTile(ship, _grabCell);
                }
            }

            // 重置标志
            _isDraggingAfterRotation = false;
            _recentlyRotated = false;
            _lastRotatedShip = null;
            
            Destroy(_grabbedShip);
        }
        
        // 恢复船只瓦片到指定位置
        private void RestoreShipTile(Ship ship, Vector3Int position)
        {
            Debug.Log($"恢复船只瓦片到 {position}");
            sourceTileMap.SetTile(position, ship.tile);
            
            // 设置旋转角度
            var tileTransform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, (int)ship.CurrentDirection * 90f));
            sourceTileMap.SetTransformMatrix(position, tileTransform);
        }

        private bool SpriteRepresentsShip(out Ship spriteShip)
        {
            if (_sprite == null)
            {
                spriteShip = null;
                return false;
            }
            foreach (var ship in rules.ships.Where(ship => ship.tile.sprite.Equals(_sprite)))
            {
                spriteShip = ship;
                return true;
            }

            spriteShip = null;
            return false;
        }

        private Vector3 GetWorldPoint(Vector2 position)
        {
            var worldPoint = sceneCamera.ScreenToWorldPoint(position);
            return new Vector3(worldPoint.x, worldPoint.y, 0);
        }

        // 处理点击事件（旋转）
        public void OnPointerClick(PointerEventData eventData)
        {
            // 如果不允许旋转或正在拖拽，跳过
            if (!isCanRotate)
            {
                return;
            }
            if (_grabbedShip != null)
            {
                Debug.Log("正在拖拽中，跳过旋转");
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            // 获取点击的单元格和精灵
            var clickCell = GridUtils.ScreenToCell(eventData.position, sceneCamera, _grid, rules.areaSize);
            var anchorCell = clickCell;
            Sprite clickedSprite = _selfGridSpriteMapper.GetSpriteAt(ref anchorCell);
            if (clickedSprite == null)
            {
                Debug.Log($"在 {clickCell} 没有找到精灵");
                return;
            }

            // 检查是否是船只
            if (!SpriteRepresentsShip(out var ship, clickedSprite))
            {
                Debug.Log("精灵不是船只");
                return;
            }

            // 执行旋转
            RotateShip(ship, anchorCell);
        }

        private bool SpriteRepresentsShip(out Ship spriteShip, Sprite targetSprite)
        {
            if (targetSprite == null)
            {
                spriteShip = null;
                return false;
            }
            foreach (var ship in rules.ships.Where(ship => ship.tile.sprite.Equals(targetSprite)))
            {
                spriteShip = ship;
                return true;
            }

            spriteShip = null;
            return false;
        }

        private void RotateShip(Ship ship, Vector3Int anchorCell)
        {
            // 记录原方向
            var originalDirection = ship.CurrentDirection;

            // 执行逆时针旋转
            ship.RotateCounterclockwise();
            
            Debug.Log($"旋转船只: rankOrder={ship.rankOrder}, 位置={anchorCell}, 方向={ship.CurrentDirection}");

            // 更新Tilemap中的显示
            UpdateShipSprite(ship, anchorCell);
            
            // 通知地图进行旋转处理
            bool rotationResult = targetMap.MoveShip(ship, anchorCell, anchorCell, false, true);
            Debug.Log($"旋转结果: {rotationResult}");
            
            // 设置旋转标志
            _recentlyRotated = true;
            _lastRotatedShip = ship;
            _lastRotatedPosition = anchorCell;
            
            // 更新地图记录
            _selfGridSpriteMapper.ClearSpritePositions();
            _selfGridSpriteMapper.CacheSpritePositions();
        }

        // 更新船只在Tilemap中的显示
        private void UpdateShipSprite(Ship ship, Vector3Int anchorCell)
        {
            // 设置旋转角度
            var tileTransform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, (int)ship.CurrentDirection * 90f));
            sourceTileMap.SetTransformMatrix(anchorCell, tileTransform);

            // 如果有多个瓦片（不同方向使用不同瓦片），更新瓦片
            if (ship.Tiles.Count > 1)
            {
                var oldtile = ScriptableObject.CreateInstance<Tile>();
                for (int i = 0; i < ship.Tiles.Count; i++)
                {
                    if (ship.Tiles[i] == ship.tile)
                    {
                        oldtile = ship.Tiles[i == 0 ? 3 : i - 1];
                        break;
                    }
                }
                sourceTileMap.SwapTile(oldtile, ship.tile);
            }
        }
    }
}