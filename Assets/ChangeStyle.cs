using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Attachment;

public class ChangeStyle : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI m_TextLocation;
    [SerializeField] private MicroAttachController m_AttachController;
    [SerializeField] private Image m_ImageLocation;

    [SerializeField] private Sprite m_PinchImage;
    [SerializeField] private Sprite m_ScrollImage;
    
    
    
    
    
    private String pinchMovementText =
        "Pinch Movement Mode:\nLook at a cube & pinch to grab.\nStretch the pinch closer or further from your palm to adjust depth.";

    private String indexScrollingText =
        "Index Scrolling Mode:\nUse your index finger as a scrolling surface; scroll up and down with your middle finger to adjust depth.";

    private bool isIndexScrolling = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SwapControls()
    {
        // If in index scrolling mode, swap to pinch movement
        if (isIndexScrolling)
        {
            m_TextLocation.text = pinchMovementText;
            isIndexScrolling = false;
            m_AttachController.setIndexScrolling(isIndexScrolling);
            m_ImageLocation.sprite = m_PinchImage;
        }
        else
        {
            m_TextLocation.text = indexScrollingText;
            isIndexScrolling = true;
            m_AttachController.setIndexScrolling(isIndexScrolling);
            m_ImageLocation.sprite = m_ScrollImage;
        }
    }
}
