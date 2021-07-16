using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WfcGenerator : MonoBehaviour
{
    public Vector3 size = new Vector3(8, 3, 8);
    public CustomModuleData moduleData;

    private Dictionary<Vector3, CustomModuleData> wfc = new Dictionary<Vector3, CustomModuleData>();

    public void Start()
    {
        Init(size, moduleData);
    }

    private void Update()
    {
        
    }

    private void Init(Vector3 size, CustomModuleData moduleData)
    {
        if (moduleData == null || size.Equals(Vector3.zero))
        {
            Debug.Log("No terrain generated. Module data is missing.");
        }

        for (int y = 0; y < size.y; ++y)
        {
            for (int z = 0; z < size.z; ++z)
            {
                for (int x = 0; x < size.x; ++x)
                {
                    var pos = new Vector3(x, y, z);
                    var allPrototypes = Instantiate(moduleData);
                    
                    wfc.Add(pos, allPrototypes);
                }
            }
        }
    }

}
