using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class fuseBoxScript : MonoBehaviour
{
    public bool isOn = true;

    public void onActivate()
    {
        if (isOn)
        {
            isOn = false;
        }
        else
        {
            isOn = true;
        }

        Debug.Log("Enabled is " + isOn);
    }
}
