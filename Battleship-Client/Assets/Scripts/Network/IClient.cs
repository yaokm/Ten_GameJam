using System;

namespace BattleshipGame.Network
{
    public interface IClient
    {
        event Action<string> GamePhaseChanged;
        event Action<int[][], int[], int> OnOpponentInfoReceived;
        State GetRoomState();
        string GetSessionId();
        void Connect(string endPoint = null, Action success = null, Action<Exception> error = null);
        void SendPlacement(int[] placement, int[] directions = null, int[][] basePositions = null, int heroType = 1);

        void SendTurn(int[] targetIndexes);
        void SendRematch(bool isRematching);
        void SendGetOpponentInfoRequest();
        void LeaveRoom();
        public void GetOpponentShipData(Action<int[], int[], int[]> callback);
        void SendUseSkill(int skillType, object param = null);
    }
}