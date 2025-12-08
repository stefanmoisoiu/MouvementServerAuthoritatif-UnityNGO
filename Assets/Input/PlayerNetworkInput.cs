using Networking;
using Unity.Netcode;
using UnityEngine;

namespace Input
{
    public class PlayerNetworkInput : NetworkBehaviour
    {
        [SerializeField] private InputManager inputManager;
        
        private readonly CircularBuffer<InputPayload> _clientInputBuffer = new(BufferSize);
        private readonly CircularBuffer<InputPayload> _serverInputBuffer = new(BufferSize);
        
        private const int BufferSize = 256;
        
        [SerializeField] [Range(0,5)] private int redundancyPayloadCount = 3; 
        [SerializeField] [Range(0,10)] private int tickSafetyMargin = 3;
        
        
        public int LastPredictionTick { get; private set; }
        
        /// <summary>
        /// Met à jour le tick de prédiction en ajoutant la marge de sécurité et la latence au tick serveur actuel
        /// </summary>
        public void UpdatePredictionTick() => 
            LastPredictionTick = NetworkManager.LocalTime.Tick + tickSafetyMargin;

        private bool _accumulateJump;
        private bool _accumulateJumpContinuous;

        public InputPayload ContinuousInput { get; private set; }

        public InputPayload ServerInput { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsOwner) inputManager.OnJump += JumpPressed;
        }

        private void JumpPressed()
        {
            _accumulateJump = true;
            _accumulateJumpContinuous = true;
        }
        private int _tick = -1;
        private void FixedUpdate()
        {
            if (IsOwner || (IsClient && IsServer))
            {
                // Online owner or offline
                ContinuousInput = new InputPayload
                {
                    Tick = 0,
                    Move = inputManager.MoveInput,
                    Jump = _accumulateJumpContinuous
                };
                _accumulateJumpContinuous = false;
            }
            
            if (!IsOwner) return;
            // Online owner
            
            UpdatePredictionTick();
            if (_tick == LastPredictionTick) return;
            _tick = LastPredictionTick;
            OnTickClient();
        }

        /// <summary>
        /// Traite un nouveau tick côté client : capture les inputs et les envoie au serveur avec redondance
        /// </summary>
        private void OnTickClient()
        {
            int currentTick = LastPredictionTick;

            InputPayload newPayload = new InputPayload
            {
                Tick = currentTick,
                Move = inputManager.MoveInput,
                Jump = _accumulateJump
            };
            _clientInputBuffer.Add(newPayload, currentTick);

            _accumulateJump = false;
            
            InputPayload[] inputsToSend = new InputPayload[redundancyPayloadCount + 1];
            inputsToSend[0] = newPayload;
            
            for (int i = 1; i <= redundancyPayloadCount; i++)
            {
                int pastTick = currentTick - i;
                if (_clientInputBuffer.GetMostRecent(pastTick, x => x.Tick, out InputPayload pastInput))
                {
                    if (pastInput.Tick == pastTick)
                        inputsToSend[i] = pastInput;
                    else
                        inputsToSend[i] = default;
                }
            }

            InputRedundancyPayload redundancyPayload = new InputRedundancyPayload { Inputs = inputsToSend };
            
            if (IsServer)
            {
                AddInputsToServerBuffer(redundancyPayload);
                SendInputsClientRpc(redundancyPayload);
            }
            else
            {
                SendInputsServerRpc(redundancyPayload);
            }
        }

        [ServerRpc(Delivery = RpcDelivery.Unreliable)] 
        private void SendInputsServerRpc(InputRedundancyPayload redundancyPayload)
        {
            AddInputsToServerBuffer(redundancyPayload);
            SendInputsClientRpc(redundancyPayload);
        }

        [ClientRpc(Delivery = RpcDelivery.Unreliable)]
        private void SendInputsClientRpc(InputRedundancyPayload redundancyPayload)
        {
            if (IsServer) return;
            AddInputsToServerBuffer(redundancyPayload);
        }

        /// <summary>
        /// Ajoute les inputs reçus dans le buffer serveur après validation des ticks
        /// </summary>
        private void AddInputsToServerBuffer(InputRedundancyPayload payload)
        {
            int currentServerTick = NetworkManager.ServerTime.Tick;
            
            if (ServerInput.Tick < payload.Inputs[0].Tick) ServerInput = payload.Inputs[0];

            foreach (InputPayload input in payload.Inputs)
            {
                if (input.Tick < currentServerTick - BufferSize) continue;
                if (input.Tick > currentServerTick + BufferSize) 
                {
                    Debug.LogWarning($"Input rejeté : Trop loin dans le futur (ACTUEL : currentServerTick. INPUT : {input.Tick})");
                    continue;
                }

                InputPayload cleanInput = input;
                cleanInput.Move = Vector2.ClampMagnitude(input.Move, 1f);

                _serverInputBuffer.Add(cleanInput, cleanInput.Tick);
            }
        }

        /// <summary>
        /// Récupère l'input client le plus récent pour le tick spécifié
        /// </summary>
        public bool GetClientInput(int tick, out InputPayload res) =>
            _clientInputBuffer.GetMostRecent(tick, val => val.Tick, out res);
        
        /// <summary>
        /// Récupère l'input serveur le plus récent pour le tick spécifié
        /// </summary>
        public bool GetServerInput(int tick, out InputPayload res) =>
            _serverInputBuffer.GetMostRecent(tick, val => val.Tick, out res);
    }
}