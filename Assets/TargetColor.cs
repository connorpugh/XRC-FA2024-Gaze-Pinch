using System;
using UnityEngine;

[RequireComponent(typeof(MeshRenderer))]
public class TargetColor : MonoBehaviour
{
    private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    [SerializeField] private Color m_GoalColor;
    private Color m_DefaultColor;
    private MeshRenderer m_MeshRenderer;
    private Material m_TargetMaterial;
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        m_MeshRenderer = this.GetComponent<MeshRenderer>();
        m_TargetMaterial = m_MeshRenderer.materials[1];
        m_DefaultColor = m_TargetMaterial.GetColor(EmissionColor);
        m_TargetColor = m_DefaultColor;
        
    }

    private Color m_TargetColor;
    private float m_LerpSpeed = 0.05f;

    // Update is called once per frame
    void Update()
    {
        // Get the current color
        Color currentColor = m_TargetMaterial.GetColor(EmissionColor);
        // If it doesn't match the target, move towards it
        if (currentColor != m_TargetColor)
        {
            Color newColor = Color.Lerp(currentColor, m_TargetColor, m_LerpSpeed);
            m_TargetMaterial.SetColor(EmissionColor, newColor);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log("Object entered target zone");
        m_TargetColor = m_GoalColor;
    }

    private void OnTriggerExit(Collider other)
    {
        Debug.Log("Object exited target zone");
        m_TargetColor = m_DefaultColor;
    }
}
