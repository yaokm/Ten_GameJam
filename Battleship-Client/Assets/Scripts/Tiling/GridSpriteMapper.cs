using System.Collections.Generic;
using System.Linq;
using BattleshipGame.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace BattleshipGame.Tiling
{
    public class GridSpriteMapper : MonoBehaviour
    {
        [SerializeField] private Rules rules;
        [SerializeField] private Tilemap tilemap;
        
        /// <summary>
        /// 存储 Sprite 的实例 ID 与对应的在 Tilemap 上的位置列表的映射关系。
        /// 键为 Sprite 的实例 ID，值为该 Sprite 在 Tilemap 上出现的所有位置列表。
        /// </summary>
        private readonly Dictionary<int, List<Vector3Int>> _spritePositionsOnTileMap =
            new Dictionary<int, List<Vector3Int>>();

        /// <summary>
        /// 存储 Sprite 的实例 ID 与对应的 Sprite 对象的映射关系。
        /// 键为 Sprite 的实例 ID，值为对应的 Sprite 对象。
        /// </summary>
        private readonly Dictionary<int, Sprite> _sprites = new Dictionary<int, Sprite>();

        private void Awake()
        {
            //CacheSpritePositions();
        }
        private void Start(){
            Debug.Log("Grid_Start");
            CacheSpritePositions();
        }
        public void CacheSpritePositions()
        {
            foreach (var position in tilemap.cellBounds.allPositionsWithin)
            {                
                var sprite = tilemap.GetSprite(position);
                if (!sprite)
                {
                    //Debug.Log("No sprite at " + position);
                    continue;
                }
                else{
                    Debug.Log("Sprite at " + position + " is " + sprite.name);
                }
                int spriteId = sprite.GetInstanceID();
                if (!_sprites.ContainsKey(spriteId))
                    _sprites.Add(spriteId, sprite);
                else
                    _sprites[spriteId] = sprite;

                if (!_spritePositionsOnTileMap.ContainsKey(spriteId))
                    _spritePositionsOnTileMap.Add(spriteId, new List<Vector3Int> {position});
                else
                    _spritePositionsOnTileMap[spriteId].Add(position);
            }
        }

        public void ClearSpritePositions()
        {
            _sprites.Clear();
            _spritePositionsOnTileMap.Clear();
        }

        public void ChangeSpritePosition(Sprite sprite, Vector3Int oldPosition, Vector3Int newPosition)
        {
            int spriteId = sprite.GetInstanceID();

            if (!_sprites.ContainsKey(spriteId))
                _sprites.Add(spriteId, sprite);

            if (_spritePositionsOnTileMap.ContainsKey(spriteId))
            {
                var oldPositionsList = _spritePositionsOnTileMap[spriteId];
                oldPositionsList.Remove(oldPosition);
                oldPositionsList.Add(newPosition);
            }
            else
            {
                _spritePositionsOnTileMap.Add(spriteId, new List<Vector3Int> {newPosition});
            }
        }

        public void RemoveSpritePosition(Sprite sprite, Vector3Int oldPosition)
        {            
            int spriteId = sprite.GetInstanceID();
            if (_spritePositionsOnTileMap.ContainsKey(spriteId))
                _spritePositionsOnTileMap[spriteId].Remove(oldPosition);
        }

        public Dictionary<int, List<Vector3Int>> GetSpritePositions()
        {
            return new Dictionary<int, List<Vector3Int>>(_spritePositionsOnTileMap);
        }

        /// <summary>
        /// 根据传入的位置引用，在地图中查找并返回对应位置的 Sprite 对象。
        /// 若该位置属于某个 Ship 的部件，则返回该 Ship 对应的 Sprite，
        /// 同时将传入的位置更新为该 Sprite 的基础位置。
        /// </summary>
        /// <param name="position">要查找的位置引用，查找成功后会更新为 Sprite 的基础位置。</param>
        /// <returns>若找到对应位置的 Sprite 则返回该 Sprite，未找到则返回 null。</returns>
        public Sprite GetSpriteAt(ref Vector3Int position)
        {
            foreach (var keyValuePair in _spritePositionsOnTileMap)
            {
                int spriteId = keyValuePair.Key;
                var spritePositions = keyValuePair.Value;
                if (!_sprites.TryGetValue(spriteId, out var sprite)) continue;
                Ship ship = null;
                foreach (var s in rules.ships.Where(s => s.tile.sprite.Equals(sprite))) ship = s;
                if (ship is null) continue;
                foreach (var spritePosition in spritePositions)
                foreach (var cell in ship.partCoordinates.Select(part => spritePosition + (Vector3Int) part))
                {
                    //Debug.Log("GetSpriteAt " + cell+" "+position);
                    if (!cell.Equals(position)) continue;
                    _sprites.TryGetValue(spriteId, out var result);
                    position = spritePosition;
                    
                    Debug.Log("GetSpriteAt " + position+" "+result.name);
                    return result;
                }
            }
            return null;
        }
    }
}