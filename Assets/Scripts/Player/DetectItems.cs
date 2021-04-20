using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace player
{
    public class DetectItems : MonoBehaviour
    {
        CharacterController2D controller;


        private void Start()
        {
            controller = GetComponent<CharacterController2D>();
        }
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("HealthItem"))
            {

            }
        }
       
    }
}
