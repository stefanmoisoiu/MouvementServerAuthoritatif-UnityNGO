using System.Collections.Generic;
using Input;
using Physics;
using Unity.Netcode;
using UnityEngine;
using Visual;

namespace Networking
{
    /// <summary>
    /// Gère la réconciliation du joueur entre le client et le serveur.
    /// Corrige la position du joueur en fonction des mises à jour du serveur pour éviter les désynchronisations.
    /// </summary>
    public class PlayerReconciliation : NetworkBehaviour
    {
        private bool _hasReceivedFirstState;
        
        [Header("References")]
        [SerializeField] private PlayerNetworkInput playerNetworkInput;
        [SerializeField] private PlayerNetworkState playerNetworkState;
        [SerializeField] private PlayerPhysicsCalculator physicsCalculator;
        
        [SerializeField] private VisualOffset visualOffset;
        
        [Header("Reconciliation Settings")]
        [SerializeField] private bool enableCorrection = true;
        [SerializeField] [Range(0, 5f)] private float smoothCorrectionThreshold = 0.05f;
        [SerializeField] [Range(0, 5f)] private float hardCorrectionThreshold = 0.8f;
        [SerializeField] private float ownerSmoothCorrectionSpeed = 5f;
        [SerializeField] private float notOwnermoothCorrectionSpeed = 5f;
        
        private MovementPayload _mostRecentServerPayload;
        private MovementPayload _correspondingClientPayload;
        
        [Space] [SerializeField] private bool allowDebug;
        
        [Header("Debug Visualization - Owner")]
        [SerializeField] private bool showClientPosition = true;
        [SerializeField] private bool showClientVelocity = true;
        [SerializeField] private bool showServerRawPosition = true;
        [SerializeField] private bool showServerExtrapolated = true;
        [SerializeField] private bool showExtrapolationTrajectory = true;
        [SerializeField] private bool showErrorLine = true;
        [SerializeField] private bool showErrorDistance = true;
        
        [Header("Debug Visualization - Not Owner")]
        [SerializeField] private bool showServerPosition = true;
        [SerializeField] private bool showVisualPosition = true;
        [SerializeField] private bool showOffsetLine = true;

        private readonly VisualOffset.VisualOffsetComponent _visualOffsetComponent = new();

        public override void OnNetworkSpawn()
        {
            if (IsServer) return;
            if (IsOwner) playerNetworkState.OnServerStateReceivedClient += CheckReconciliationOwner;
            else playerNetworkState.OnServerStateReceivedClient += CheckReconciliationNotOwner;
            visualOffset.AddComponent(_visualOffsetComponent);
        }

        private void OnDisable()
        {
            playerNetworkState.OnServerStateReceivedClient -= CheckReconciliationOwner;
            playerNetworkState.OnServerStateReceivedClient -= CheckReconciliationNotOwner;
            visualOffset?.RemoveComponent(_visualOffsetComponent);
        }

        private void Update()
        {
            if (!IsClient) return;
            if (IsServer) return;
            
            UpdateSmoothCorrection(Time.deltaTime);
        }

        private void OnDrawGizmos()
        {
            if (!IsClient) return;
            if (IsServer) return;
            if (!allowDebug) return;

            if (IsOwner)
                DrawDebugOwner();
            else
                DrawDebugNotOwner();
        }
        
        /// <summary>
        /// Vérifie la réconciliation pour le propriétaire en comparant la prédiction client avec l'état serveur.
        /// </summary>
        private void CheckReconciliationOwner(MovementPayload serverPayload)
        {
            if (_mostRecentServerPayload.Tick > 0 && serverPayload.Tick < _mostRecentServerPayload.Tick) return;
            _mostRecentServerPayload = serverPayload;
            
            if (!_hasReceivedFirstState)
            {
                HardCorrection(serverPayload);
                _correspondingClientPayload = serverPayload; 
                _hasReceivedFirstState = true;
                return;
            }
    
            int serverTick = serverPayload.Tick;
            if (!playerNetworkState.LocalMovementBuffer.GetMostRecent(serverTick, p => p.Tick, out MovementPayload correspondingLocalPayload)) return;

            _correspondingClientPayload = correspondingLocalPayload;
            
            float positionError = Vector3.Distance(serverPayload.Position, correspondingLocalPayload.Position);
            
            if (positionError > hardCorrectionThreshold) HardCorrection(serverPayload);
            else if (positionError > smoothCorrectionThreshold) SmoothCorrection(serverPayload);
        }
        
