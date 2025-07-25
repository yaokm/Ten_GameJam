using UnityEngine;

namespace BattleshipGame.Network
{
    [CreateAssetMenu(fileName = "NetworkOptions", menuName = "Battleship/Network Options")]
    public class NetworkOptions : ScriptableObject
    {
        [SerializeField] private string localEndpoint = "ws://192.168.69.23:2567";
        [SerializeField] private string onlineEndpoint = "ws://";
        [SerializeField] private ServerType serverType = ServerType.Local;

        public string EndPoint => serverType == ServerType.Online ? onlineEndpoint : localEndpoint;

        private enum ServerType
        {
            Local,
            Online
        }
    }
}