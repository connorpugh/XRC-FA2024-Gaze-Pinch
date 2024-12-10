using System.Collections.Generic;
using UnityEngine;

public class ResetCubes : MonoBehaviour
{
    [SerializeField] private List<GameObject> m_Cubes;
    private List<Vector3> m_Positions = new List<Vector3>();
    private List<Quaternion> m_Rotations = new List<Quaternion>();
    
    
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // Save the initial transform of each cube
        foreach (GameObject cube in m_Cubes)
        {
            m_Positions.Add(cube.transform.position);
            m_Rotations.Add(cube.transform.rotation);
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void ResetAllCubes()
    {
        // Iterate through each cube
        for (int i = 0; i < m_Cubes.Count; i++) 
        {
            m_Cubes[i].transform.position = m_Positions[i];
            m_Cubes[i].transform.rotation = m_Rotations[i];
        }
    }
}
