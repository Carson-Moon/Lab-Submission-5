using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
public class CharController : MonoBehaviour
{
    private Controls Input;
    public float speed = 5f;
    // Start is called before the first frame update
    private Vector2 moveDirection = Vector2.zero;

    private InputAction move;
    public Rigidbody rb;

    private void Awake()
    {
        Input = new Controls();
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        moveDirection = move.ReadValue<Vector2>();
        
    }

    private void FixedUpdate()
    {
        rb.velocity = new Vector3(moveDirection.x * speed, moveDirection.y * speed, moveDirection.z * speed);
    }

    private void OnEnable()
    {
        move = Input.Player.Move;
        move.Enable();
    }
    private void OnDisable()
    {
        move.Disable();
    }
}
