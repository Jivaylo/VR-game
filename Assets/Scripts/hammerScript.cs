using System;
using UnityEngine;

public class hammerScript : MonoBehaviour
{
    [SerializeField] private Collider headCollider;
    [SerializeField] private int durability = 5; // durability should never be set higher than 5

    private void Start()
    {
        Debug.Log("hammer durability is "+ durability);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.GetContact(0).thisCollider != headCollider)
            return;

        breakableScript target = collision.collider.GetComponent<breakableScript>();
        if (target == null)
            return;

        target.TakeHit();
        durability--;
        Debug.Log("hammer durability is "+ durability);
            
        if (durability == 0)
            Destroy(gameObject);
    }
}
