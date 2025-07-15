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