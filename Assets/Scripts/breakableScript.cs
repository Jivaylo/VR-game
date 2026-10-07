using System;
using UnityEngine;

public class breakableScript : MonoBehaviour
{
    [SerializeField] private int durability = 3;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.GetComponent<hammerScript>())
        {
            durability--;
            Debug.Log(name + " durability is " + durability);
            
            if (durability == 0)
                Destroy(gameObject);
        }
    }
}
