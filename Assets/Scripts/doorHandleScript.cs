using System;
using Unity.VectorGraphics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class doorHandleScript : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;
    [SerializeField] private bool locked = false;

    public void OnGrab()
    {
        if (!locked && sceneToLoad != "")
        SceneManager.LoadScene(sceneToLoad);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (locked && collision.gameObject.GetComponent<keyScript>())
            locked = false;
    }
}