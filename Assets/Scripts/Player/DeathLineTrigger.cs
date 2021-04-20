using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace player
{
    public class DeathLineTrigger : MonoBehaviour
    {

        public GameObject player;
        PlayerHealth playerHealth;

        private void Start()
        {
            playerHealth = player.GetComponent<PlayerHealth>();
        }

        //If cembaba cross the deathline it will die.
        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.tag == "Player")
            {
                playerHealth.KillPlayer();
            }
        }
    }
}