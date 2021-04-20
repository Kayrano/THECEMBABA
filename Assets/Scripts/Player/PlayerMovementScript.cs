using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace player
{
    public class PlayerMovementScript : MonoBehaviour
    {
        #region Components Initialize

        public CharacterController2D controller;
        Animator animator;
        Rigidbody2D rb2d;
        #endregion

        #region Player Variables
        [SerializeField] private float slideSpeed = 0.5f;
        private bool isSliding = false;
        private bool isJump = false;
        private bool isMatrixEscape;
        public float runSpeed = 40f;

        #endregion



        float horizontalmove = 0f;
        // Start is called before the first frame update
        void Start()
        {
            animator = GetComponent<Animator>();
            rb2d = GetComponent<Rigidbody2D>();
        }



        private void FixedUpdate()
        {
            if (!isMatrixEscape && !isSliding)
            {

                controller.Move(horizontalmove * Time.fixedDeltaTime, false, isJump);
                isJump = false;

            }




            if (isSliding)
                Sliding();



            if (controller.m_Grounded == false)
            {
                animator.SetBool("isGrounded", false);
            }
            else if (controller.m_Grounded == true)
            {
                animator.SetBool("isJumping", false);
                animator.SetBool("isGrounded", true);
            }


        }

        

        // Update is called once per frame
        void Update()
        {
            

           


            #region Move Input
            horizontalmove = Input.GetAxisRaw("Horizontal") * runSpeed;
            animator.SetFloat("PlayerSpeed", Mathf.Abs(horizontalmove));
            animator.SetFloat("PlayerVelocity",rb2d.velocity.normalized.magnitude);
            #endregion

            #region Jump Input
            if (Input.GetButtonDown("Jump"))
            {
                isJump = true;
                animator.SetBool("isJumping", true);

            }
            #endregion

            #region MatrixEscape Input
            if (Input.GetButtonDown("MatrixEscape"))
            {
                isMatrixEscape = true;
                animator.SetBool("isMatrixEscaping", true);
            }
            if (Input.GetButtonUp("MatrixEscape"))
            {
                isMatrixEscape = false;
                animator.SetBool("isMatrixEscaping", false);
            }
            #endregion

            #region Sliding Input 
            if (Input.GetKeyDown(KeyCode.C) && Mathf.Abs(horizontalmove) >0.1)
                isSliding = true;
            #endregion




        }




        #region Sliding Methods
        void Sliding()
        {
            
            animator.SetBool("isSliding", true);
            if (controller.m_FacingRight)
            {
                rb2d.AddForce(Vector2.right * slideSpeed);

            }

            if (!controller.m_FacingRight)
            {
                rb2d.AddForce(Vector2.left * slideSpeed);

            }

            StartCoroutine("StopSliding");
        }
        IEnumerator StopSliding()
        {
            
            yield return new WaitForSeconds(1f);
            animator.SetBool("isSliding", false);
            isSliding = false;


        }
        #endregion











    }

}