using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Core;
using UnityEngine;
using UnityEngine.Tilemaps;
using static BattleshipGame.Tiling.BattleMap;
namespace BattleshipGame.Tiling
{
    public class OpponentStatus : MonoBehaviour
    {
        public const int NotShot = -1;
        [SerializeField] private GameObject maskPrefab;
        [SerializeField] private Rules rules;
        private readonly List<(int ShotTurn, Vector3Int Coordinate)> _shipParts = new List<(int, Vector3Int)>();
        [SerializeField] private Tilemap OpponentMap;
        [SerializeField] private ScreenType screenType=ScreenType.Opponent;
        private void Start()
        {
            var spritePositions = GetComponent<GridSpriteMapper>().GetSpritePositions();
            foreach (var sprite in spritePositions.Keys)
                Debug.Log("Sprite: " + sprite + "spritePosition" + spritePositions[sprite][0]);
            foreach (var ship in rules.ships)
                for (var i = 0; i < ship.amount; i++)
                {
                    Debug.Log("Ship: " + ship.tile.sprite.GetInstanceID() + " " + i);
                    foreach (var partCoordinate in ship.PartCoordinates.Select(coordinate =>
                        spritePositions[ship.tile.sprite.GetInstanceID()][i] + (Vector3Int)coordinate))
                    {
                        Debug.Log("Part coordinate: " + partCoordinate);
                        _shipParts.Add((NotShot, partCoordinate));
                    }
                }
            // 初始化为24个零向量
            for (int i = 0; i < 24; i++)
            {
                ShipPartPos.Add(Vector3.zero);
            }


            // 新增：初始化完成后打印所有敌方船只位置
            PrintAllEnemyShipPositions();
        }

        // 新增：打印所有敌方船只部件的坐标和被击中状态
        private void PrintAllEnemyShipPositions()
        {
            if (_shipParts.Count == 0)
            {
                Debug.Log("No enemy ship parts found.");
                return;
            }

            Debug.Log("=== Enemy Ship Positions ===");
            foreach (var part in _shipParts)
            {
                string status = part.ShotTurn == NotShot ? "未被击中" : $"被第 {part.ShotTurn} 回合击中";
                Debug.Log($"坐标: {part.Coordinate} | 状态: {status}");
            }
            Debug.Log("===========================");
        }

        // // 可选：通过按键（如F1）触发打印（添加到Update方法中）
        // private void Update()
        // {
        //     if (Input.GetKeyDown(KeyCode.F1))
        //     {
        //         PrintAllEnemyShipPositions();
        //     }
        // }

        // 原有方法保持不变
        public int GetShotTurn(Vector3Int coordinate)
        {
            foreach ((int shotTurn, var vector3Int) in _shipParts)
                if (vector3Int.Equals(coordinate))
                    return shotTurn;

            return NotShot;
        }

        public List<Vector3Int> GetCoordinates(int turn)
        {
            var result = new List<Vector3Int>();
            foreach ((int shotTurn, var vector3Int) in _shipParts)
                if (shotTurn == turn)
                    result.Add(vector3Int);
            return result;
        }
        public bool isAllShipPartShot(int rankOrder)
        {
            switch (rankOrder)
            {
                case 0://F0
                    return isShipPartShot(0, 5);
                case 1://E0
                    return isShipPartShot(6, 10);
                case 2://D0
                    return isShipPartShot(11, 14);
                case 3://C0
                    return isShipPartShot(15, 17);
                case 4://B0
                    return isShipPartShot(18, 19);
                // case 5://A0
                //     return isShipPartShot(20, 20);
                case 5://D1
                    return isShipPartShot(20, 23);
                default:
                    return false;
            }
        }
        public int getShipRankOrder(int shipPart)
        {
            switch (shipPart)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                    return 0;//F0
                case 6:
                case 7:
                case 8:
                case 9:
                case 10:
                    return 1;//E0
                case 11:
                case 12:
                case 13:
                case 14:
                    return 2;//D0
                case 15:
                case 16:
                case 17:
                    return 3;//C0
                case 18:
                case 19:
                    return 4;//B0   
                // case 20:
                //     return 5;//A0  // A0船已移除
                case 20:
                case 21:
                case 22:
                case 23:
                    return 5;//D1
                default:
                    return -1;
            }
        }
        private bool isShipPartShot(int beginIndex, int endIndex)
        {
            for (int i = beginIndex; i <= endIndex; i++)
            {
                if (_shipParts[i].ShotTurn == NotShot)
                {
                    return false;
                }
            }
            return true;
        }
        public List<Vector3> ShipPartPos = new List<Vector3>();

        private void Awake()
        {

        }
        private void ShipMask(int rankOrder)
        {
            for (int i = 0; i < ShipPartPos.Count; i++)
            {
                if (getShipRankOrder(i) == rankOrder)
                {
                    Instantiate(maskPrefab, ShipPartPos[i], Quaternion.identity);
                }
            }
        }
        void SetShip(Ship ship, Vector3Int coordinate)

        {
            var tile = ship.tile;

            var dir = (int)ship.CurrentDirection;
            if (screenType == ScreenType.Opponent)
            {
                dir = (int)ship._enemyDirection;
                tile = ship.Tiles[dir];
            }
            OpponentMap.SetTile(coordinate, tile);
            // 1. 获取当前棋子在 Tilemap 中的变换矩阵（用于旋转）
            var tileTransform = OpponentMap.GetTransformMatrix(coordinate);

            // // 2. 计算新的旋转角度（根据当前方向）
            float rotationAngle = dir * 90f;
            tileTransform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, rotationAngle));

            // // 3. 更新 Tilemap 中该位置的瓷砖变换
            OpponentMap.SetTransformMatrix(coordinate, tileTransform);
        }
        //changedShipPart: 0-24,shotTurn:击中回合
        public void DisplayShotEnemyShipParts(int changedShipPart, int shotTurn)
        {
            Debug.Log("ShipPartPos_size: " + ShipPartPos.Count);
            Debug.Log("DisplayShotEnemyShipParts: " + changedShipPart + " " + shotTurn);

            // 添加边界检查
            if (changedShipPart < 0 || changedShipPart >= ShipPartPos.Count || changedShipPart >= _shipParts.Count)
            {
                Debug.LogError($"Invalid shipPart index: {changedShipPart}. Valid range is 0-{ShipPartPos.Count - 1}");
                return;
            }

            var part = _shipParts[changedShipPart];
            part.ShotTurn = shotTurn;
            _shipParts[changedShipPart] = part;
            var maskPositionInWorldSpace = transform.position + part.Coordinate + new Vector3(0.5f, 0.5f);
            Debug.Log("Mask position: " + maskPositionInWorldSpace);
            
            ShipPartPos[changedShipPart] = maskPositionInWorldSpace;
            int rankOrder = getShipRankOrder(changedShipPart);
            Debug.Log("rankOrder: " + rankOrder.ToString());
            if (isAllShipPartShot(rankOrder))
            {//一艘船全被击中
                ShipMask(rankOrder);
                foreach (var ship in rules.ships)
                {
                    
                    if (ship.rankOrder == rankOrder)
                    {
                        Debug.Log("ship:" + ship + "EnemyCoordinate:" + ship.EnemyCoordinate + "_enemyDirection:" + ship._enemyDirection);
                        SetShip(ship, new Vector3Int(ship.EnemyCoordinate.x, ship.EnemyCoordinate.y, 0));
                        break;
                    }
                }
            }
        }
    }

}