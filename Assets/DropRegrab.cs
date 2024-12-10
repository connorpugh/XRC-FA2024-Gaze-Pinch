using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class DropRegrab : MonoBehaviour
{
    [SerializeField] private XRBaseInteractor m_Interactor;
    private IXRSelectInteractable m_DroppedObject;
    [SerializeField] private InputActionProperty m_GrabAction;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_GrabAction.action.Enable();
        m_GrabAction.action.started += OnGrab;
        m_Interactor.selectExited.AddListener(SelectExit);
        m_Interactor.selectEntered.AddListener(SelectEnter);
        
    }

    private void SelectExit(SelectExitEventArgs args)
    {
        // When a grab ends, save the dropped object.
        Debug.Log(args.interactorObject + " released " + args.interactableObject);
        m_DroppedObject = args.interactableObject;
    }

    private void SelectEnter(SelectEnterEventArgs args)
    {
        // When a new grab begins, clear the dropped object.
        Debug.Log(args.interactorObject + " grabbed " + args.interactableObject);
        m_DroppedObject = null;
    }

    private void OnGrab(InputAction.CallbackContext callbackContext)
    {
        // If the interactor is not currently hovering an object and there is a dropped object,
        // regrab the last dropped object.
        Debug.Log("Grab detected");
        if (m_Interactor.interactablesHovered.Count < 1 && m_DroppedObject != null)
        {
            Debug.Log("Regrabbing dropped object");
            m_Interactor.interactionManager.SelectEnter(m_Interactor, m_DroppedObject);
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
