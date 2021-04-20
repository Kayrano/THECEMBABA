using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using enemies;

namespace Shooting
{
    public class Shoot : MonoBehaviour
    {
        public Transform firepoint;
        
        public GameObject bulletPrefab;
        

        
        void Update()
        {
           
            if (Input.GetButtonDown("Fire1"))
            {
                Fire();
            }
        }

        void Fire()
        {

            GameObject bullet = Instantiate(bulletPrefab, firepoint.position, firepoint.rotation);
            Destroy(bullet, 5f);

        }
    }
}
