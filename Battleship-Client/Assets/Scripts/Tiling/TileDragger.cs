using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;

namespace BattleshipGame.Tiling
{
    [RequireComponent(typeof(Grid), typeof(BoxCollider2D))]
    public class TileDragger : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler // 新增接口
    {
        [SerializeField] private GameObject dragShipPrefab;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Rules rules;
        [SerializeField] private Map targetMap;
        [SerializeField] private Tilemap sourceTileMap;
        [SerializeField] private bool removeFromSource;
        [SerializeField] private bool removeIfDraggedOut;
        private GameObject _grabbedShip;
        private Vector3Int _grabCell;
        private Vector3 _grabOffset;
        private Grid _grid;
        private bool _isGrabbedFromTarget;
        private GridSpriteMapper _selfGridSpriteMapper;
        private Sprite _sprite;
        private GridSpriteMapper _targetGridSpriteMapper;

        private void Start()
        {
            _grid = GetComponent<Grid>();
            _selfGridSpriteMapper = GetComponent<GridSpriteMapper>();
            _targetGridSpriteMapper = targetMap.GetComponent<GridSpriteMapper>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log("OnBeginDrag");
            _isGrabbedFromTarget = eventData.hovered.Contains(targetMap.gameObject);
            _grabCell = GridUtils.ScreenToCell(eventData.position, sceneCamera, _grid, rules.areaSize);
            _sprite = _selfGridSpriteMapper.GetSpriteAt(ref _grabCell);

            var grabPoint = transform.position + _grabCell + new Vector3(0.5f, 0.5f, 0);
            _grabOffset = grabPoint - GetWorldPoint(eventData.position);
            if (!_sprite)
            {
                Debug.Log("No sprite");
                return;
            }else{
                Debug.Log(_sprite.name);
            }
            _grabbedShip = Instantiate(dragShipPrefab, _grabCell, Quaternion.identity);
            _grabbedShip.GetComponent<SpriteRenderer>().sprite = _sprite;
            if (removeFromSource) sourceTileMap.SetTile(_grabCell, null);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_grabbedShip) _grabbedShip.transform.position = GetWorldPoint(eventData.position) + _grabOffset;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_grabbedShip) return;

            var isOverTheTarget = false;
#if UNITY_ANDROID || UNITY_IOS
            var ray = sceneCamera.ScreenPointToRay(eventData.position);
            var raycast2d = Physics2D.Raycast(ray.origin, ray.direction, 100);
            if (raycast2d) isOverTheTarget = raycast2d.transform.gameObject.Equals(targetMap.gameObject);
#else
            isOverTheTarget = eventData.hovered.Contains(targetMap.gameObject);
#endif

            if (removeIfDraggedOut && !isOverTheTarget)
            {
                if (_targetGridSpriteMapper)
                    _targetGridSpriteMapper.RemoveSpritePosition(_sprite, _grabCell);
                Destroy(_grabbedShip);
                return;
            }

            if (SpriteRepresentsShip(out var ship))
            {
                var grid = targetMap.GetComponent<Grid>();
                var dropWorldPoint = GetWorldPoint(eventData.position) + _grabOffset;
                var dropCell = GridUtils.WorldToCell(dropWorldPoint, sceneCamera, grid, rules.areaSize);
                (int shipWidth, int shipHeight) = ship.GetShipSize();
                if (isOverTheTarget && _targetGridSpriteMapper &&
                    GridUtils.IsInsideBoundaries(shipWidth, shipHeight, dropCell, rules.areaSize) &&
                    targetMap.MoveShip(ship, _grabCell, dropCell, !_isGrabbedFromTarget))
                {
                    if (removeFromSource) _selfGridSpriteMapper.RemoveSpritePosition(_sprite, _grabCell);
                    _targetGridSpriteMapper.ChangeSpritePosition(_sprite, _grabCell, dropCell);
                }
                else if (removeFromSource)
                    // The tile is already removed inside the OnBeginDrag callback. Place the tile back.
                {
                    sourceTileMap.SetTile(_grabCell, ship.tile);
                }
            }

