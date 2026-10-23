using UnityEngine;

public class DoorAnimationHandler : MonoBehaviour
{
    
    [SerializeField] private Animator animator;
    private bool isOpen = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void Animate()
    {
        if (isOpen)
        {
            animator.SetLayerWeight(1, 0);
            animator.Play("OpenDoor");
        }

        if (!isOpen)
        {
            animator.SetLayerWeight(1, 1);
            animator.Play("CloseDoor");
        }
    }
}
