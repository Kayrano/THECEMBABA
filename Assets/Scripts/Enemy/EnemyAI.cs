using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using System;

namespace enemies
{
    public class EnemyAI : MonoBehaviour
    {

        public Transform target;


        public float speed = 200f;
        public float nextWayPointDistance = 3f;

        Path path;
        int currentWayPoint = 0;
        bool reachedEndOfPath = false;
        internal bool facingleft = true;

        public Transform enemyGFX;
        Seeker seeker;
        Rigidbody2D rb2d;

        void Start()
        {
            seeker = GetComponent<Seeker>();
            rb2d = GetComponent<Rigidbody2D>();

            InvokeRepeating("UpdatePath", 0f, 0.5f);
            
        }

        void UpdatePath()
        {
            if (seeker.IsDone())
                seeker.StartPath(rb2d.position, target.position, OnPathComplete);
        }

        private void OnPathComplete(Path p)
        {
            if (!p.error)
            {
                path = p;
                currentWayPoint = 0;
            }
        }

        void FixedUpdate()
        {
            if (path == null)
                return;

            if(currentWayPoint >= path.vectorPath.Count)
            {
                reachedEndOfPath = true;
                return;
            }
            else
            {
                reachedEndOfPath = false;
            }

            Vector2 direction = ((Vector2)path.vectorPath[currentWayPoint] - rb2d.position).normalized;
            Vector2 force = direction * speed * Time.deltaTime;

            rb2d.AddForce(force);

            float distance = Vector2.Distance(rb2d.position, path.vectorPath[currentWayPoint]);

            if(distance < nextWayPointDistance)
            {
                currentWayPoint++;
            }

            if (force.x >= 0.01f && facingleft)
            {
                FlipEnemy();

            }
            if(force.x <= -0.01f && !facingleft)
            {
                FlipEnemy();
            }
                

        }

        private void FlipEnemy()
        {
            // Switch the way the player is labelled as facing.
            facingleft = !facingleft;

            transform.Rotate(0f, 180f, 0f);
        }
    }
}