        /// <summary>
        /// Vérifie la réconciliation pour un joueur non-propriétaire en comparant la position serveur avec la position locale.
        /// </summary>
        private void CheckReconciliationNotOwner(MovementPayload serverPayload)
        {
            if (_mostRecentServerPayload.Tick > 0 && serverPayload.Tick < _mostRecentServerPayload.Tick) return;
            
            _mostRecentServerPayload = serverPayload;
            
            if (!_hasReceivedFirstState)
            {
                HardCorrection(serverPayload);
                _hasReceivedFirstState = true;
                return;
            }
            
            float positionError = Vector3.Distance(serverPayload.Position, physicsCalculator.CurrentMovementPayload.Position);
            if (positionError > hardCorrectionThreshold) HardCorrection(serverPayload);
            else SmoothCorrection(serverPayload);
        }

        /// <summary>
        /// Applique progressivement la correction visuelle avec une interpolation exponentielle.
        /// La vitesse de correction s'adapte à la magnitude de l'erreur et au mouvement du joueur.
        /// </summary>
        private void UpdateSmoothCorrection(float deltaTime)
        {
            if (!enableCorrection) return;
            if (!_hasReceivedFirstState) return;
            if (_visualOffsetComponent.Offset == Vector3.zero) return;

            if (IsOwner) Debug.LogError($"{_visualOffsetComponent.Offset}");
            
            if (_visualOffsetComponent.Offset.sqrMagnitude < 0.0001f)
            {
                _visualOffsetComponent.Offset = Vector3.zero;
                return;
            }
            
            float correctionSpeed = IsOwner ? ownerSmoothCorrectionSpeed : notOwnermoothCorrectionSpeed;
            _visualOffsetComponent.Offset = Vector3.Lerp(_visualOffsetComponent.Offset, Vector3.zero, correctionSpeed * deltaTime);
        }
        
        /// <summary>
        /// Applique une correction de position en créant un offset visuel pour masquer la téléportation.
        /// </summary>
        private void SmoothCorrection(MovementPayload serverPayload)
        {
            MovementPayload beforeCorrectionClient = physicsCalculator.CurrentMovementPayload;
            if (IsOwner)
                TeleportAndReplay(serverPayload);
            else
                physicsCalculator.SetPayload(serverPayload);
            MovementPayload afterCorrectionClient = physicsCalculator.CurrentMovementPayload;
            
            _visualOffsetComponent.Offset += beforeCorrectionClient.Position - afterCorrectionClient.Position;
        }

        /// <summary>
        /// Applique une correction immédiate sans transition visuelle.
        /// </summary>
        private void HardCorrection(MovementPayload serverPayload)
        {
            if (IsOwner)
                TeleportAndReplay(serverPayload);
            else
                physicsCalculator.SetPayload(serverPayload);
            
            _visualOffsetComponent.Offset = Vector3.zero;
        }
        
