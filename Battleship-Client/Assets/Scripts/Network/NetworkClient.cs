﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Colyseus;
using UnityEngine;
using DataChange = Colyseus.Schema.DataChange;

namespace BattleshipGame.Network
{
    public class NetworkClient : IClient
    {
        // 添加事件，用于通知BattleManager敌方船只信息已就绪
        public event Action<int[][], int[]> OnOpponentInfoReceived;
        private const string RoomName = "game";
        private const string LobbyName = "lobby";
        private readonly Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
        private Client _client;
        private Room<LobbyState> _lobby;
        private Room<State> _room;
        public event Action<string> GamePhaseChanged;

        public State GetRoomState()
        {
            return _room?.State;
        }

        public string GetSessionId()
        {
            return _room?.SessionId;
        }

        public void SendPlacement(int[] placement, int[] directions = null, int[][] basePositions = null)
        {
            var message = new Dictionary<string, object>();
            message.Add("placement", placement);
            message.Add("directions", directions);
            message.Add("basePositions", basePositions);
            _room.Send(RoomMessage.Place, message);
        }
        public void SendGetOpponentInfoRequest()
        {
            _room.Send(RoomMessage.OpponentInfoRequest);
        }
        public void SendTurn(int[] targetIndexes)
        {
            _room.Send(RoomMessage.Turn, targetIndexes);
        }
        public void SendBasePositions(int[][] basePositions)
        {
            _room.Send(RoomMessage.BasePosition, basePositions);
        }
        public void SendDirection(int[] direction)
        {
            _room.Send(RoomMessage.Direction, direction);
        }
        public void SendRematch(bool isRematching)
        {
            _room.Send(RoomMessage.Rematch, isRematching);
        }
        public void SendUseSkill(int skillType, object param = null)
        {
            var msg = new Dictionary<string, object> { { "skillType", skillType } };
            if (param != null) msg.Add("params", param);
            _room.Send(RoomMessage.UseSkill, msg);
        }

        public void LeaveRoom()
        {
            _room?.Leave();
            _room = null;
        }

        public async void Connect(string endPoint, Action success, Action<Exception> error)
        {
            if (_lobby != null && _lobby.Connection.IsOpen) return;
            _client = new Client(endPoint);
            try
            {
                _lobby = await _client.JoinOrCreate<LobbyState>(LobbyName);
                success?.Invoke();
                RegisterLobbyHandlers();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkClient] Connect Exception: {ex}");
                error?.Invoke(ex);
            }
        }

        public event Action<Dictionary<string, Room>> RoomsChanged;

        private void RegisterLobbyHandlers()
        {
            _lobby.OnMessage<Room[]>(LobbyMessage.Rooms, message =>
            {
                foreach (var room in message)
                    if (!_rooms.ContainsKey(room.roomId))
                        _rooms.Add(room.roomId, room);

                RoomsChanged?.Invoke(_rooms);
            });

            _lobby.OnMessage<object[]>(LobbyMessage.Add,
                message => { _lobby.Send(LobbyMessage.RoomInfo, message[0]); });

            _lobby.OnMessage<Room>(LobbyMessage.RoomInfo, room =>
            {
                if (room == null)
                    _rooms.Clear();
                else if (_rooms.ContainsKey(room.roomId))
                    _rooms[room.roomId] = room;
                else
                    _rooms.Add(room.roomId, room);

                RoomsChanged?.Invoke(_rooms);
            });

            _lobby.OnMessage<string>(LobbyMessage.Remove, roomId =>
            {
                if (!_rooms.ContainsKey(roomId)) return;
                _rooms.Remove(roomId);
                RoomsChanged?.Invoke(_rooms);
            });
        }

        public async void CreateRoom(string name, string password, Action success = null, Action<string> error = null)
        {
            try
            {
                _room = await _client.Create<State>(RoomName,
                    new Dictionary<string, object> { { RoomOption.Name, name }, { RoomOption.Password, password }, { "aiMode", false } });
                RegisterRoomHandlers();
                success?.Invoke();
            }
            catch (Exception exception)
            {
                error?.Invoke(exception.Message);
            }
        }

        public async void JoinRoom(string roomId, string password, Action<string> error = null)
        {
            try
            {
                _room = await _client.JoinById<State>(roomId, new Dictionary<string, object> { { RoomOption.Password, password } });
                RegisterRoomHandlers();
            }
            catch (Exception exception)
            {
                error?.Invoke(exception.Message);
            }
        }

