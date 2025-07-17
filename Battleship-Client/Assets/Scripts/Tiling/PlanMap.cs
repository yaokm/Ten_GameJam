using BattleshipGame.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BattleshipGame.Tiling
{
    public class PlanMap : Map
    {
        [SerializeField] private Tilemap fleetLayer;
        [SerializeField] private Tilemap gridsLayer;
        [SerializeField] private Tile gridTile;
        [SerializeField] private Rules rules;
        private IPlanMapMoveListener _listener;

        private void Start()
        {
            Debug.Log("PlanMap Start");
            GenerateGridLines();
        }
#if UNITY_EDITOR
        public void Print()
        {
            GenerateGridLines();
        }
#endif

        private void GenerateGridLines()
        {
            if (gridsLayer == null || gridTile == null) 
            {
                Debug.LogError("PlanMap gridsLayer or gridTile is null");
                return;
            }
            // 假设 PlanMap 也有 rules 字段，若没有请补充
            if (rules == null) 
            {
                Debug.LogError("PlanMap rules is null");
                return;
            }
            Debug.Log("PlanMap rules: " + rules.areaSize);
            gridsLayer.ClearAllTiles();
            for (int x = 0; x < rules.areaSize.x; x++)
            {
                for (int y = 0; y < rules.areaSize.y; y++)
                {
                    Vector3Int pos = new Vector3Int(x, y, 0);
                    gridsLayer.SetTile(pos, gridTile);
                }
            }
        }

        public override void SetShip(Ship ship, Vector3Int coordinate)
        {

            fleetLayer.SetTile(coordinate, ship.tile);
            // 1. 获取当前棋子在 Tilemap 中的变换矩阵（用于旋转）
            var tileTransform = fleetLayer.GetTransformMatrix(coordinate);
            // // 2. 计算新的旋转角度（根据当前方向）
            float rotationAngle = (int)ship.CurrentDirection * 90f;
            tileTransform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, rotationAngle));            
            // // 3. 更新 Tilemap 中该位置的瓷砖变换
            fleetLayer.SetTransformMatrix(coordinate, tileTransform);
            
        }

        public override bool MoveShip(Ship ship, Vector3Int from, Vector3Int to, bool isMovedIn)
        {
            return _listener.OnShipMoved(ship, from, to, isMovedIn);
        }

        public override void ClearAllShips()
        {
            fleetLayer.ClearAllTiles();
        }

        public void SetPlaceListener(IPlanMapMoveListener listener)
        {
            _listener = listener;
        }
    }
}