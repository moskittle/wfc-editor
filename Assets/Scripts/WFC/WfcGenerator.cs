using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;
using PrototypeInfo = CustomModuleData.PrototypeInfo;
using Random = UnityEngine.Random;

public class WfcGenerator : MonoBehaviour {
    public float blockSize = 5f;
    public int maxBacktracking = 100;
    public Vector3Int size = new Vector3Int(8, 3, 8);
    public GameObject moduleObject;
    public CustomModuleData moduleData;

    public Action onGenerateComplete = null;
    private List<List<List<List<PrototypeInfo>>>> wfc;
    private PrototypeInfo airPrototype;
    private int airCount = 0;
    private bool backTrackingProcess = false;
    private Vector3Int lastCoord;
    private int iteration = 0;
    private int backTrackingCount = 0;

    private readonly Dictionary<Vector3Int, int> directionToIndex = new Dictionary<Vector3Int, int> {
        {Vector3Int.left, 0}, // FaceType.Left
        {Vector3Int.back, 1}, // FaceType.Back
        {Vector3Int.right, 2}, // FaceType.Right
        {Vector3Int.forward, 3}, // FaceType.Forward
        {Vector3Int.down, 4}, // FaceType.Down
        {Vector3Int.up, 5} // FaceType.Up
    };

    private enum FaceDir {
        Left = 0,
        Back = 1,
        Right = 2,
        Forward = 3,
        Down = 4,
        Up = 5
    }

    private Stack<Vector3Int> changedCoords = new Stack<Vector3Int>();
    private List<Vector3Int> pinCoords = new List<Vector3Int>();

    private List<HistoryItem> history = new List<HistoryItem>();

    [Header("Debug")]
    public bool eachGridGeneration = false;

    public bool pauseAfterGeneration = false;

    public class HistoryItem {
        public Vector3Int coord;
        public List<PrototypeInfo> originPrototypes;
        public Dictionary<Vector3Int, List<PrototypeInfo>> propagatePrototypes;
    }

    public void Start() {
        //GenerateTerrain();
    }

    public void InitGird3D() {
        bool zeroSize = size.x == 0 || size.y == 0 || size.z == 0;
        if (moduleData == null || zeroSize) {
            Debug.Log("No terrain generated. Module data is missing, or size is 0.");
        }

        wfc = new List<List<List<List<PrototypeInfo>>>>();
        for (int x = 0; x < size.x; ++x) {
            var vx = new List<List<List<PrototypeInfo>>>();
            for (int y = 0; y < size.y; ++y) {
                var vy = new List<List<PrototypeInfo>>();
                for (int z = 0; z < size.z; ++z) {
                    var allPrototypesCopy = Instantiate(moduleData);
                    vy.Add(allPrototypesCopy.modules);
                    airPrototype ??= new PrototypeInfo(allPrototypesCopy.modules[0]);
                }

                vx.Add(vy);
            }

            wfc.Add(vx);
        }
    }

    [Button("Generate WFC Terrain")]
    public void GenerateTerrain() {
        GenerateTerrainProcess();

#if UNITY_EDITOR
        EditorApplication.isPaused = pauseAfterGeneration;
#endif
    }

    public void GenerateTerrainProcess() {
        Profiler.BeginSample("WFC Terrain Generation");
        Stopwatch sw = new Stopwatch();
        sw.Start();

        ResetGeneration();

        InitGird3D();

        ApplyCustomConstraints();

        ApplyCustomPin();

        // TODO: change to coroutine
        iteration = 0;
        int maxIteration = 1000;
        while (IsCollapsed() == false) {
            if (backTrackingCount > maxBacktracking) {
                sw.Stop();
                Debug.Log("Backtracking max count, regenerate");
                GC.Collect();
                GenerateTerrainProcess();
                return;
            }
            
            if (iteration++ > maxIteration) {
                Debug.LogError($"[MANUAL BREAK] Iteration over {maxIteration} Possible infinite loop.");
                return;
            }

            //如果是回溯状态继续上次坐标
            if (backTrackingProcess) {
                backTrackingProcess = false;
                Iterate(lastCoord);
            } else Iterate(GetMinEntropyCoord()); //获取熵值最小的坐标
        }


        // Create game objects on each grid
        if (eachGridGeneration) {
            for (int x = 0; x < size.x; x++) {
                for (int y = 0; y < size.y; y++) {
                    for (int z = 0; z < size.z; z++) {
                        CreateGridElement(x, y, z);
                    }
                }
            }
        } else {
            CreateGridCombine();
            gameObject.AddComponent<MeshCollider>();
        }


        sw.Stop();
        Debug.Log(
            $"Generation Complete. Time: {sw.ElapsedMilliseconds / 1000f} sec    Iteration: {iteration}  Backtracking {backTrackingCount}");
        Profiler.EndSample();

        onGenerateComplete?.Invoke();
    }