        private void RegisterRoomHandlers()
        {
            _room.State.OnChange += OnRoomStateChange;
            // 注册接收敌方船只信息的消息处理器
            _room.OnMessage<Dictionary<string, object>>("opponentInfo", message =>
            {
                Debug.Log("opponentInfo:" + message);
                Debug.Log("message.Length:" + message.Count);
                foreach (var key in message.Keys)
                {
                    Debug.Log("key:" + key + ":" + message[key]);
                }
                try
                {

                    // 解析方向数组
                    var directionsObj = message["directions"] as List<object>;
                    if (directionsObj == null)
                    {
                        Debug.Log("directionsObj is null");
                        return;
                    }
                    else
                    {
                        Debug.Log("directionsObj is not null");
                    }
                    if (directionsObj is List<object>)
                    {
                        directionsObj.ForEach(d => Debug.Log("directionObj:" + d));
                        Debug.Log("directionsObj is List<object>");
                    }
                    else if (directionsObj is object[])
                    {
                        Debug.Log("directionsObj is object[]");
                    }
                    else
                    {
                        Debug.Log("directionsObj is not List<object> or object[]");
                    }
                    var directions = directionsObj?.Select(d => Convert.ToInt32(d)).ToArray() ?? new int[0];
                    for (int i = 0; i < directions.Length; i++)
                    {
                        Debug.Log("direction:" + i + ":" + directions[i]);
                    }
                    if (directions.Length == 0)
                    {
                        Debug.Log("directions is empty");
                        return;
                    }
                    else
                    {
                        Debug.Log("directions is not empty");
                    }

                    // 解析基点坐标数组（二维数组）
                    var basePositionsObj = message["basePositions"] as List<object>;
                    var basePositions = new int[7][];

                    for (int i = 0; i < basePositions.Length; i++)
                    {
                        var posObj = basePositionsObj[i] as List<object>;
                        basePositions[i] = posObj?.Select(p => Convert.ToInt32(p)).ToArray() ?? new int[2];
                    }

                    // 触发事件，通知BattleManager
                    OnOpponentInfoReceived?.Invoke(basePositions, directions);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing opponent info: {ex.Message}");
                }
            });
            void OnRoomStateChange(List<DataChange> changes)
            {
                foreach (var change in changes.Where(change => change.Field == RoomState.Phase))
                    GamePhaseChanged?.Invoke((string)change.Value);
            }
            // 修改事件注册
            _room.OnMessage<Dictionary<string, object>>("xBombHit", message =>
            {
                string player = message["player"] as string;
                string victim = message["victim"] as string;
                OnXBombHit?.Invoke(player, victim);
            });

            _room.OnMessage<Dictionary<string, object>>("skipTurn", message =>
            {
                string player = message["player"] as string;
                OnTurnSkipped?.Invoke(player);
            });

            // 注册技能广播消息
            _room.OnMessage<Dictionary<string, object>>(RoomMessage.SkillUsed, message =>
            {
                int skillType = Convert.ToInt32(message["skillType"]);
                string player = message["player"] as string;
                object param = message.ContainsKey("params") ? message["params"] : null;
                Debug.Log("param type: " + param.GetType());
                if (param is Dictionary<string, object>)
                {
                    Debug.Log("param is Dictionary<string, object>");
                }
                else if (param is Hashtable)
                {
                    Debug.Log("param is Hashtable");
                }
                else if (param is string)
                {
                    Debug.Log("param is string");
                }
                else if (param is List<object>)
                {
                    Debug.Log("param is List<object>");
                }
                else
                {
                    Debug.Log("param is " + param.GetType());
                }
                OnSkillUsed?.Invoke(player, skillType, param);
            });
        }

        public void LeaveLobby()
        {
            _lobby?.Leave();
            _lobby = null;
        }

        public bool IsRoomPasswordProtected(string roomId)
        {
            return _rooms.TryGetValue(roomId, out var room) && room.metadata.requiresPassword;
        }

        public bool IsRoomFull(string roomId)
        {
            return _rooms.TryGetValue(roomId, out var room) && room.maxClients <= room.clients;
        }

        public void RefreshRooms()
        {
            RoomsChanged?.Invoke(_rooms);
        }

        public async void GetOpponentShipData(Action<int[], int[], int[]> callback)
        {
            _room.Send("getOpponentShipData");
            _room.OnMessage<object[]>("opponentShipData", message =>
            {
                callback?.Invoke(
                    (int[])message[0], // directions
                    (int[])message[1], // coordinates 
                    (int[])message[2]  // rankOrders
                );
            });
        }

        // 添加事件定义
        public event Action<string, string> OnXBombHit; // 参数：击中炸弹的玩家ID，被炸弹影响的玩家ID
        public event Action<string> OnTurnSkipped; // 参数：被跳过回合的玩家ID
        public event Action<string, int, object> OnSkillUsed; // 参数：玩家ID，技能类型，附加参数


    }
}