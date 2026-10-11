using UnityEngine;

public class Manager : MonoBehaviour
{
    int blueCount = 0;
    int redCount = 0;
    private GameObject target;
    private TargetSpawner targetSpawner;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        DontDestroyOnLoad(this);
        targetSpawner = Object.FindAnyObjectByType<TargetSpawner>();
    }

    // Update is called once per frame
    void Update()
    {
        if (targetSpawner.hasTartget)
        {
            if (target == null)
            {
                target = GameObject.FindWithTag("BlueTarget");

                if (target != null)
                {
                    Debug.Log("Blue Target Found");
                }
                else
                {
                    target = GameObject.FindWithTag("RedTarget");

                    if (target != null)
                    {
                        Debug.Log("Red Target Found");
                    }
                    else
                    {
                        Debug.Log("No target found");
                    }
                }
            }
        }
    }

    public void SortBlue()
    {
        Sort("BlueTarget");
    }

    public void SortRed()
    {
        Sort("RedTarget");
    }

    private void Sort(string expectedTag)
    {
        if (target == null)
        {
            Debug.Log("No target to sort");
            return;
        }

        string targetTag = target.tag;

        if (targetTag == expectedTag)
        {
            if (expectedTag == "BlueTarget")
            {
                blueCount++;
            }
            else
            {
                redCount++;
            }

            Debug.Log(expectedTag + " sorted successfully");
        }
        else
        {
            Debug.Log("Target of wrong type was sorted (it was " + targetTag + ")");
        }

        Destroy(target);
        Debug.Log("Target of type " + targetTag + " was destroyed");

        target = null;

        targetSpawner.hasTartget = false;
        Debug.Log("No more targets exist");
    }
    
    public void blueAnnounce()
    {
        Debug.Log("Blue Button Was Pressed");
    }

    public void redAnnounce()
    {
        Debug.Log("Red Button Was Pressed");
    }
    
}
