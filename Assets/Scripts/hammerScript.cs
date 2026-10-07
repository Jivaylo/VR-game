using System;
using UnityEngine;

public class hammerScript : MonoBehaviour
{
    [SerializeField] private int durability = 5; // durability should never be set higher than 5

    private void Start()
    {
        Debug.Log("hammer durability is "+ durability);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<breakableScript>())
        {
            durability--;
            Debug.Log("hammer durability is "+ durability);
            
            if (durability == 0)
                Destroy(transform.parent.gameObject);
        }
    }
}
