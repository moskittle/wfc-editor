using NaughtyAttributes;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using PrototypeInfo = CustomModuleData.PrototypeInfo;

public class WfcGenerator : MonoBehaviour
{
    public Vector3 size = new Vector3(8, 3, 8);
    public CustomModuleData moduleData;

    private Dictionary<Vector3, List<PrototypeInfo>> wfc = new Dictionary<Vector3, List<PrototypeInfo>>();

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
                    var allPrototypesCopy = Instantiate(moduleData);
                    
                    wfc.Add(pos, allPrototypesCopy.modules);
                }
            }
        }
    }

    [Button("Generate WFC Terrain")]
    public void GenerateTerrain()
    {
        while(IsCollapsed() == false)
        {
            Iterate();
        }
    }

    private bool IsCollapsed()
    {
        var keys = wfc.Keys.ToList();

        foreach(var key in keys)
        {
            if(wfc[key].Count > 1)
            {
                return false;
            }
        }

        return true;
    }

    private void Iterate()
    {
        var coord = GetMinEntropyCoord();
        CollapseAt(coord);
        PropagateAt(coord);
    }

    private Vector3 GetMinEntropyCoord()
    {
        // exception check
        if (wfc.Count == 0)
        {
            Debug.LogError("Min entropy coord not found!");
            return new Vector3(-1, -1, -1);
        }

        int min = int.MaxValue;
        var minEntropyCoordList = new List<Vector3>();

        var coords = wfc.Keys.ToList();
        foreach(var coord in coords)
        {
            var currEntropy = wfc[coord].Count;

            if (currEntropy < min)
            {
                minEntropyCoordList.Clear();
                minEntropyCoordList.Add(coord);
                min = currEntropy;
            }
            else if(currEntropy == min)
            {
                minEntropyCoordList.Add(coord);
            }
        }

        // randomly choose one to collapse if there is a tie
        var selection = Random.Range(0, minEntropyCoordList.Count);
        var minEntropyCoord = minEntropyCoordList[selection];

        return minEntropyCoord;
    }

    private void CollapseAt(Vector3 coord)
    {
        var possiblePrototypes = wfc[coord];

        // narrow possible prototypes down to 1
        var selection = Random.Range(0, possiblePrototypes.Count);  // TODO: weighted selection
        var chosenPrototype = new PrototypeInfo(possiblePrototypes[selection]);
        possiblePrototypes.Clear();
        possiblePrototypes.Add(chosenPrototype);
    }

    private void PropagateAt(Vector3 coord)
    {
        Stack<Vector3> changedCoords = new Stack<Vector3>();
        changedCoords.Push(coord);

        while(changedCoords.Count > 0)
        {
            var currentCoord = changedCoords.Pop();

            // for valid directions
        }
    }
}