        /// <summary>
        /// Téléporte le joueur à la position serveur et rejoue tous les inputs client depuis ce tick jusqu'au tick actuel.
        /// Utilisé pour la réconciliation client-serveur.
        /// </summary>
        private void TeleportAndReplay(MovementPayload serverPayload)
        {
            if (!IsOwner) Debug.LogError("TeleportAndReplay should only be called by the owner.");
            
            int startTick = serverPayload.Tick + 1;
            int currentTick = playerNetworkInput.LastPredictionTick;
            float timeStep = NetworkManager.ServerTime.FixedDeltaTime;
            
            physicsCalculator.SetPayload(serverPayload);
            playerNetworkState.LocalMovementBuffer.Add(serverPayload, serverPayload.Tick);
            
            for (int tick = startTick; tick < currentTick; tick++)
            {
                playerNetworkInput.GetClientInput(tick, out InputPayload pastInput);
                physicsCalculator.Step(pastInput, timeStep, tick);
                playerNetworkState.LocalMovementBuffer.Add(physicsCalculator.CurrentMovementPayload, tick);
            }
        }

        /// <summary>
        /// Extrapole un payload en simulant les inputs jusqu'au tick actuel.
        /// Utilisé principalement pour le debug.
        /// </summary>
        private MovementPayload ExtrapolatePayload(MovementPayload payload)
        {
            int startTick = payload.Tick + 1;
            int currentTick = playerNetworkInput.LastPredictionTick;
            float timeStep = NetworkManager.ServerTime.FixedDeltaTime;
            
            for (int tick = startTick; tick <= currentTick; tick++)
            {
                playerNetworkInput.GetClientInput(tick, out InputPayload pastInput);
                payload = physicsCalculator.StepPayload(pastInput, payload, timeStep);
            }
            
            return payload;
        }

        /// <summary>
        /// Extrapole un payload en simulant les inputs jusqu'au tick actuel et retourne tous les états intermédiaires.
        /// Utilisé principalement pour le debug et la visualisation des trajectoires.
        /// </summary>
        private List<MovementPayload> ExtrapolatePayloadAll(MovementPayload payload)
        {
            List<MovementPayload> allPayloads = new List<MovementPayload>();
            
            int startTick = payload.Tick + 1;
            int currentTick = playerNetworkInput.LastPredictionTick;
            float timeStep = NetworkManager.ServerTime.FixedDeltaTime;
            
            allPayloads.Add(payload);
            
            for (int tick = startTick; tick <= currentTick; tick++)
            {
                playerNetworkInput.GetClientInput(tick, out InputPayload pastInput);
                payload = physicsCalculator.StepPayload(pastInput, payload, timeStep);
                allPayloads.Add(payload);
            }
            
            return allPayloads;
        }
        