    private void CreateGridCombine() {
        var mfChildren = moduleObject.GetComponentsInChildren<MeshFilter>();
        if (mfChildren.Length == 0) return;

        var mrChildren = moduleObject.GetComponentsInChildren<MeshRenderer>();

        var mrSelf = gameObject.GetComponent<MeshRenderer>();
        var mfSelf = gameObject.GetComponent<MeshFilter>();

        if (!mrSelf || !mfSelf) {
            mrSelf = gameObject.AddComponent<MeshRenderer>();
            mfSelf = gameObject.AddComponent<MeshFilter>();
        }

        var combineMats = new List<Material>();
        foreach (var render in mrChildren) {
            var localMats = render.sharedMaterials;
            foreach (var mat in localMats) {
                if (!combineMats.Contains(mat)) {
                    combineMats.Add(mat);
                }
            }
        }

        var finalMats = new List<Material>();
        var recordPosition = moduleObject.transform.position;
        moduleObject.transform.position = Vector3.zero;

        //提取submesh
        var subMeshs = new List<Mesh>();
        foreach (var m in combineMats) {
            var combines = new List<CombineInstance>();
            for (int x = 0; x < size.x; x++) {
                for (int y = 0; y < size.y; y++) {
                    for (int z = 0; z < size.z; z++) {
                        //CreateGridElement(x, y, z);
                        var prototypeInfo = wfc[x][y][z][0];
                        if (string.IsNullOrEmpty(prototypeInfo.mesh)) continue;
                        var moduleModel = moduleObject.transform.Find(prototypeInfo.mesh);
                        if (moduleModel.GetComponent<MeshFilter>() == null) continue;
                        var meshFilter = moduleModel.GetComponent<MeshFilter>();
                        meshFilter.transform.position =
                            new Vector3(blockSize * x + 1, blockSize * y + 1, blockSize * z + 1);
                        meshFilter.transform.eulerAngles = Vector3.up * (90f * prototypeInfo.rotation);
                        var localMats = meshFilter.GetComponent<MeshRenderer>().sharedMaterials;
                        for (int j = 0; j < localMats.Length; j++) {
                            if (localMats[j] != m) {
                                continue;
                            }

                            if (!finalMats.Contains(m)) finalMats.Add(m);
                            var ci = new CombineInstance();
                            ci.mesh = meshFilter.sharedMesh;
                            ci.transform = meshFilter.transform.localToWorldMatrix;
                            ci.subMeshIndex = j;
                            //meshFilter.enabled = false;
                            combines.Add(ci);
                        }
                    }
                }
            }

            var subMesh = new Mesh();
            subMesh.CombineMeshes(combines.ToArray(), true, true);
            subMeshs.Add(subMesh);
        }

        //合并submesh
        var finalCombiners = new List<CombineInstance>();
        foreach (var t in subMeshs) {
            var ci = new CombineInstance();
            ci.mesh = t;
            ci.transform = Matrix4x4.identity;
            finalCombiners.Add(ci);
        }

        var newMesh = new Mesh();
        newMesh.CombineMeshes(finalCombiners.ToArray(), false); //合并submesh网格 
        mfSelf.mesh = newMesh;
        mrSelf.sharedMaterials = finalMats.ToArray();
        mrSelf.enabled = true;

        Distribute(moduleObject.transform);
        moduleObject.transform.position = recordPosition;
    }

