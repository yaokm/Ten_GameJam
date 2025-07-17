using System.Collections.Generic;
using BattleshipGame.UI;
using UnityEngine;
using UnityEngine.Tilemaps;
using BattleshipGame.Core;
namespace BattleshipGame.Managers
{
    public class PoolManager : MonoBehaviour
    {
        [SerializeField] private ButtonController clearButton;
        [SerializeField] private ButtonController randomButton;
        [SerializeField] private Tilemap tilemap;
        [SerializeField] private Rules rules;
        private readonly Dictionary<Vector3Int, TileBase> _cache = new Dictionary<Vector3Int, TileBase>();

        private void Start()
        {
            //ResetThePool();
            // for(int i=0;i<rules.ships.Count;i++){
            // }

            clearButton.AddListener(ResetThePool);
            randomButton.AddListener(ClearThePool);
            foreach (var coordinate in tilemap.cellBounds.allPositionsWithin)
            {
                if (!tilemap.HasTile(coordinate)) continue;
                if (_cache.ContainsKey(coordinate))
                    _cache[coordinate] = tilemap.GetTile(coordinate);
                else
                    _cache.Add(coordinate, tilemap.GetTile(coordinate));
            }
        }

        private void ClearThePool()
        {
            tilemap.ClearAllTiles();
        }

        private void ResetThePool()
        {
            ClearThePool();
            foreach (var kvp in _cache) tilemap.SetTile(kvp.Key, kvp.Value);
        }
    }
}