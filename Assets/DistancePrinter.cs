using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DistancePrinter : MonoBehaviour
{
    [SerializeField] private Transform m_ObjectA;
    [SerializeField] private Transform m_ObjectB;
    

    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        // World-level AB vector
        Vector3 ab = (m_ObjectB.position - m_ObjectA.position);
        // Convert to local A reference
        Vector3 ab2 = m_ObjectA.worldToLocalMatrix.MultiplyVector(ab);
        Debug.Log("AB vector: " + ab2);
    }
}
