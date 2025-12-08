using System;
using Input;
using Physics;
using Unity.Netcode;
using UnityEngine;

namespace Networking
{
    public class PlayerNetworkState : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerNetworkInput playerNetworkInput;
        [SerializeField] private PlayerPhysicsCalculator physicsCalculator;
        
        
        private const int BufferSize = 256;

        public readonly CircularBuffer<MovementPayload> LocalMovementBuffer = new(BufferSize);
        public readonly CircularBuffer<MovementPayload> ServerMovementBuffer = new(BufferSize);

        public event Action<MovementPayload> OnServerStateReceivedClient;

        public override void OnNetworkSpawn()
        {
            MovementPayload defaultPayload = physicsCalculator.GenerateDefaultPayload();
            LocalMovementBuffer.Fill(defaultPayload);
            ServerMovementBuffer.Fill(defaultPayload);
        }

        private int _ownerTick = -1;
        private int _serverTick = -1;
        private void FixedUpdate()
        {
            if (IsOwner)
            {
                if (_ownerTick != playerNetworkInput.LastPredictionTick)
                {
                    _ownerTick = playerNetworkInput.LastPredictionTick;
                    OnTickClient();
                }
            }

            if (IsServer)
            {
                if (_serverTick != NetworkManager.ServerTime.Tick)
                {
                    _serverTick = NetworkManager.ServerTime.Tick;
                    OnTickServer();
                }
            }
        }

        /// <summary>
        /// Enregistre l'état du mouvement local du client dans le buffer à chaque tick
        /// </summary>
        private void OnTickClient()
        {
            MovementPayload payload = physicsCalculator.CurrentMovementPayload;
            payload.Tick = playerNetworkInput.LastPredictionTick;
            LocalMovementBuffer.Add(payload, payload.Tick);
        }

        /// <summary>
        /// Enregistre l'état autoritaire du mouvement côté serveur et l'envoie aux clients
        /// </summary>
        private void OnTickServer()
        {
            MovementPayload payload = physicsCalculator.CurrentMovementPayload;
            payload.Tick = NetworkManager.ServerTime.Tick;
            ServerMovementBuffer.Add(payload, payload.Tick);
            SendStateClientRpc(payload);
        }

        /// <summary>
        /// Reçoit l'état autoritaire du serveur et déclenche l'événement de réconciliation
        /// </summary>
        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendStateClientRpc(MovementPayload payload)
        {
            if (IsServer) return;

            ServerMovementBuffer.Add(payload, payload.Tick);
            OnServerStateReceivedClient?.Invoke(payload);
        }
    }
}