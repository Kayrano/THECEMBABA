using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using enemies;

namespace Shooting
{
    public class Bullet : MonoBehaviour
    {
        public int damage = 40;
        internal float speed = 25;
        public Rigidbody2D rb2d;
        public GameObject impacteffect;
        private void Start()
        {
            
            rb2d.velocity = transform.right * speed;
        }

        private void OnTriggerEnter2D(Collider2D hitInfo)
        {
            Enemy enemy = hitInfo.GetComponent<Enemy>();
            if(enemy != null)
            {
                enemy.TakeDamage(damage);
            }

            GameObject clone = Instantiate(impacteffect, transform.position, transform.rotation);
            Destroy(gameObject);
            Destroy(clone, 0.35f);
        }


    }
}
