﻿using UnityEngine;

namespace BattleshipGame.Network
{
    [CreateAssetMenu(fileName = "NetworkOptions", menuName = "Battleship/Network Options")]
    public class NetworkOptions : ScriptableObject
    {
        [SerializeField] private string localEndpoint = "ws://localhost:2567";
        [SerializeField] private string onlineEndpoint = "ws://192.168.137.1:2567";
        [SerializeField] private ServerType serverType = ServerType.Local;

        public string EndPoint => serverType == ServerType.Online ? onlineEndpoint : localEndpoint;

        private enum ServerType
        {
            Local,
            Online
        }
    }
}