using System;
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

        public void LeaveRoom()
        {
            _room?.Leave();
            _room = null;
        }

        public async void Connect(string endPoint, Action success, Action error)
        {
            if (_lobby != null && _lobby.Connection.IsOpen) return;
            _client = new Client(endPoint);
            try
            {
                _lobby = await _client.JoinOrCreate<LobbyState>(LobbyName);
                success?.Invoke();
                RegisterLobbyHandlers();
            }
            catch (Exception)
            {
                error?.Invoke();
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
                    new Dictionary<string, object> { { RoomOption.Name, name }, { RoomOption.Password, password } });
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
                Debug.Log("Received opponentInfo: " + (message != null ? "message not null" : "message is null"));
                
                try 
                {
                    // 检查消息是否包含必要的字段
                    if (!message.ContainsKey("directions") || !message.ContainsKey("basePositions"))
                    {
                        Debug.LogError("opponentInfo message missing required fields");
                        return;
                    }
                    
                    // 获取directions，确保它是一个数组
                    var directions = new int[0];
                    var directionsObj = message["directions"];
                    
                    if (directionsObj is object[] dirArray)
                    {
                        directions = dirArray.Select(d => Convert.ToInt32(d)).ToArray();
                    }
                    else if (directionsObj != null)
                    {
                        Debug.LogWarning($"directions is not an array, type: {directionsObj.GetType()}");
                        // 尝试转换单一值
                        directions = new int[] { Convert.ToInt32(directionsObj) };
                    }
                    
                    // 获取basePositions，确保它是一个二维数组
                    var basePositions = new int[0][];
                    var basePositionsObj = message["basePositions"];
                    
                    if (basePositionsObj is object[] posArray)
                    {
                        basePositions = new int[posArray.Length][];
                        for (int i = 0; i < posArray.Length; i++)
                        {
                            if (posArray[i] is object[] innerArray)
                            {
                                basePositions[i] = innerArray.Select(p => Convert.ToInt32(p)).ToArray();
                            }
                            else
                            {
                                basePositions[i] = new int[2]; // 默认值
                            }
                        }
                    }
                    
                    // 只有当两个数组都不为空时才触发事件
                    if (directions.Length > 0 && basePositions.Length > 0)
                    {
                        Debug.Log($"Parsed directions: {directions.Length}, basePositions: {basePositions.Length}");
                        OnOpponentInfoReceived?.Invoke(basePositions, directions);
                    }
                    else
                    {
                        Debug.LogWarning("Empty directions or basePositions arrays");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error parsing opponent info: {ex.Message}\n{ex.StackTrace}");
                }
            });
            void OnRoomStateChange(List<DataChange> changes)
            {
                foreach (var change in changes.Where(change => change.Field == RoomState.Phase))
                    GamePhaseChanged?.Invoke((string)change.Value);
            }

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
    }
}