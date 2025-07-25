﻿using System;
using System.Collections;
using BattleshipGame.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Tilemaps;
using static BattleshipGame.Core.GridUtils;

namespace BattleshipGame.Tiling
{
    public class BattleMap : Map, IPointerClickHandler, IBeginDragHandler, IEndDragHandler
    {
        private const int FlashGridCount = 2;
        private const int GridSize = 10;
        private const int ShotsSize = GridSize * GridSize;
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Rules rules;
        [SerializeField] private ScreenType screenType;

        // @formatter:off
        [Header("Layers")]
        [SerializeField] private Tilemap cursorLayer;
        [SerializeField] private Tilemap markerLayer;
        [SerializeField] private Tilemap fleetLayer;
        [SerializeField] private Tilemap gridsLayer;
        [Space] 
        [Header("Cursors")] 
        [SerializeField] private Tile activeCursor;
        [SerializeField] private Tile inactiveCursor;
        [Space] 
        [Header("Markers")] 
        [SerializeField] private Tile hitMarker;
        [SerializeField] private Tile missedMarker;
        [SerializeField] private Tile markedTargetMarker;
        [SerializeField] private Tile shotTargetMarker;
        [SerializeField] private Tile shotFleetMarker;
        [Space] 
        // @formatter:on
        private readonly WaitForSecondsRealtime _flashGridInterval = new WaitForSecondsRealtime(0.3f);

        private IBattleMapClickListener _clickListener;
        private Grid _grid;
        private bool _isDragging;
        private bool _isFlashingGrids;
        public bool IsMarkingTargets { get; set; }

        [SerializeField] private Tile gridTile; // 拖入你想用的网格线Tile

        private void GenerateGridLines()
        {
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

        private void Start()
        {
            if (sceneCamera == null) sceneCamera = Camera.main;
            _grid = GetComponent<Grid>();
            GenerateGridLines(); // 动态生成网格线
        }
#if UNITY_EDITOR
        public void Print()
        {
            GenerateGridLines();
        }
#endif
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isDragging && screenType == ScreenType.Opponent)
                _clickListener.OnOpponentMapClicked(
                    ScreenToCell(eventData.position, sceneCamera, _grid, rules.areaSize));
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _isDragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

#if UNITY_WEBGL || UNITY_STANDALONE || UNITY_EDITOR
        private void OnMouseOver()
        {
            if (screenType != ScreenType.Opponent) return;
            cursorLayer.ClearAllTiles();
            var coordinate = ScreenToCell(Input.mousePosition, sceneCamera, _grid, rules.areaSize);
            if (markerLayer.HasTile(coordinate))
            {
                var tile = markerLayer.GetTile(coordinate);
                if (tile && tile.name.Equals(markedTargetMarker.name))
                    cursorLayer.SetTile(coordinate, inactiveCursor);
            }
            else if (IsMarkingTargets)
            {
                cursorLayer.SetTile(coordinate, activeCursor);
            }
        }

        private void OnMouseExit()
        {
            if (screenType != ScreenType.Opponent) return;
            cursorLayer.ClearAllTiles();
        }
#endif

        public override void SetShip(Ship ship, Vector3Int coordinate)
        {
            var tile=ship.tile;
            
            var dir=(int)ship.CurrentDirection;
            if (screenType == ScreenType.Opponent)
            {
                dir =(int) ship._enemyDirection;
                tile=ship.Tiles[dir];
            }
            fleetLayer.SetTile(coordinate, tile);
            // 1. 获取当前棋子在 Tilemap 中的变换矩阵（用于旋转）
            var tileTransform = fleetLayer.GetTransformMatrix(coordinate);
            
            // // 2. 计算新的旋转角度（根据当前方向）
            float rotationAngle = dir * 90f;
            tileTransform = Matrix4x4.Rotate(Quaternion.Euler(0, 0, rotationAngle));
            
            // // 3. 更新 Tilemap 中该位置的瓷砖变换
             fleetLayer.SetTransformMatrix(coordinate, tileTransform);
        }

        public override bool MoveShip(Ship ship, Vector3Int from, Vector3Int to, bool isMovedIn,bool isRotation=false)
        {
            SetShip(ship, to);
            return true;
        }

        public override void ClearAllShips()
        {
            fleetLayer.ClearAllTiles();
        }

        public void SetClickListener(IBattleMapClickListener clickListener)
        {
            _clickListener = clickListener;
        }

        public void ClearMarker(Vector3Int coordinate)
        {
            if (markerLayer.HasTile(coordinate)) markerLayer.SetTile(coordinate, null);
        }

        public bool SetMarker(int index, Marker marker)
        {
            Tile markerTile;
            switch (marker)
            {
                case Marker.Missed:
                    markerTile = missedMarker;
                    break;
                case Marker.Hit:
                    markerTile = hitMarker;
                    break;
                case Marker.MarkedTarget:
                    markerTile = markedTargetMarker;
                    break;
                case Marker.ShotTarget:
                    markerTile = shotTargetMarker;
                    break;
                case Marker.ShotFleet:
                    markerTile = shotFleetMarker;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(marker), marker, null);
            }

            var coordinate = CellIndexToCoordinate(index, rules.areaSize.x);
            var tile = markerLayer.GetTile(coordinate);
            if (tile && !(markerTile is null) && markerTile.name.Equals(markedTargetMarker.name))
                return false;
            markerLayer.SetTile(coordinate, markerTile);
            return true;
        }

        public void FlashGrids()
        {
            if (!_isFlashingGrids) StartCoroutine(FlashGridsCoroutine());
        }

        private IEnumerator FlashGridsCoroutine()
        {
            _isFlashingGrids = true;
            var colorCache = gridsLayer.color;
            var flashGridColor = new Color(0.9f, 0.5f, 0.5f);
            for (var i = 0; i < FlashGridCount; i++)
            {
                yield return _flashGridInterval;
                gridsLayer.color = flashGridColor;
                yield return _flashGridInterval;
                // ReSharper disable once Unity.InefficientPropertyAccess
                gridsLayer.color = colorCache;
            }

            _isFlashingGrids = false;
        }

        public enum ScreenType
        {
            User,
            Opponent
        }
    }
}