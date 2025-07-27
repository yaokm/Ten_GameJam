using System;
using BattleshipGame.AI;
using BattleshipGame.Core;
using BattleshipGame.Network;
using UnityEngine;
using static BattleshipGame.Core.StatusData.Status;

namespace BattleshipGame.Managers
{
    public class GameManager : Singleton<GameManager>
    {
        [SerializeField] private NetworkOptions networkOptions;
        [SerializeField] private StatusData statusData;
        public IClient Client { get; private set; }
        public int SelectedHeroId { get; set; } = 1;

        protected override void Awake()
        {
            base.Awake();
            statusData.State = GameStart;
        }

        private void OnApplicationQuit()
        {
            FinishNetworkClient();
        }

        public void ConnectToServer(Action onSuccess, Action onError)
        {
            switch (Client)
            {
                case NetworkClient _:
                    GameSceneManager.Instance.GoToLobby();
                    return;
                case LocalClient _:
                    gameObject.GetComponent<LocalClient>().enabled = false;
                    break;
            }

            Client = new NetworkClient();
            Client.GamePhaseChanged += phase =>
            {
                if (phase != RoomPhase.Place) return;
                GameSceneManager.Instance.GoToPlanScene();
            };
            var networkClient = (NetworkClient) Client;
            statusData.State = Connecting;
            
            // 添加调试信息
            Debug.Log($"尝试连接到服务器: {networkOptions.EndPoint}");
            
            networkClient.Connect(networkOptions.EndPoint,
                () =>
                {
                    if (Client is NetworkClient)
                    {
                        Debug.Log("连接成功");
                        onSuccess?.Invoke();
                    }
                },
                (ex) =>
                {
                    if (Client is NetworkClient)
                    {
                        Debug.LogError($"连接失败: {ex}");
                        onError?.Invoke();
                        Client = null;
                    }
                });
        }

        public void StartLocalClient()
        {
            FinishNetworkClient();
            var localClient = GetComponent<LocalClient>();
            localClient.enabled = true;
            Client = localClient;
            Client.GamePhaseChanged += phase =>
            {
                if (phase != RoomPhase.Place) return;
                GameSceneManager.Instance.GoToPlanScene();
            };
            Client.Connect();
        }

        public void FinishNetworkClient()
        {
            if (Client is NetworkClient networkClient)
            {
                networkClient.LeaveRoom();
                networkClient.LeaveLobby();
                Client = null;
            }
        }
    }
}