    private bool IsCollapsed() {
        for (int x = 0; x < size.z; ++x) {
            for (int y = 0; y < size.y; ++y) {
                for (int z = 0; z < size.z; ++z) {
                    if (wfc[x][y][z].Count > 1) {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private void Distribute(Transform parent) {
        int i = 0;
        foreach (Transform t in parent) {
            t.localPosition = Vector3.forward * i * blockSize * 2f;
            i++;
        }
    }

    private void Iterate(Vector3Int coord) {
        //备份原数据
        var originPrototypes = new List<PrototypeInfo>(wfc[coord.x][coord.y][coord.z]);
        
        //塌陷
        var chosenPrototype = CollapseAt(coord);

        for (int i = 0; i < originPrototypes.Count; i++) {
            if (originPrototypes[i].index == chosenPrototype.index)
                originPrototypes.Remove(originPrototypes[i]);
        }

        //传播塌陷
        var propagatePrototypes = PropagateAt(coord);

        //开始进行回溯
        if (backTrackingProcess) {
            backTrackingCount++;
            lastCoord = coord;
            
            //数据重新写入
            wfc[coord.x][coord.y][coord.z] = originPrototypes;
            RestorePropagatePrototypes(propagatePrototypes);
            
            //如果没有可选择的，则回到上一次记录，直到有为止
            while (originPrototypes.Count == 0) {
                // TODO 这个地方记录为空，不知道为啥，只有进行重新生成
                if (history.Count == 0) {
                    backTrackingCount += maxBacktracking;
                    return;
                }
                var lastChosen = history.Last();
                originPrototypes = lastChosen.originPrototypes;
                wfc[lastChosen.coord.x][lastChosen.coord.y][lastChosen.coord.z] = lastChosen.originPrototypes;
                RestorePropagatePrototypes(lastChosen.propagatePrototypes);
                lastCoord = lastChosen.coord;
                history.Remove(lastChosen);
            }
        } else {
            var historyItem = new HistoryItem();
            historyItem.coord = coord;
            historyItem.originPrototypes = originPrototypes;
            historyItem.propagatePrototypes = propagatePrototypes;
            history.Add(historyItem);
        }
    }

    private void RestorePropagatePrototypes(Dictionary<Vector3Int, List<PrototypeInfo>> propagatePrototypes) {
        foreach (var propagate in propagatePrototypes) {
            wfc[propagate.Key.x][propagate.Key.y][propagate.Key.z] = propagate.Value;
        }
    }

    private Vector3Int GetMinEntropyCoord() {
        // exception check
        if (wfc.Count == 0) {
            Debug.LogError("Min entropy coord not found!");
            return new Vector3Int(-1, -1, -1);
        }

        int minEntropy = int.MaxValue;
        var minEntropyCoordList = new List<Vector3Int>();

        for (int x = 0; x < size.x; ++x) {
            for (int y = 0; y < size.y; ++y) {
                for (int z = 0; z < size.z; ++z) {
                    var currEntropy = wfc[x][y][z].Count;

                    if (currEntropy > 1) {
                        // new min entropy
                        if (currEntropy < minEntropy) {
                            minEntropyCoordList.Clear();
                            minEntropyCoordList.Add(new Vector3Int(x, y, z));
                            minEntropy = currEntropy;
                        }
                        // equal min entropy (tied)
                        else if (currEntropy == minEntropy) {
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

    private PrototypeInfo CollapseAt(Vector3Int coord) {
        var possiblePrototypes = wfc[coord.x][coord.y][coord.z];

        // narrow possible prototypes down to 1
        var weightedSelection = GetWeightedSelection(possiblePrototypes);
        var chosenPrototype = new PrototypeInfo(weightedSelection);

        possiblePrototypes.Clear();
        possiblePrototypes.Add(chosenPrototype);

        if (string.IsNullOrEmpty(chosenPrototype.mesh)) {
            airCount++;
        }

        return chosenPrototype;
    }

    private Dictionary<Vector3Int, List<PrototypeInfo>> PropagateAt(Vector3Int coord) {
        var propagatePrototypes = new Dictionary<Vector3Int, List<PrototypeInfo>>();
        changedCoords.Push(coord);
        while (changedCoords.Count > 0) {
            var currentCoord = changedCoords.Pop();
            var currentPrototypes = wfc[currentCoord.x][currentCoord.y][currentCoord.z];

            //获取正确的方向
            var validDirections = GetValidDirections(currentCoord);

            foreach (var dir in validDirections) {
                var otherCoord = currentCoord + dir;
                var otherPrototype = wfc[otherCoord.x][otherCoord.y][otherCoord.z];
                var possibleOtherIndice = GetPrototypeIndice(otherPrototype);

                //备份用于回溯
                var copyPrototypes = otherPrototype.ToList();

                //取到该方向上的对于index
                var neighborDirIndex = GetNeighborDirectionIndex(dir);

                //取到该方向上所有可能的prototype index
                var possibleNeighborIndice = GetAllPossibleNeighbors(currentPrototypes, neighborDirIndex);

                //记录下原来的数量
                var originalCount = possibleOtherIndice.Count;

                //对比剔除掉该方向上不可能的prototype index
                possibleOtherIndice.RemoveAll(item => !possibleNeighborIndice.Contains(item));

                //对比剔除掉该方向上不可能的prototype
                otherPrototype.RemoveAll(item => !possibleOtherIndice.Contains(item.index)); // update grid data

                // 如果为空则加入空气块
                if (otherPrototype.Count == 0) {
                    if (!propagatePrototypes.ContainsKey(otherCoord))
                        propagatePrototypes.Add(otherCoord, copyPrototypes);
                    //如果是pin的坐标则需要回溯
                    if (pinCoords.Contains(otherCoord)) {
                        backTrackingProcess = true;
                        return propagatePrototypes;
                    }

                    otherPrototype.Add(airPrototype);
                }

                // 对比原来的数量，如果改变了，则加入处理列表
                bool hasChanged = possibleOtherIndice.Count < originalCount;
                if (hasChanged && !changedCoords.Contains(otherCoord)) {
                    if (!propagatePrototypes.ContainsKey(otherCoord))
                        propagatePrototypes.Add(otherCoord, copyPrototypes);
                    changedCoords.Push(otherCoord);
                }
            }
        }

        return propagatePrototypes;
    }

    private PrototypeInfo GetWeightedSelection(List<PrototypeInfo> prototypeInfos) {
        float sum = 0;

        foreach (var prototype in prototypeInfos) {
            sum += prototype.probability;
        }

        var weightedSelection = Random.Range(0, sum);

        sum = 0;
        foreach (var prototype in prototypeInfos) {
            sum += prototype.probability;

            if (sum >= weightedSelection) {
                return prototype;
            }
        }

        return null;
    }

    private void ApplyCustomPin() {
        for (int x = 0; x < size.x; ++x) {
            for (int y = 0; y < size.y; ++y) {
                for (int z = 0; z < size.z; ++z) {
                    var possiblePrototypes = wfc[x][y][z];
                    var coord = new Vector3Int(x, y, z);
                    if (x == 0 && y == 0 && z == 0) {
                        pinCoords.Add(coord);
                        possiblePrototypes.Clear();
                        possiblePrototypes.Add(new PrototypeInfo(moduleData.modules[52]));
                    }

                    if (x == 0 && y == 0 && z == size.z - 1) {
                        pinCoords.Add(coord);
                        possiblePrototypes.Clear();
                        possiblePrototypes.Add(new PrototypeInfo(moduleData.modules[53]));
                    }

                    if (x == size.x - 1 && y == 0 && z == size.z - 1) {
                        pinCoords.Add(coord);
                        possiblePrototypes.Clear();
                        possiblePrototypes.Add(new PrototypeInfo(moduleData.modules[54]));
                    }

                    if (x == size.x - 1 && y == 0 && z == 0) {
                        pinCoords.Add(coord);
                        possiblePrototypes.Clear();
                        possiblePrototypes.Add(new PrototypeInfo(moduleData.modules[55]));
                    }

                    //little house in center
                    if (x == size.x / 2 && y == size.y - 1 && z == size.z / 2) {
                        pinCoords.Add(coord);
                        possiblePrototypes.Clear();
                        possiblePrototypes.Add(new PrototypeInfo(moduleData.modules[51]));
                    }
                    if (x == size.x / 2 + 1 && y == size.y - 1 && z == size.z / 2 + 1) {
                        pinCoords.Add(coord);
                        possiblePrototypes.Clear();
                        possiblePrototypes.Add(new PrototypeInfo(moduleData.modules[5]));
                    }
                }
            }
        }
    }

    private void ApplyCustomConstraints() {
        for (int x = 0; x < size.x; ++x) {
            for (int y = 0; y < size.y; ++y) {
                for (int z = 0; z < size.z; ++z) {
                    var possiblePrototypes = wfc[x][y][z];
                    var coord = new Vector3Int(x, y, z);
                    // 1. constrain bottom layer
                    if (y == 0) {
                        possiblePrototypes.RemoveAll(prototype =>
                            !(prototype.faceDetails[(int) FaceDir.Down].Connector == 0 &&
                              prototype.faceDetails[(int) FaceDir.Down].Invariant) ||
                            prototype.constraintFromTags.Contains("bot"));
                        AddToPropagationStack(coord);
                    }

                    // 2. everything but bottom
                    if (y > 0) {
                        possiblePrototypes.RemoveAll(prototype => prototype.constraintToTags.Contains("bot"));
                        AddToPropagationStack(coord);
                    }

                    // 3. constrain top layer to not contain uncapped prototypes
                    if (y == size.y - 1) {
                        possiblePrototypes.RemoveAll(prototype =>
                            !(prototype.faceDetails[(int) FaceDir.Up].Connector == 0 &&
                              prototype.faceDetails[(int) FaceDir.Up].Invariant ||
                              prototype.constraintToTags.Contains("top")));
                        AddToPropagationStack(coord);
                    }

                    // 4. constrain left bound
                    if (x == 0) {
                        possiblePrototypes.RemoveAll(prototype =>
                            !(prototype.faceDetails[(int) FaceDir.Left].Connector == 0 &&
                              prototype.faceDetails[(int) FaceDir.Left].Symmetric));
                        AddToPropagationStack(coord);
                    }

                    // 5. constrain right bound
                    if (x == size.x - 1) {
                        possiblePrototypes.RemoveAll(prototype =>
                            !(prototype.faceDetails[(int) FaceDir.Right].Connector == 0 &&
                              prototype.faceDetails[(int) FaceDir.Right].Symmetric));
                        AddToPropagationStack(coord);
                    }


                    // 6. constrain back bound
                    if (z == 0) {
                        possiblePrototypes.RemoveAll(prototype =>
                            !(prototype.faceDetails[(int) FaceDir.Back].Connector == 0 &&
                              prototype.faceDetails[(int) FaceDir.Back].Symmetric));
                        AddToPropagationStack(coord);
                    }

                    // 7. constrain forward bound
                    if (z == size.z - 1) {
                        possiblePrototypes.RemoveAll(prototype =>
                            !(prototype.faceDetails[(int) FaceDir.Forward].Connector == 0 &&
                              prototype.faceDetails[(int) FaceDir.Forward].Symmetric));
                        AddToPropagationStack(coord);
                    }

                    // // 8. remove gaps in the middle
                    // int edgeX = 3, edgeZ = 3;
                    // if (x >= edgeX && x <= size.x - edgeX - 1 && z >= edgeZ && z <= size.z - edgeZ - 1 && y == 0)
                    // {
                    //     possiblePrototypes.RemoveAll(
                    //         // prototype => prototype.constraintToTags.Contains("bot")
                    //         
                    //         prototype => (prototype.faceDetails[(int) FaceDir.Left].Connector == 0)
                    //         || (prototype.faceDetails[(int) FaceDir.Back].Connector == 0)
                    //         || (prototype.faceDetails[(int) FaceDir.Right].Connector == 0)
                    //         || (prototype.faceDetails[(int) FaceDir.Forward].Connector == 0)
                    //     );
                    // }


                    // List<float> edges = new List<float>{0.2f, 0.25f, 0.3f};
                    // for (int i = 0; i < edges.Count; ++i)
                    // {
                    //     if (y == i)
                    //     {
                    //         if (x > size.x * edges[i] && x <= size.x * (1 - edges[i]) && z > size.z * edges[i] && z <= size.z * (1 - edges[i]))
                    //         {
                    //             possiblePrototypes.RemoveAll(
                    //                 prototype => prototype.constraintToTags.Contains("edge")
                    //             );
                    //             AddToPropagationStack(coord);
                    //         }
                    //     }
                    // }
                }
            }
        }
    }

    private List<Vector3Int> GetValidDirections(Vector3Int coord) {
        List<Vector3Int> validDirections = new List<Vector3Int>();

        if (coord.x > 0) {
            validDirections.Add(Vector3Int.left);
        }

        if (coord.x < size.x - 1) {
            validDirections.Add(Vector3Int.right);
        }

        if (coord.y > 0) {
            validDirections.Add(Vector3Int.down);
        }

        if (coord.y < size.y - 1) {
            validDirections.Add(Vector3Int.up);
        }

        if (coord.z > 0) {
            validDirections.Add(Vector3Int.back);
        }

        if (coord.z < size.z - 1) {
            validDirections.Add(Vector3Int.forward);
        }

        return validDirections;
    }

    /// <summary>
    /// 获取对应方向上的index
    /// </summary>
    /// <param name="dir">方向vec</param>
    /// <returns></returns>
    private int GetNeighborDirectionIndex(Vector3Int dir) {
        int index = -1;

        var dirKeys = directionToIndex.Keys.ToList();
        foreach (var dirKey in dirKeys) {
            if (dir.Equals(dirKey)) {
                index = directionToIndex[dirKey];
            }
        }

        return index;
    }

    private List<int> GetPrototypeIndice(List<PrototypeInfo> prototypes) {
        List<int> prototypeIndice = new List<int>();

        foreach (var prototype in prototypes) {
            prototypeIndice.Add(prototype.index);
        }

        return prototypeIndice;
    }

    private List<int> GetAllPossibleNeighbors(List<PrototypeInfo> prototypes, int dirIndex) {
        List<int> allPossibleNeighbors = new List<int>();

        foreach (var prototype in prototypes) {
            var neighbors = prototype.neighbors[dirIndex].neighborIndexOnOneFace;
            var excludedNeighbours = prototype.faceDetails[dirIndex].ExcludedNeighbours;
            foreach (var neighborIndex in neighbors) {
                if (!allPossibleNeighbors.Contains(neighborIndex) && !excludedNeighbours.Contains(neighborIndex)) {
                    allPossibleNeighbors.Add(neighborIndex);
                }
            }
        }

        return allPossibleNeighbors;
    }

    private void CreateGridElement(int x, int y, int z) {
        var prototypeInfo = wfc[x][y][z][0];
        var meshObj = moduleObject.transform.Find(wfc[x][y][z][0].mesh);
        if (meshObj == null) return;

        var element = Instantiate(meshObj.gameObject, transform);
        DestroyImmediate(element.GetComponent<ModulePrototype>());
        element.name = $"{x}, {y}, {z} ({prototypeInfo.index})";
        element.transform.position = new Vector3(blockSize * x + 1, blockSize * y + 1, blockSize * z + 1);
        element.transform.eulerAngles = Vector3.up * (90f * prototypeInfo.rotation);
    }

    private void ResetGeneration() {
        transform.DeleteChildren();
        pinCoords.Clear();
        changedCoords.Clear();
        airCount = 0;
        backTrackingCount = 0;
        airPrototype = null;
        backTrackingProcess = false;
        if (GetComponent<MeshFilter>()) DestroyImmediate(GetComponent<MeshFilter>());
        if (GetComponent<MeshRenderer>()) DestroyImmediate(GetComponent<MeshRenderer>());
        if (GetComponent<MeshCollider>()) DestroyImmediate(GetComponent<MeshCollider>());
    }

    private void AddToPropagationStack(Vector3Int coord) {
        if (!changedCoords.Contains(coord)) {
            changedCoords.Push(coord);
        }
    }
}