using System;

namespace BattleshipGame.Network
{
    public interface IClient
    {
        event Action<string> GamePhaseChanged;
        State GetRoomState();
        string GetSessionId();
        void Connect(string endPoint = null, Action success = null, Action error = null);
        void SendPlacement(int[] placement,int[] direction=null,int[][] basePositions=null);

        void SendTurn(int[] targetIndexes);
        void SendRematch(bool isRematching);
        void SendGetOpponentInfoRequest();
        void LeaveRoom();
        public void GetOpponentShipData(Action<int[], int[], int[]> callback);
        void SendUseSkill(int skillType, object param = null);
    }
}