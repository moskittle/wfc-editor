using System.Collections;
using NaughtyAttributes;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using PrototypeInfo = CustomModuleData.PrototypeInfo;

// a 3D list which represents grid3D[x][y][z] = List<Possible Prototypes>
using Grid3D = System.Collections.Generic.List<System.Collections.Generic.List<System.Collections.Generic.List<
    System.Collections.Generic.List<CustomModuleData.PrototypeInfo>>>>;

public class WfcGenerator : MonoBehaviour
{
    public Vector3Int size = new Vector3Int(8, 3, 8);
    public Material material;
    public CustomModuleData moduleData;

    private Grid3D wfc;
    private PrototypeInfo airPrototype;
    private const float blockSize = 2f;
    private readonly Dictionary<Vector3Int, int> directionToIndex = new Dictionary<Vector3Int, int>
    {
        {Vector3Int.left, 0},      // FaceType.Left
        {Vector3Int.back, 1},      // FaceType.Back
        {Vector3Int.right, 2},     // FaceType.Right
        {Vector3Int.forward, 3},   // FaceType.Forward
        {Vector3Int.down, 4},      // FaceType.Down
        {Vector3Int.up, 5}         // FaceType.Up
    };
    private enum FaceDir { Left = 0, Back = 1, Right = 2, Forward = 3, Down = 4, Up = 5 }
    private Stack<Vector3Int> changedCoords = new Stack<Vector3Int>();

    [Header("Debug")]
    public bool pauseAfterGeneration = false;

    public void Start()
    {
        GenerateTerrain();
    }

    private void Update()
    {
        
    }
    

    public void InitGird3D()
    {
        bool zeroSize = size.x == 0 || size.y == 0 || size.z == 0;
        if (moduleData == null || zeroSize)
        {
            Debug.Log("No terrain generated. Module data is missing, or size is 0.");
        }
        
        wfc = new Grid3D(new List<List<List<PrototypeInfo>>>[size.x]);
        for (int x = 0; x < size.x; ++x)
        {
            wfc[x] = new List<List<List<PrototypeInfo>>>(new List<List<PrototypeInfo>>[size.y]);
            for (int y = 0; y < size.y; ++y)
            {
                wfc[x][y] = new List<List<PrototypeInfo>>(new List<PrototypeInfo>[size.z]);
                for (int z = 0; z < size.z; ++z)
                {
                    wfc[x][y][z] = new List<PrototypeInfo>(new PrototypeInfo[1]);
                    
                    var allPrototypesCopy = Instantiate(moduleData);
                    wfc[x][y][z] = allPrototypesCopy.modules;

                    if (airPrototype == null)
                    {
                        airPrototype = new PrototypeInfo(allPrototypesCopy.modules[0]);
                    }
                }
            }
        }
    }
    
    [Button("Generate WFC Terrain")]
    public void GenerateTerrain()
    {
        StartCoroutine(GenerateTerrainCoroutine());

#if UNITY_EDITOR
        EditorApplication.isPaused = pauseAfterGeneration;
#endif
    }
    
    static int iteration = 0;
    public IEnumerator GenerateTerrainCoroutine()
    {
        Profiler.BeginSample("WFC Terrain Generation");
        Stopwatch sw = new Stopwatch();
        sw.Start();

        ResetGeneration();

        InitGird3D();

        ApplyCustomConstraints();

        // TODO: change to coroutine
        iteration = 0;
        int maxIteration = 10000;
        while(IsCollapsed() == false)
        {
            if (iteration++ > maxIteration) { Debug.LogError($"[MANUAL BREAK] Iteration over {maxIteration} Possible infinite loop."); break; }
            
            Iterate();
        }

        bool isCollapsed = IsCollapsed();

        // Create game objects on each grid
        for (int x = 0; x < size.x; ++x)
        {
            for (int y = 0; y < size.y; ++y)
            {
                for (int z = 0; z < size.z; ++z)
                {
                    CreateGridElement(x, y, z);
                }
            }
        }

        sw.Stop();
        UnityEngine.Debug.Log($"Generation Complete. Time: {sw.ElapsedMilliseconds / 1000f} sec    Iteration: {iteration}");
        Profiler.EndSample();
        
        yield return new WaitForEndOfFrame();
    }

