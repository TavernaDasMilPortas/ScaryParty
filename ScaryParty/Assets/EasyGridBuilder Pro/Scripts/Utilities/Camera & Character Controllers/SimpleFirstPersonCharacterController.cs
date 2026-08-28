using UnityEngine;

namespace SoulGames.Utilities
{
    public class SimpleFirstPersonCharacterController : MonoBehaviour
    {
        [Header("Movement")]
        [Space]
        [Tooltip("Character movement speed")]
        [SerializeField]private float moveSpeed;
        [Tooltip("RigidBody ground drag")]
        [SerializeField]private float groundDrag;
        [Tooltip("Character jump power")]
        [SerializeField]private float jumpForce;
        [Tooltip("Initial jump delay")]
        [SerializeField]private float jumpCooldown;
        [Tooltip("In air movement multiplier")]
        [SerializeField]private float airMultiplier;
        
        [Header("Ground Check")]
        [Space]
        [Tooltip("Player collider height")]
        [SerializeField]private float playerHeight;
        [Tooltip("Layer Mask to check ground. Used for Jump & Fall")]
        [SerializeField]private LayerMask groundLayerMask;
        [Tooltip("Player Position transform empty game object")]
        [SerializeField]private Transform orientation;

        private bool readyToJump;
        private bool grounded;
        private float walkSpeed;
        private float sprintSpeed;
        private float horizontalHandleInput;
        private float verticalHandleInput;
        private Vector3 moveDirection;
        private Rigidbody rb;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true;
            readyToJump = true;
        }

        private void Update()
        {
            grounded = Physics.Raycast(transform.position, Vector3.down, playerHeight * 0.5f + 0.3f, groundLayerMask);

            HandleInput();
            HandleSpeed();

            if (grounded)
            {
                rb.linearDamping = groundDrag;
            }
            else
            {
                rb.linearDamping = 0;
            }
        }

        private void FixedUpdate()
        {
            MovePlayer();
        }

        private void HandleInput()
        {
            horizontalHandleInput = Input.GetAxisRaw("Horizontal");
            verticalHandleInput = Input.GetAxisRaw("Vertical");

            if(Input.GetKey(KeyCode.Space) && readyToJump && grounded)
            {
                readyToJump = false;
                Jump();
                Invoke(nameof(ResetJump), jumpCooldown);
            }
        }

        private void MovePlayer()
        {
            moveDirection = orientation.forward * verticalHandleInput + orientation.right * horizontalHandleInput;

            if(grounded)
            {
                rb.AddForce(moveDirection.normalized * moveSpeed * 10f, ForceMode.Force);
            }
            else if(!grounded)
            {
                rb.AddForce(moveDirection.normalized * moveSpeed * 10f * airMultiplier, ForceMode.Force);
            }
        }

        private void HandleSpeed()
        {
            Vector3 flatVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

            if(flatVel.magnitude > moveSpeed)
            {
                Vector3 limitedVel = flatVel.normalized * moveSpeed;
                rb.linearVelocity = new Vector3(limitedVel.x, rb.linearVelocity.y, limitedVel.z);
            }
        }

        private void Jump()
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(transform.up * jumpForce, ForceMode.Impulse);
        }
        
        private void ResetJump()
        {
            readyToJump = true;
        }
    }
}