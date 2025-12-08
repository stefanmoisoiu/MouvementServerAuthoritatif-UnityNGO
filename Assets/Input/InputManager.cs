using System;
using UnityEngine;

namespace Input
{
    public class InputManager : MonoBehaviour
    {
        private Controls _controls;

        public Vector2 MoveInput { get; private set; }
        public event Action OnJump;
    
        private void OnEnable()
        {
            _controls ??= new();
            _controls.Enable();
        
            _controls.Player.Move.performed += ctx => MoveInput = ctx.ReadValue<Vector2>();
            _controls.Player.Move.canceled += ctx => MoveInput = Vector2.zero;
        
            _controls.Player.Jump.started += ctx => OnJump?.Invoke();
        }

        private void OnDisable()
        {
            _controls.Disable();
        }
    }
}
