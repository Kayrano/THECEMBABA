using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace player
{
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField]internal int maxHealth;
        [SerializeField]Animator animator;
        internal int currentHealth;

        // Start is called before the first frame update
        void Start()
        {
            currentHealth = maxHealth;
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (currentHealth <= 0)
            {
                Die();
            }
        }

        internal void DeductHealth(int damage)
        {
            currentHealth = currentHealth - damage;
        }

        internal void AddHealth(int value)
        {
            currentHealth = currentHealth + value;
            if (currentHealth > maxHealth)
                currentHealth = maxHealth;
        }

        internal void KillPlayer()
        {
            currentHealth = 0;
        }
        internal void Die()
        {
            animator.SetBool("isDead",true);
        }

       
    }
}
