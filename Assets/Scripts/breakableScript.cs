using System;
using UnityEngine;

public class breakableScript : MonoBehaviour
{
    [SerializeField] private int durability = 3;

    public void TakeHit()
    {
        durability--;
        Debug.Log(name + " durability is " + durability);
            
        if (durability == 0)
            Destroy(gameObject);
    }
}
