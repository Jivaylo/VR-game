using System;
using UnityEngine;
using Random = UnityEngine.Random;

public class TargetSpawner : MonoBehaviour
{
    public bool hasTartget = false;
    public bool RedTartget = false;
    public bool BlueTartget = false;
    [SerializeField]private GameObject[]  targets;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
               
    }

    // Update is called once per frame
    void Update()
    {
        if (!hasTartget)
        {
            int x = Random.Range(0,targets.Length);
            Debug.Log("New tpye was chosen for spawning");
            Instantiate(targets[x], transform.position, transform.rotation);
            if (x == 0)
            {
                BlueTartget = true;
                Debug.Log("Blue target was spawned");
            }
            else if (x == 1)
            {
                RedTartget = true;
                Debug.Log("Red target was spawned");
            }
            hasTartget = true;
        }
    }
}
