using Unity.VectorGraphics;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class doorHandleScript : MonoBehaviour
{
    [SerializeField] private string sceneToLoad;
    [SerializeField] private bool locked;

    public void OnGrab()
    {
        if (sceneToLoad != "")
        SceneManager.LoadScene(sceneToLoad);
    }
}
