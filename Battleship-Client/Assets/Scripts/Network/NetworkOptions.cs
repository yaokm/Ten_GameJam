using UnityEngine;

namespace BattleshipGame.Network
{
    [CreateAssetMenu(fileName = "NetworkOptions", menuName = "Battleship/Network Options")]
    public class NetworkOptions : ScriptableObject
    {
        [SerializeField] private string localEndpoint = "ws://172.16.57.22:2567";
        [SerializeField] private string onlineEndpoint = "ws://172.16.57.22:2567";
        [SerializeField] private ServerType serverType = ServerType.Local;

        public string EndPoint => serverType == ServerType.Online ? onlineEndpoint : localEndpoint;

        private enum ServerType
        {
            Local,
            Online
        }
    }
}