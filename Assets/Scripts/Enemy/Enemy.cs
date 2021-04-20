using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace enemies
{ 
	public class Enemy : MonoBehaviour
    {
		

		public GameObject player;

		public int health = 100;

		public GameObject deathEffect;
		

        public void TakeDamage(int damage)
		{
			health -= damage;

			if (health <= 0)
			{
				Time.timeScale = 0.1f;
				Die();
				Time.timeScale = 1f;
			}
		}

		void Die()
		{
			GameObject clone = Instantiate(deathEffect, transform.position, Quaternion.identity);
			Destroy(gameObject);
			Destroy(clone, 0.35f);
			
			
		}

      
       
		
        



    }
}
