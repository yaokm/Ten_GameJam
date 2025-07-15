using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Core;
using UnityEngine;

namespace BattleshipGame.Tiling
{
    public class OpponentStatus : MonoBehaviour
    {
        public const int NotShot = -1;
        [SerializeField] private GameObject maskPrefab;
        [SerializeField] private Rules rules;
        private readonly List<(int ShotTurn, Vector3Int Coordinate)> _shipParts = new List<(int, Vector3Int)>();

        private void Start()
        {
            var spritePositions = GetComponent<GridSpriteMapper>().GetSpritePositions();
            foreach (var ship in rules.ships)
                for (var i = 0; i < ship.amount; i++)
                    foreach (var partCoordinate in ship.partCoordinates.Select(coordinate =>
                        spritePositions[ship.tile.sprite.GetInstanceID()][i] + (Vector3Int)coordinate))
                    {
                        Debug.Log("Part coordinate: " + partCoordinate);
                        _shipParts.Add((NotShot, partCoordinate));
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


        //changedShipPart: 0-25,shotTurn:击中回合
        public void DisplayShotEnemyShipParts(int changedShipPart, int shotTurn)
        {
            Debug.Log("DisplayShotEnemyShipParts: " + changedShipPart + " " + shotTurn);
            var part = _shipParts[changedShipPart];
            part.ShotTurn = shotTurn;
            _shipParts[changedShipPart] = part;
            var maskPositionInWorldSpace = transform.position + part.Coordinate + new Vector3(0.5f, 0.5f);
            Debug.Log("Mask position: " + maskPositionInWorldSpace);
            
            if (true == true)
            {
                Instantiate(maskPrefab, maskPositionInWorldSpace, Quaternion.identity);
            }
            else
            {
                
            }
        }
    }
}
