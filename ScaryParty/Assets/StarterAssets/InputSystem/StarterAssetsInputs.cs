using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
        
        [Header("Weapon Input Values")]
        public float scrollWeapon;
        public bool aim;
        public bool fire;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook && !ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked)
			{
				LookInput(value.Get<Vector2>());
			}
			else
			{
				LookInput(Vector2.zero);
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

        public void OnScrollWeapon(InputValue value)
        {
            if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || !cursorInputForLook)
            {
                scrollWeapon = 0f;
                return;
            }
            scrollWeapon = value.Get<float>();
        }

        public void OnAim(InputValue value)
        {
            if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || !cursorInputForLook)
            {
                aim = false;
                return;
            }
            aim = value.isPressed;
        }

        public void OnFire(InputValue value)
        {
            if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || !cursorInputForLook)
            {
                fire = false;
                return;
            }
            fire = value.isPressed;
        }
#endif


		public void MoveInput(Vector2 newMoveDirection)
		{
			if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || !cursorInputForLook)
			{
				move = Vector2.zero;
				return;
			}
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || !cursorInputForLook)
			{
				look = Vector2.zero;
				return;
			}
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || !cursorInputForLook)
			{
				jump = false;
				return;
			}
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			if (ScaryParty.Pizzeria.Presentation.GameplayInputGate.IsLocked || !cursorInputForLook)
			{
				sprint = false;
				return;
			}
			sprint = newSprintState;
		}

		private void OnApplicationFocus(bool hasFocus)
		{
			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}
	}
	
}