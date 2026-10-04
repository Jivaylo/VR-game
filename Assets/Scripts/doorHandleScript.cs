using UnityEngine;

public class doorHandleScript : MonoBehaviour
{
    private int timesGrabbed = -1;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    /*void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    } */

    public void OnGrab()
    {
        timesGrabbed++;
        Debug.Log("The door handle has been grabbed. (" +timesGrabbed+")");
    }
}