    private bool IsCollapsed()
    {
        for (int x = 0; x < size.z; ++x)
        {
            for (int y = 0; y < size.y; ++y)
            {
                for (int z = 0; z < size.z; ++z)
                {
                    if (wfc[x][y][z].Count > 1)
                    {
                        return false;
                    }
                }
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

    private Vector3Int GetMinEntropyCoord()
    {
        // exception check
        if (wfc.Count == 0)
        {
            Debug.LogError("Min entropy coord not found!");
            return new Vector3Int(-1, -1, -1);
        }
        
        int minEntropy = int.MaxValue;
        var minEntropyCoordList = new List<Vector3Int>();

        for (int x = 0; x < size.x; ++x)
        {
            for (int y = 0; y < size.y; ++y)
            {
                for (int z = 0; z < size.z; ++z)
                {
                    var currEntropy = wfc[x][y][z].Count;

                    if (currEntropy > 1)
                    {
                        // new min entropy
                        if (currEntropy < minEntropy)
                        {
                            minEntropyCoordList.Clear();
                            minEntropyCoordList.Add(new Vector3Int(x, y, z));
                            minEntropy = currEntropy;
                        }
                        // equal min entropy (tied)
                        else if (currEntropy == minEntropy)
                        {
                            minEntropyCoordList.Add(new Vector3Int(x, y, z));
                        }
                    }
                }
            }
        }

        // randomly choose one to collapse if there is a tie
        var selection = Random.Range(0, minEntropyCoordList.Count);
        var minEntropyCoord = minEntropyCoordList[selection];
        
        return minEntropyCoord;
    }

    private void CollapseAt(Vector3Int coord)
    {
        var possiblePrototypes = wfc[coord.x][coord.y][coord.z];

        // narrow possible prototypes down to 1
        var weightedSelection = GetWeightedSelection(possiblePrototypes);
        var chosenPrototype = new PrototypeInfo(possiblePrototypes[weightedSelection]);
        possiblePrototypes.Clear();
        possiblePrototypes.Add(chosenPrototype);
    }

    private void PropagateAt(Vector3Int coord)
    {
        changedCoords.Push(coord);

        while(changedCoords.Count > 0)
        {
            var currentCoord = changedCoords.Pop();
            var currentPrototypes = wfc[currentCoord.x][currentCoord.y][currentCoord.z];

            var validDirections = GetValidDirections(currentCoord);

            foreach (var dir in validDirections)
            {
                var otherCoord = currentCoord + dir;
                var otherPrototype = wfc[otherCoord.x][otherCoord.y][otherCoord.z];
                var possibleOtherIndice = GetPrototypeIndice(otherPrototype);
                
                var neighborDirIndex = GetNeighborDirectionIndex(dir);
                var possibleNeighborIndice = GetAllPossibleNeighbors(currentPrototypes, neighborDirIndex);

                var originalCount = possibleOtherIndice.Count;
                possibleOtherIndice.RemoveAll(item => !possibleNeighborIndice.Contains(item));  // compare neighbor prototypes and current coord's possible neighbor
                otherPrototype.RemoveAll(item => possibleOtherIndice.Contains(item.index) == false);  // update grid data
                
                // add empty prototype(air) if there is not possible prototype
                if (otherPrototype.Count == 0)
                {
                    otherPrototype.Add(airPrototype);
                }

                // add 'otherCoord' to the stack, if otherCoord changes and the propagation stack does not have this coord 
                bool hasChanged = possibleOtherIndice.Count < originalCount;
                if (hasChanged && changedCoords.Contains(otherCoord) == false)
                {
                    changedCoords.Push(otherCoord);
                }
            }
        }
    }

    private int GetWeightedSelection(List<PrototypeInfo> prototypeInfos)
    {
        List<int> indexPool = new List<int>(prototypeInfos.Count * 3);

        for (int i = 0; i < prototypeInfos.Count; ++i)
        {
            var faceDetails = prototypeInfos[i].faceDetails;
            var isInvariant = faceDetails[4].Invariant && faceDetails[5].Invariant;    // Up and Down faces are both invariant
            var extraCount = isInvariant ? prototypeInfos[i].probability * 4 : prototypeInfos[i].probability;
            for (int j = 0; j < prototypeInfos[i].probability; ++j)
            {
                indexPool.Add(i);
            }
        }

        var weightedSelection = Random.Range(0, indexPool.Count);

        return indexPool[weightedSelection];
    }

    private void ApplyCustomConstraints()
    {
        for (int x = 0; x < size.x; ++x)
        {
            for (int y = 0; y < size.y; ++y)
            {
                for (int z = 0; z < size.z; ++z)
                {
                    var possiblePrototypes = wfc[x][y][z];
                    var coord = new Vector3Int(x, y, z);

                    // 1. constrain bottom layer
                    if (y == 0)
                    {
                        possiblePrototypes.RemoveAll(prototype => !(prototype.faceDetails[(int) FaceDir.Down].Connector == 0 
                                                                       && prototype.faceDetails[(int) FaceDir.Down].Invariant)
                                                                       || prototype.constraintFromTags.Contains("bot"));
                        AddToPropagationStack(coord);
                    }

                    // 2. everything but bottom
                    if (y > 0)
                    {
                        possiblePrototypes.RemoveAll(prototype => prototype.constraintToTags.Contains("bot"));
                        AddToPropagationStack(coord);
                    }
                    
                    // 3. constrain top layer to not contain uncapped prototypes
                    if (y == size.y - 1)
                    {
                        possiblePrototypes.RemoveAll(
                            prototype => !(prototype.faceDetails[(int) FaceDir.Up].Connector == 0 && prototype.faceDetails[(int) FaceDir.Up].Invariant
                                || prototype.constraintToTags.Contains("bot"))
                            );
                        AddToPropagationStack(coord);
                    }

                    // 4. constrain left bound
                    if (x == 0)
                    {
                        possiblePrototypes.RemoveAll(
                            prototype => !(prototype.faceDetails[(int) FaceDir.Left].Connector == 0 && prototype.faceDetails[(int) FaceDir.Left].Symmetric)
                            );
                        AddToPropagationStack(coord);
                    }
                    
                    // 5. constrain right bound
                    if (x == size.x - 1)
                    {
                        possiblePrototypes.RemoveAll(
                            prototype => !(prototype.faceDetails[(int) FaceDir.Right].Connector == 0 && prototype.faceDetails[(int) FaceDir.Right].Symmetric)
                        );
                        AddToPropagationStack(coord);
                    }

                    
                    // 6. constrain back bound
                    if (z == 0)
                    {
                        possiblePrototypes.RemoveAll(
                            prototype => !(prototype.faceDetails[(int) FaceDir.Back].Connector == 0 && prototype.faceDetails[(int) FaceDir.Back].Symmetric)
                        );
                        AddToPropagationStack(coord);
                    }

                    // 7. constrain forward bound
                    if (z == size.z - 1)
                    {
                        possiblePrototypes.RemoveAll(
                            prototype => !(prototype.faceDetails[(int) FaceDir.Forward].Connector == 0 && prototype.faceDetails[(int) FaceDir.Forward].Symmetric)
                        );
                        AddToPropagationStack(coord);
                    }

                    // int edgeX = 1, edgeZ = 1;
                    // // 8. remove gaps in the middle
                    // if (x >= edgeX && x <= size.x - edgeX - 1 && z >= edgeZ && z <= size.z - edgeZ - 1 && y == 0)
                    // {
                    //     possiblePrototypes.RemoveAll(
                    //         prototype => (prototype.faceDetails[(int) FaceDir.Left].Connector == 0)
                    //         || (prototype.faceDetails[(int) FaceDir.Back].Connector == 0)
                    //         || (prototype.faceDetails[(int) FaceDir.Right].Connector == 0)
                    //         || (prototype.faceDetails[(int) FaceDir.Forward].Connector == 0)
                    //     );
                    // }
 
                }
            }
        }
        
        
    }

    private List<Vector3Int> GetValidDirections(Vector3Int coord)
    {
        List<Vector3Int> validDirections = new List<Vector3Int>();

        if (coord.x > 0)
        {
            validDirections.Add(Vector3Int.left);
        }

        if (coord.x < size.x - 1)
        {
            validDirections.Add(Vector3Int.right);
        }

        if (coord.y > 0)
        {
            validDirections.Add(Vector3Int.down);
        }

        if (coord.y < size.y - 1)
        {
            validDirections.Add(Vector3Int.up);
        }

        if (coord.z > 0)
        {
            validDirections.Add(Vector3Int.back);
        }

        if (coord.z < size.z - 1)
        {
            validDirections.Add(Vector3Int.forward);
        }

        return validDirections;
    }

    private int GetNeighborDirectionIndex(Vector3Int dir)
    {
        int index = -1;
        
        var dirKeys = directionToIndex.Keys.ToList();
        foreach (var dirKey in dirKeys)
        {
            if (dir.Equals(dirKey))
            {
                index = directionToIndex[dirKey];
            }
        }
        
        return index;
    }

    private List<int> GetPrototypeIndice(List<PrototypeInfo> prototypes)
    {
        List<int> prototypeIndice = new List<int>();
        
        foreach (var prototype in prototypes)
        {
            prototypeIndice.Add(prototype.index);
        }

        return prototypeIndice;
    }

    private List<int> GetAllPossibleNeighbors(List<PrototypeInfo> prototypes, int dirIndex)
    {
        List<int> allPossibleNeighbors = new List<int>();

        foreach (var prototype in prototypes)
        {
            var neighbors = prototype.neighbors[dirIndex].neighborIndexOnOneFace;

            foreach (var neighborIndex in neighbors)
            {
                if (allPossibleNeighbors.Contains(neighborIndex) == false)
                {
                    allPossibleNeighbors.Add(neighborIndex);
                }
            }
        }

        return allPossibleNeighbors;
    }

    private void CreateGridElement(int x, int y, int z)
    {
        var prototypeInfo = wfc[x][y][z][0];
        GameObject element = new GameObject(x + ", " + y + ", " + z);
        element.transform.parent = GameObject.Find("WfcGenerator").transform;
        element.transform.position = new Vector3(blockSize * x + 1, blockSize * y + 1, blockSize * z + 1);

        if (wfc[x][y][z][0].mesh == null)
        {
            return;
        }

        var rotation = Vector3.up * (90f * prototypeInfo.rotation);
        element.transform.Rotate(rotation);

        var meshFilter = element.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = wfc[x][y][z][0].mesh;
        var submeshCount = meshFilter.sharedMesh.subMeshCount;

        var meshRenderer = element.AddComponent<MeshRenderer>();
        List<Material> materials = new List<Material>{};
        for (int i = 0; i < submeshCount; ++i)
        {
            materials.Add(material);
        }
        meshRenderer.materials = materials.ToArray();
    }

    private void ResetGeneration()
    {
        transform.DeleteChildren();
        changedCoords.Clear();
        airPrototype = null;
    }

    private void AddToPropagationStack(Vector3Int coord)
    {
        if (!changedCoords.Contains(coord))
        {
            changedCoords.Push(coord);
        }
    }
    
}
