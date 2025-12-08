using System;
using System.Linq;
using Input;
using Networking;
using Unity.Netcode;
using UnityEngine;
using Visual;

namespace Physics
{
    /// <summary>
    /// Calcule et gère la physique du joueur en appliquant tous les composants physiques.
    /// Gère l'interpolation visuelle entre FixedUpdate (60Hz) et Update (variable) pour un rendu fluide.
    /// </summary>
    public class PlayerPhysicsCalculator : NetworkBehaviour
    {
        [SerializeField] private CharacterController characterController;

        [SerializeField] private PlayerNetworkInput playerInput;
        [SerializeField] private PlayerNetworkState playerState;
        [SerializeField] private VisualOffset visualOffset;
        
        
        [SerializeField] private MonoBehaviour[] physicsComponents;

        public MovementPayload CurrentMovementPayload { get; private set; }
        
        private VisualOffset.VisualOffsetComponent _visualOffsetComponent = new();
        
        
        public void BakePhysicsComponents()
        {
            physicsComponents = GetComponentsInChildren<IPhysicsComponent>(false).Cast<MonoBehaviour>().ToArray();
        }

        private void OnEnable()
        {
            CurrentMovementPayload = GenerateDefaultPayload();
            visualOffset.AddComponent(_visualOffsetComponent);
        }

        private void OnDisable()
        {
            visualOffset?.RemoveComponent(_visualOffsetComponent);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentMovementPayload = GenerateDefaultPayload();
            }
            else if (IsOwner)
            {
                playerInput.UpdatePredictionTick();
                CurrentMovementPayload = GenerateDefaultPayload();
            }
        }
        
        public MovementPayload GenerateDefaultPayload() => new MovementPayload()
        {
            Tick = playerInput.LastPredictionTick,
            Position = characterController.transform.position,
            Velocity = Vector3.zero
        };

        private void Update()
        {
            UpdateVisualOffset();
        }

        /// <summary>
        /// Réduit progressivement l'offset visuel pour interpoler entre les frames de FixedUpdate.
        /// Permet un rendu fluide même quand Update tourne plus vite que FixedUpdate (60Hz).
        /// </summary>
        private void UpdateVisualOffset()
        {
            if (_visualOffsetComponent.Offset == Vector3.zero) return;
            
            Vector3 reduction = _visualOffsetComponent.Offset * Mathf.Clamp01(Time.deltaTime / Time.fixedDeltaTime);
            
            if (_visualOffsetComponent.Offset.sqrMagnitude < 0.0001f)
            {
                _visualOffsetComponent.Offset = Vector3.zero;
            }
            else
            {
                _visualOffsetComponent.Offset -= reduction;
            }
        }
        
        /// <summary>
        /// Calcule l'offset visuel entre deux positions pour créer une interpolation fluide.
        /// Permet de masquer visuellement les changements de position brusques entre frames physiques.
        /// </summary>
        private void SetVisualOffset(MovementPayload previousPayload, MovementPayload currentPayload)
        {
            Vector3 offset = previousPayload.Position - currentPayload.Position;
            if (offset.sqrMagnitude > 0.0001f) _visualOffsetComponent.Offset += offset;
        }

        private void FixedUpdate()
        {
            // Calcul du mouvement
            if (IsOwner || (!IsClient && !IsHost))
            {
                // En ligne et owner, ou hors-ligne
                
                MovementPayload previousPayload = CurrentMovementPayload;
                UpdateMovement(playerInput.ContinuousInput, Time.fixedDeltaTime);
                SetVisualOffset(previousPayload, CurrentMovementPayload);
            }
            else if (IsServer)
            {
                // En ligne et serveur
                
                int targetTick = NetworkManager.ServerTime.Tick;
                playerInput.GetServerInput(targetTick, out InputPayload input);
                MovementPayload previousPayload = CurrentMovementPayload;
                UpdateMovement(input, Time.fixedDeltaTime);
                SetVisualOffset(previousPayload, CurrentMovementPayload);
            }
        }

        /// <summary>
        /// Met à jour le mouvement
        /// </summary>
        private void UpdateMovement(InputPayload inputPayload, float deltaTime)
        {
            Step(inputPayload, deltaTime, playerInput.LastPredictionTick);
        }

        /// <summary>
        /// Exécute un step de physique complet et met à jour la position du joueur.
        /// </summary>
        public void Step(InputPayload inputPayload, float deltaTime, int tick)
        {
            CurrentMovementPayload = StepMovementPayload(inputPayload, CurrentMovementPayload, deltaTime);
            CurrentMovementPayload = ApplyVelocity(CurrentMovementPayload, deltaTime);

            MovementPayload payload = CurrentMovementPayload;
            payload.Tick = tick;
            CurrentMovementPayload = payload;
        }

        /// <summary>
        /// Simule un step de physique sur un payload sans modifier la position réelle du joueur.
        /// Utilisé pour les prédictions et les extrapolations.
        /// </summary>
        public MovementPayload StepPayload(InputPayload input, MovementPayload payload, float deltaTime)
        {
            payload = StepMovementPayload(input, payload, deltaTime);
            payload.Position += payload.Velocity * deltaTime;

            return payload;
        }
        /// <summary>
        /// Applique tous les composants physiques sur le payload dans l'ordre.
        /// </summary>
        private MovementPayload StepMovementPayload(InputPayload input, MovementPayload movement, float deltaTime)
        {
            foreach (MonoBehaviour monoBehaviour in physicsComponents)
            {
                IPhysicsComponent physicsComponent = monoBehaviour as IPhysicsComponent;
                if (physicsComponent == null) throw new Exception($"Physics component {monoBehaviour.name} is not of type IPhysicsComponent");
                movement = physicsComponent.Step(input, movement, deltaTime);
            }

            return movement;
        }

        /// <summary>
        /// Applique la vélocité au CharacterController et met à jour la position du payload.
        /// </summary>
        private MovementPayload ApplyVelocity(MovementPayload payload, float deltaTime)
        {
            Vector3 previousPosition = characterController.transform.position;
            characterController.Move(payload.Velocity * deltaTime);
            Vector3 newPosition = characterController.transform.position;
            
            payload.Position = newPosition;
            // payload.Velocity = (newPosition - previousPosition) / deltaTime;
            
            return payload;
        }
        
        public void Move(Vector3 velocity) => characterController.Move(velocity);

        /// <summary>
        /// Définit directement le payload en téléportant le CharacterController.
        /// </summary>
        public void SetPayload(MovementPayload payload)
        {
            characterController.enabled = false;
            characterController.transform.position = payload.Position;
            characterController.enabled = true;
            
            CurrentMovementPayload = payload;
            
            UnityEngine.Physics.SyncTransforms();
        }
    }
}
