using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using enemies;
using System;

public class EnemyTriggerArea : MonoBehaviour
{
    EnemyAI enemyAIScript;
    Enemy enemyScript;

    [SerializeField]Collider2D playercollision;

    [SerializeField]Transform spawnposition;
    BoxCollider2D triggerarea;
    [SerializeField]GameObject droneprefab;
    private int spawndrones = 0;
    
    private void Awake()
    {
        triggerarea = GetComponent<BoxCollider2D>();

    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        

        if (collision == playercollision )
        {
            int maxspawn = 5;
            for (int i = 0; i < maxspawn; i++)
            {
                Instantiate(droneprefab, spawnposition.position, spawnposition.rotation);
                GetScripts();
                InitializeScripts();
                spawndrones++;
            }
            

            
           
        }
    }

    public void GetScripts()
    {
        enemyAIScript = droneprefab.GetComponent<EnemyAI>();
        enemyScript = droneprefab.GetComponent<Enemy>();
    }

    public void InitializeScripts()
    {
        enemyScript.player = playercollision.gameObject;
        enemyAIScript.target = playercollision.transform;

    }
}