        private void DrawDebugOwner()
        {
            if (!_hasReceivedFirstState) return;
            if (_correspondingClientPayload.Tick == 0) return;

            if (showClientPosition)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_correspondingClientPayload.Position, 0.1f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(_correspondingClientPayload.Position + Vector3.up * 0.5f, 
                    $"CLIENT\nTick: {_correspondingClientPayload.Tick}",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.green }, alignment = TextAnchor.MiddleCenter });
                #endif
            }

            if (showClientVelocity && _correspondingClientPayload.Velocity.magnitude > 0.1f)
            {
                Gizmos.color = Color.green;
                Vector3 arrowEnd = _correspondingClientPayload.Position + _correspondingClientPayload.Velocity;
                Gizmos.DrawLine(_correspondingClientPayload.Position, arrowEnd);
                
                Vector3 arrowDir = _correspondingClientPayload.Velocity.normalized;
                Vector3 right = Vector3.Cross(Vector3.up, arrowDir).normalized * 0.1f;
                Vector3 arrowTip1 = arrowEnd - arrowDir * 0.2f + right;
                Vector3 arrowTip2 = arrowEnd - arrowDir * 0.2f - right;
                Gizmos.DrawLine(arrowEnd, arrowTip1);
                Gizmos.DrawLine(arrowEnd, arrowTip2);
            }

            if (showServerRawPosition)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_mostRecentServerPayload.Position, 0.1f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(_mostRecentServerPayload.Position + Vector3.up * 0.5f, 
                    $"SERVER (Raw)\nTick: {_mostRecentServerPayload.Tick}",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red }, alignment = TextAnchor.MiddleCenter });
                #endif
            }

            if (showServerExtrapolated)
            {
                List<MovementPayload> extrapolatedPayloads = ExtrapolatePayloadAll(_mostRecentServerPayload);
                
                for (int i = 0; i < extrapolatedPayloads.Count; i++)
                {
                    var payload = extrapolatedPayloads[i];
                    bool isLast = (i == extrapolatedPayloads.Count - 1);
                    
                    Gizmos.color = Color.cyan;
                    float sphereSize = isLast ? 0.15f : 0.05f;
                    Gizmos.DrawWireSphere(payload.Position, sphereSize);
                    
                    #if UNITY_EDITOR
                    if (isLast)
                    {
                        UnityEditor.Handles.Label(payload.Position + Vector3.down * 0.5f, 
                            $"SERVER (Extrap)\nTick: {payload.Tick}",
                            new GUIStyle() { normal = new GUIStyleState() { textColor = Color.cyan }, alignment = TextAnchor.MiddleCenter });
                    }
                    #endif
                    
                    if (showExtrapolationTrajectory && i < extrapolatedPayloads.Count - 1)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawLine(payload.Position, extrapolatedPayloads[i + 1].Position);
                    }
                }
            }
            
            if (showErrorLine)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_correspondingClientPayload.Position, _mostRecentServerPayload.Position);
                    
                if (showErrorDistance)
                {
                    float distance = Vector3.Distance(_correspondingClientPayload.Position, _mostRecentServerPayload.Position);
                    Vector3 midPoint = (_correspondingClientPayload.Position + _mostRecentServerPayload.Position) / 2f;
                    
                    #if UNITY_EDITOR
                    UnityEditor.Handles.Label(midPoint, 
                        $"Error: {distance:F3}m",
                        new GUIStyle() { normal = new GUIStyleState() { textColor = Color.yellow }, alignment = TextAnchor.MiddleCenter, fontSize = 14 });
                    #endif
                }
            }

            #if UNITY_EDITOR
            if (IsOwner)
            {
                UnityEditor.Handles.Label(physicsCalculator.CurrentMovementPayload.Position + Vector3.up * 1.5f, 
                    $"Tick Latency: {NetworkManager.NetworkTimeSystem.TickLatency}",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.white }, alignment = TextAnchor.MiddleCenter, fontSize = 14 });
            }
            #endif
        }

        private void DrawDebugNotOwner()
        {
            if (_mostRecentServerPayload.Tick == 0) return;

            #if UNITY_EDITOR
            UnityEditor.Handles.Label(_mostRecentServerPayload.Position + Vector3.up * 2f, 
                $"Tick Latency: {NetworkManager.NetworkTimeSystem.TickLatency}",
                new GUIStyle() { normal = new GUIStyleState() { textColor = Color.white }, alignment = TextAnchor.MiddleCenter, fontSize = 14 });
            #endif
            
            if (showServerPosition)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_mostRecentServerPayload.Position, 0.1f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(_mostRecentServerPayload.Position + Vector3.up * 0.5f, 
                    $"SERVER\nTick: {_mostRecentServerPayload.Tick}",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red }, alignment = TextAnchor.MiddleCenter });
                #endif
            }

            if (showVisualPosition)
            {
                Vector3 visualPos = _mostRecentServerPayload.Position + _visualOffsetComponent.Offset;
                Gizmos.color = new Color(0.5f, 0f, 1f);
                Gizmos.DrawWireSphere(visualPos, 0.1f);
                
                #if UNITY_EDITOR
                UnityEditor.Handles.Label(visualPos + Vector3.down * 0.5f, 
                    $"VISUAL\nOffset: {_visualOffsetComponent.Offset.magnitude:F3}m",
                    new GUIStyle() { normal = new GUIStyleState() { textColor = new Color(0.5f, 0f, 1f) }, alignment = TextAnchor.MiddleCenter });
                #endif
            }

            if (showOffsetLine)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(_mostRecentServerPayload.Position, _mostRecentServerPayload.Position + _visualOffsetComponent.Offset);
            }
        }
    }
}
