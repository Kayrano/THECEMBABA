using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CokeJalouiseOpeningScript : MonoBehaviour
{
    #region Fields
    [SerializeField] public int time;
    [SerializeField] private List<Collider2D> _targetColliders;
    [SerializeField] private Animator animator;
    #endregion
    public bool isTriggered;

    #region Properties

    public List<Collider2D> TargetColliders => _targetColliders;
    #endregion








    #region Instance Methods
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (TargetColliders.Count == 0 || TargetColliders.Contains(other))
        {
            isTriggered = true;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if(TargetColliders.Count == 0 || TargetColliders.Contains(other))
        {
            
            isTriggered = false;
            
        }
    }
    #endregion
    private void Update()
    {
        if (isTriggered)
        {
            OpenJalouise();
        }
        else
            StopCoroutine(Open());
    }
    void OpenJalouise()
    {
        if (Input.GetKeyDown(KeyCode.E))
            StartCoroutine(Open());

        if (Input.GetKeyUp(KeyCode.E))
            StopCoroutine(Open());

    }



    IEnumerator Open()
    {
        
        Debug.Log("E PRESSED");
        yield return new WaitForSeconds(time);
        animator.SetBool("isOpening", true);
       
    }
}