            Destroy(_grabbedShip);
        }

        private bool SpriteRepresentsShip(out Ship spriteShip)
        {
            if(_sprite == null) {
                Debug.Log("No Sprite");
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

        //新增：实现IPointerClickHandler接口，处理点击事件
        public void OnPointerClick(PointerEventData eventData)
        {
            Debug.Log("OnPointerClick");
            // 关键修改1：若当前正在拖拽（_grabbedShip存在），跳过点击逻辑
            if (_grabbedShip != null)
            {
                Debug.Log("Skipping click: Dragging in progress");
                return;
            }

            if (eventData.button != PointerEventData.InputButton.Left)
            {
                Debug.Log("Not Left Click");
                return;
            }

            // 关键修改2：使用局部变量存储点击状态，不覆盖类共享变量
            //获得点击单位的网格坐标
            var clickCell = GridUtils.ScreenToCell(eventData.position, sceneCamera, _grid, rules.areaSize);
            var anchorCell = clickCell; 
            Sprite clickedSprite = _selfGridSpriteMapper.GetSpriteAt(ref anchorCell); // 局部变量
            if (clickedSprite == null)
            {
                Debug.Log("No Sprite at " + clickCell);
                return;
            }

            if (!SpriteRepresentsShip(out var ship, clickedSprite)) // 传递局部Sprite
            {
                Debug.Log("No Ship associated with the sprite");
                return;
            }

            RotateShip(ship, anchorCell); // 传递局部锚点
        }

        // 关键修改3：重构SpriteRepresentsShip，支持传入特定Sprite
        private bool SpriteRepresentsShip(out Ship spriteShip, Sprite targetSprite)
        {
            if (targetSprite == null)
            {
                Debug.Log("No Sprite");
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
            // 记录原方向，用于回退
            var originalDirection = ship.CurrentDirection;
            
            // 执行逆时针旋转，此处修改了direction这个ship成员变量而已
            ship.RotateCounterclockwise();
            
            // 计算旋转后的占据单元格（此处修改的是part coordinates）
            var newCells = ship.GetOccupiedCells(anchorCell);
            
            // 校验是否合法（不越界、不重叠，先不管这个）
            if (true==false
                //!IsRotationValid(ship, newCells)
            )
            {
                Debug.Log("Invalid rotation.");
                ship.RotateCounterclockwise(); // 非法则回退方向
                return;
            }

            // 更新实际 Tilemap 中的棋子显示（传递锚点位置）
            UpdateShipSprite(ship, anchorCell); // 新增：传递锚点位置
            
            // 更新地图记录（保持原有逻辑）
            _selfGridSpriteMapper.ClearSpritePositions();
            _selfGridSpriteMapper.CacheSpritePositions();
        }

        // 关键修改：调整 UpdateShipSprite，作用于 Tilemap 中的实际棋子
        private void UpdateShipSprite(Ship ship, Vector3Int anchorCell)
        {
            // 1. 获取当前棋子在 Tilemap 中的变换矩阵（用于旋转）
            var tileTransform = sourceTileMap.GetTransformMatrix(anchorCell);
            
            // 2. 计算新的旋转角度（根据当前方向）
            float rotationAngle = (int)ship.CurrentDirection * 90f;
            tileTransform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, rotationAngle));
            
            // 3. 更新 Tilemap 中该位置的瓷砖变换
            sourceTileMap.SetTransformMatrix(anchorCell, tileTransform);

            // 4. 可选：若需要更新精灵（如不同方向使用不同 Sprite），可在此处设置
            var oldtile =ScriptableObject.CreateInstance<Tile>();
            for(int i = 0; i < ship.tiles.Count; i++){
                if(ship.tiles[i] == ship.tile){
                    oldtile = ship.tiles[i==0?3:i-1];
                    break;
                }
            }
            sourceTileMap.SwapTile(oldtile, ship.tile); // 根据实际需求调整
        }
    }
}