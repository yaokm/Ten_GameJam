using UnityEngine;

namespace BattleshipGame.Core
{
    public static class GridUtils
    {
        /// <summary>
        /// 表示超出地图范围的索引值。
        /// </summary>
        public const int OutOfMap = -1;

        /// <summary>
        /// 将三维整数坐标转换为单元格索引。
        /// </summary>
        /// <param name="coordinate">要转换的三维整数坐标。</param>
        /// <param name="areaSize">地图区域的二维整数尺寸。</param>
        /// <returns>若坐标在地图范围内，返回对应的单元格索引；否则返回 <see cref="OutOfMap"/>。</returns>
        public static int CoordinateToCellIndex(Vector3Int coordinate, Vector2Int areaSize)
        {
            int cellIndex = coordinate.y * areaSize.x + coordinate.x;
            if (cellIndex >= 0 && cellIndex < areaSize.x * areaSize.y) return cellIndex;
            return OutOfMap;
        }

        /// <summary>
        /// 将单元格索引转换为三维整数坐标。
        /// </summary>
        /// <param name="cellIndex">要转换的单元格索引。</param>
        /// <param name="width">地图的宽度。</param>
        /// <returns>转换后的三维整数坐标，Z 轴值为 0。</returns>
        public static Vector3Int CellIndexToCoordinate(int cellIndex, int width)
        {
            return new Vector3Int(cellIndex % width, cellIndex / width, 0);
        }
        
        /// <summary>
        /// 将屏幕坐标转换为网格单元格坐标。
        /// </summary>
        /// <param name="input">输入的屏幕坐标。</param>
        /// <param name="sceneCamera">场景相机。</param>
        /// <param name="grid">网格对象。</param>
        /// <param name="areaSize">地图区域的二维整数尺寸。</param>
        /// <returns>转换后的网格单元格坐标。</returns>
        public static Vector3Int ScreenToCell(Vector3 input, Camera sceneCamera, Grid grid, Vector2Int areaSize)
        {
            var worldPoint = sceneCamera.ScreenToWorldPoint(input);
            return WorldToCell(worldPoint, sceneCamera, grid, areaSize);
        }

        /// <summary>
        /// 将世界坐标转换为网格单元格坐标，并将坐标限制在地图范围内。
        /// </summary>
        /// <param name="worldPoint">输入的世界坐标。</param>
        /// <param name="sceneCamera">场景相机。</param>
        /// <param name="grid">网格对象。</param>
        /// <param name="areaSize">地图区域的二维整数尺寸。</param>
        /// <returns>转换并限制在范围内的网格单元格坐标。</returns>
        public static Vector3Int WorldToCell(Vector3 worldPoint, Camera sceneCamera, Grid grid, Vector2Int areaSize)
        {
            var cell = grid.WorldToCell(worldPoint);
            cell.Clamp(new Vector3Int(0, 0, 0), new Vector3Int(areaSize.x - 1, areaSize.y - 1, 0));
            return cell;
        }

        /// <summary>
        /// 检查给定尺寸的船只以指定枢轴点放置时是否完全在地图边界内。
        /// </summary>
        /// <param name="shipWidth">船只的宽度。</param>
        /// <param name="shipHeight">船只的高度。</param>
        /// <param name="pivot">船只的枢轴点坐标。</param>
        /// <param name="areaSize">地图区域的二维整数尺寸。</param>
        /// <returns>若船只完全在地图边界内返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public static bool IsInsideBoundaries(int shipWidth, int shipHeight, Vector3Int pivot, Vector2Int areaSize,Direction direction=Direction.Right)
        {
            bool removed=direction switch{
                Direction.Right=>pivot.x + shipWidth <= areaSize.x&&pivot.y - (shipHeight - 1) >= 0,//左上
                Direction.Left=>pivot.x - (shipWidth-1) >= 0&&pivot.y+shipHeight <= areaSize.y,//右下
                Direction.Up=>pivot.x + shipWidth <= areaSize.x&&pivot.y+shipHeight <= areaSize.y,//左下
                Direction.Down=>pivot.x - (shipWidth-1) >= 0&&pivot.y - (shipHeight - 1) >= 0,//右上
                _=>true
            };

            return pivot.x >= 0&&pivot.y >= 0&&pivot.x < areaSize.x && pivot.y < areaSize.y && removed;
        }
    }
}