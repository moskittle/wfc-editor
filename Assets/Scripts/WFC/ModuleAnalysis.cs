using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;

using HorizontalFaceDetails = ModulePrototype.HorizontalFaceDetails;
using VerticalFaceDetails = ModulePrototype.VerticalFaceDetails;


public class ModuleAnalysis : MonoBehaviour {
#if UNITY_EDITOR
    class VFaceAndCounter
    {
        public VerticalFaceDetails vFaceDetail;
        public int counter;
        public FaceType faceType;

        public VFaceAndCounter(VerticalFaceDetails _vFaceDetail, int _counter, FaceType _faceType)
        {
            vFaceDetail = _vFaceDetail;
            counter = _counter;
            faceType = _faceType;
        }
    }


    class HFaceAndCounter
    {
        public HorizontalFaceDetails hFaceDetail;
        public int counter;

        public HFaceAndCounter(HorizontalFaceDetails _vFaceDetail, int _counter)
        {
            hFaceDetail = _vFaceDetail;
            counter = _counter;
        }
    }
    
    public int digit = 4;
    public float vertexSize = 0.03f;

    private GameObject gGameObject = null;
    private Dictionary<FaceType, List<Vector3>> gVertsGroup = new Dictionary<FaceType, List<Vector3>>();

    public enum FaceType {
        Up,
        Down,
        Left,
        Right,
        Forward,
        Back
    }

    [Button]
    public void CheckModulePrototype() {
        var addIdx = 0;
        foreach (Transform t in transform) {
            var mp = t.GetComponent<ModulePrototype>();
            if (mp == null) {
                mp.gameObject.AddComponent<ModulePrototype>();
                addIdx++;
            }
        }

        Debug.Log($"AddComponent for {addIdx} items");
    }

    [Button]
    public void Analysis() {
        var modulePrototypes = gameObject.transform.GetComponentsInChildren<ModulePrototype>();
        if (modulePrototypes.Length == 0) {
            Debug.LogError("No ModulePrototype Component process!");
            return;
        }

        // add empty horizontal/vertical faces to dictionary when initialized
        
        // --------------------------------------------Horizontal-------------------------------
        var hConnectorId = 0;
        var connectorHorizontal = new Dictionary<List<Vector3>, HFaceAndCounter>();
        var hEmptyFace = new HorizontalFaceDetails();
        hEmptyFace.Symmetric = true;
        hEmptyFace.Connector = hConnectorId;
        HFaceAndCounter emptyHFaceAndCounter = new HFaceAndCounter(hEmptyFace, 0);
        connectorHorizontal.Add(new List<Vector3>(), emptyHFaceAndCounter);
        hConnectorId++;
        
        int vConnectorId = 0;
        var vEmptyFace = new VerticalFaceDetails();
        vEmptyFace.Invariant = true;
        vEmptyFace.Connector = vConnectorId;
        VFaceAndCounter emptyVFaceAndCounter = new VFaceAndCounter(vEmptyFace, 0, FaceType.Down);
        Dictionary<VerticalFaceDetails, int> verticalVariantCount = new Dictionary<VerticalFaceDetails, int>();

        foreach (var mp in modulePrototypes)
        {
            var meshFilter = mp.GetComponent<MeshFilter>();

            if (meshFilter == null)
            {
                // empty horizontal face (air prototype)
                foreach (var pair in connectorHorizontal)
                {
                    var emptyList = new List<Vector3>();
                    var hFaceAndCounter = pair.Value;
                    if (emptyList.EqualList(pair.Key))
                    {
                        CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, mp.Forward);
                        CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, mp.Back);
                        CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, mp.Left);
                        CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, mp.Right);
                        hFaceAndCounter.counter += 4;
                    }
                }
                
                continue;
            }
            
            var allVerts = meshFilter.sharedMesh.vertices;

            var vertsOnFace = new Dictionary<FaceType, List<Vector3>> {
                {FaceType.Left, new List<Vector3>()},
                {FaceType.Down, new List<Vector3>()},
                {FaceType.Back, new List<Vector3>()},
                {FaceType.Right, new List<Vector3>()},
                {FaceType.Up, new List<Vector3>()},
                {FaceType.Forward, new List<Vector3>()},
            };
            
            foreach (var vertex in allVerts)
            {
                var vertexFixed = vertex.FixedVector3(digit);

                RoundVertexOnFace(vertex.x, -AbstractMap.BLOCK_SIZE / 2, FaceType.Left, vertexFixed, vertsOnFace);
                RoundVertexOnFace(vertex.z, -AbstractMap.BLOCK_SIZE / 2, FaceType.Back, vertexFixed, vertsOnFace);
                RoundVertexOnFace(vertex.x, AbstractMap.BLOCK_SIZE / 2, FaceType.Right, vertexFixed, vertsOnFace);
                RoundVertexOnFace(vertex.z, AbstractMap.BLOCK_SIZE / 2, FaceType.Forward, vertexFixed, vertsOnFace);
            }

            var hFaceDetail = new List<HorizontalFaceDetails> {mp.Left, mp.Back, mp.Right, mp.Forward};
            var hFaceType = new List<FaceType> {FaceType.Left, FaceType.Back, FaceType.Right, FaceType.Forward};
            var hRotToFront = new Dictionary<FaceType, float> { {FaceType.Forward, 0}, {FaceType.Left, 90}, {FaceType.Back, 180}, {FaceType.Right, 270} };

            for (int i = 0; i < hFaceType.Count; ++i)
            {
                var verts = vertsOnFace[hFaceType[i]];

                if (verts.Count == 0)
                {
                    // empty face
                    foreach (var pair in connectorHorizontal)
                    {
                        if (verts.EqualList(pair.Key))
                        {
                            var hFaceAndCounter = pair.Value;
                            CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, hFaceDetail[i]);
                            hFaceAndCounter.counter++;
                        }
                    }
                }
                else
                {
                    bool connectorFound = false;
                    
                    // rotate each horizontal faces to FrontView Coordinate (正视图)
                    verts = verts.Select(v => {
                        var r = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, hRotToFront[hFaceType[i]], 0), Vector3.one) * new Vector4(v.x, v.y, v.z, 1);
                        return new Vector3(r.x, r.y, r.z).FixedVector3(digit);
                    }).ToList();

                    // check if there already exists a matching connector
                    foreach (var hConnector in connectorHorizontal)
                    {
                        if (verts.EqualList(hConnector.Key))
                        {
                            var hFaceAndCounter = hConnector.Value;
                            CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, hFaceDetail[i]);
                            hFaceAndCounter.counter++;
                            connectorFound = true;
                            break;
                        }
                    }

                    // if no exist connect matches, add this face detail as a new connector
                    // 1. check for symmetry
                    // 2. if not, add this connector and the flipped connector
                    if (connectorFound == false)
                    {
                        // 每个面都旋转到正视图坐标，只需要通过flip x来翻转
                        var flippedVerts = verts.Select(v => {
                            v.x *= -1;
                            return v;
                        }).ToList();

                        if (verts.EqualList(flippedVerts))
                        {
                            var newSymmetricFaceDetail = new HorizontalFaceDetails();
                            newSymmetricFaceDetail.Symmetric = true;
                            newSymmetricFaceDetail.Connector = hConnectorId;
                            HFaceAndCounter newHFaceAndCounter = new HFaceAndCounter(newSymmetricFaceDetail, 0);
                            connectorHorizontal.Add(verts, newHFaceAndCounter);

                            var hFaceAndCounter = connectorHorizontal[verts];
                            CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, hFaceDetail[i]);
                            hFaceAndCounter.counter++;
                        }
                        else
                        {
                            // 添加当前面作为新的connector
                            var newFaceDetail = new HorizontalFaceDetails();
                            newFaceDetail.Connector = hConnectorId;
                            var newHFaceAndCounter = new HFaceAndCounter(newFaceDetail, 0);
                            connectorHorizontal.Add(verts, newHFaceAndCounter);
                            
                            // 添加当前面的flip后作为新的connector
                            var newFlippedFaceDetail = new HorizontalFaceDetails();
                            newFlippedFaceDetail.Flipped = true;
                            newFlippedFaceDetail.Connector = hConnectorId;
                            var newHFaceAndCoutnerFlipped = new HFaceAndCounter(newFlippedFaceDetail, 0);
                            connectorHorizontal.Add(flippedVerts, newHFaceAndCoutnerFlipped);

                            var hFaceAndCounter = connectorHorizontal[verts];
                            CopyToHorizontalFaceDetail(hFaceAndCounter.hFaceDetail, hFaceDetail[i]);
                            hFaceAndCounter.counter++;
                        }
                        hConnectorId++;
                    }
                }
            }

        }

        // --------------------------------------------Vertical-------------------------------
        var connectorVertical = new Dictionary<List<Vector3>, VFaceAndCounter>();


        emptyVFaceAndCounter.counter++;
        connectorVertical.Add(new List<Vector3>(), emptyVFaceAndCounter); 
        vConnectorId++;

        foreach (var mp in modulePrototypes)
        {
            var meshFilter = mp.GetComponent<MeshFilter>();

            // set empty vertical face
            if (meshFilter == null)
            {
                var keys = connectorVertical.Keys.ToList();
                for (int i = 0; i < keys.Count; i++)
                {
                    var faceAndCounter = connectorVertical[keys[i]];
                    var emptyList = new List<Vector3>();
                    if (emptyList.EqualList(keys[i]))
                    {
                        CopyToVerticalFaceDetail(faceAndCounter.vFaceDetail, mp.Up);
                        CopyToVerticalFaceDetail(faceAndCounter.vFaceDetail, mp.Down);
                        faceAndCounter.counter += 2;
                    }
                }

                continue;
            }

            var allVerts = meshFilter.sharedMesh.vertices;
            var vertsOnFace = new Dictionary<FaceType, List<Vector3>>
            {
                {FaceType.Down, new List<Vector3>()}, 
                {FaceType.Up, new List<Vector3>()},
            };

            foreach (var vertex in allVerts)
            {
                var vertexFixed = vertex.FixedVector3(digit);
                
                RoundVertexOnFace(vertex.y, AbstractMap.BLOCK_SIZE / 2, FaceType.Up, vertexFixed, vertsOnFace);
                RoundVertexOnFace(vertex.y, -AbstractMap.BLOCK_SIZE / 2, FaceType.Down, vertexFixed, vertsOnFace);
            }

            var vFaceDetail = new List<VerticalFaceDetails> {mp.Down, mp.Up};
            var vFaceType = new List<FaceType> {FaceType.Down, FaceType.Up};

            for (int i = 0; i < vFaceType.Count; ++i)
            {
                var verts = vertsOnFace[vFaceType[i]];

                
                // move top face to bottom for matching
                if (vFaceType[i] == FaceType.Up)
                {
                    verts = verts.Select(v =>
                    {
                        v.y -= 2;
                        return v;
                    }).ToList();
                }

                List<List<Vector3>> vertRotList = new List<List<Vector3>>();
                for (int j = 0; j < 4; ++j)
                {
                    List<Vector3> newVertRot = verts.Select(v =>
                    {
                        var r = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 90 * j, 0), Vector3.one) * new Vector4(v.x, v.y, v.z, 1);
                        return new Vector3(r.x, r.y, r.z).FixedVector3(digit);
                    }).ToList();
                            
                    vertRotList.Add(newVertRot);
                }

                if (verts.Count == 0)
                {
                    var keys = connectorVertical.Keys.ToList();
                    
                    // empty face
                    for (int j = 0; j < keys.Count; ++j)
                    {
                        var faceAndCounter = connectorVertical[keys[j]];
                        if (verts.EqualList(keys[j]))
                        {
                            CopyToVerticalFaceDetail(faceAndCounter.vFaceDetail, vFaceDetail[i]);
                            faceAndCounter.counter++;
                        }
                    }
                }
                else
                {
                    bool connectorFound = false;

                    // 1.check existance
                    var keys = connectorVertical.Keys.ToList();

                    for (int j = 0; j < 4; ++j)
                    {
                        for (int k = 0; k < keys.Count; ++k)
                        {
                            var faceAndCounter = connectorVertical[keys[k]];
                            if (vertRotList[j].EqualList(keys[k]))
                            {
                                CopyToVerticalFaceDetail(faceAndCounter.vFaceDetail, vFaceDetail[i]);
                                faceAndCounter.vFaceDetail.Rotation = j;
                                faceAndCounter.counter++;
                                connectorFound = true;
                                break;
                            }
                        }

                        if (connectorFound)
                        {
                            break;
                        }
                    }


                    // 2. check invariant if it has not appeared before
                    if (connectorFound == false)
                    { 
                        // 2.1 the face is invariant if rotated verts is the same as original verts,
                        if (vertRotList[0].EqualList(vertRotList[1]))
                        {
                            VerticalFaceDetails newVFaceDetail = new VerticalFaceDetails();
                            newVFaceDetail.Invariant = true;
                            newVFaceDetail.Rotation = 0;
                            newVFaceDetail.Connector = vConnectorId;
                            
                            VFaceAndCounter newFaceAndCounter = new VFaceAndCounter(newVFaceDetail, 0, vFaceType[i]);
                            CopyToVerticalFaceDetail(newVFaceDetail, vFaceDetail[i]);
                            newFaceAndCounter.counter++;
                            connectorVertical.Add(verts, newFaceAndCounter);
                        }
                        else
                        {
                            // 2.2 if this new face detail is not invariant, add it and its three other variants to vConnector list

                            VerticalFaceDetails newVFaceDetail = new VerticalFaceDetails();
                            newVFaceDetail.Invariant = false;
                            newVFaceDetail.Rotation = 0;
                            newVFaceDetail.Connector = vConnectorId;

                            VFaceAndCounter newVFaceAndCounter = new VFaceAndCounter(newVFaceDetail, 0, vFaceType[i]);
                            
                            connectorVertical.Add(verts, newVFaceAndCounter);
                            CopyToVerticalFaceDetail(newVFaceAndCounter.vFaceDetail, vFaceDetail[i]);
                            newVFaceAndCounter.counter++;
                        }

                        connectorFound = true;
                        vConnectorId++;
                    }
                }
            }
        }


        /* 替换无法连接的connector id 为空气
         {
            // 把没有别的面可以连接的水平面重设为 0s, 垂直面重设为 0i，使其可以与空气连接
            Dictionary<Tuple<int, FaceType>, int> vFaceVariantDict = CreateVerticalVariantInfoDict(connectorVertical); // Tuple<connectorId, count>

            foreach (var mp in modulePrototypes)
            {
                var meshFilter = mp.GetComponent<MeshFilter>();
                if (meshFilter == null)
                {
                    continue;
                }

                var allVerts = meshFilter.sharedMesh.vertices;
                var vertsOnFace = new Dictionary<FaceType, List<Vector3>>
                {
                    {FaceType.Down, new List<Vector3>()},
                    {FaceType.Up, new List<Vector3>()},
                    {FaceType.Forward, new List<Vector3>()},
                    {FaceType.Back, new List<Vector3>()},
                    {FaceType.Left, new List<Vector3>()},
                    {FaceType.Right, new List<Vector3>()},
                };

                foreach (var vertex in allVerts)
                {
                    var vertexFixed = vertex.FixedVector3(digit);

                    RoundVertexOnFace(vertex.y, AbstractMap.BLOCK_SIZE / 2, FaceType.Up, vertexFixed, vertsOnFace);
                    RoundVertexOnFace(vertex.y, -AbstractMap.BLOCK_SIZE / 2, FaceType.Down, vertexFixed, vertsOnFace);
                    RoundVertexOnFace(vertex.x, -AbstractMap.BLOCK_SIZE / 2, FaceType.Left, vertexFixed, vertsOnFace);
                    RoundVertexOnFace(vertex.z, -AbstractMap.BLOCK_SIZE / 2, FaceType.Back, vertexFixed, vertsOnFace);
                    RoundVertexOnFace(vertex.x, AbstractMap.BLOCK_SIZE / 2, FaceType.Right, vertexFixed, vertsOnFace);
                    RoundVertexOnFace(vertex.z, AbstractMap.BLOCK_SIZE / 2, FaceType.Forward, vertexFixed, vertsOnFace);
                }

                var vFaceDetail = new List<VerticalFaceDetails> {mp.Down, mp.Up};
                var vFaceType = new List<FaceType> {FaceType.Down, FaceType.Up};
                var hFaceDetails = new List<HorizontalFaceDetails>() {mp.Left, mp.Right, mp.Forward, mp.Back};
                var hFaceTypes = new List<FaceType>() {FaceType.Left, FaceType.Right, FaceType.Forward, FaceType.Back};

                for (int i = 0; i < 2; ++i)
                {
                    foreach (var vConnector in connectorVertical)
                    {
                        var verts = vertsOnFace[vFaceType[i]];

                        // move top face to bottom for matching
                        if (vFaceType[i] == FaceType.Up)
                        {
                            verts = verts.Select(v =>
                            {
                                v.y -= 2;
                                return v;
                            }).ToList();
                        }

                        if (verts.EqualList(vConnector.Key))
                        {
                            var currFaceInfo = vConnector.Value;
                            if (currFaceInfo.vFaceDetail.Invariant == false)
                            {
                                var otherFaceType = currFaceInfo.faceType == FaceType.Up ? FaceType.Down : FaceType.Up;
                                var faceInfoKey = new Tuple<int, FaceType>(currFaceInfo.vFaceDetail.Connector, otherFaceType);

                                if (!vFaceVariantDict.ContainsKey(faceInfoKey))
                                {
                                    //CopyToVerticalFaceDetail(emptyVFaceAndCounter.vFaceDetail, vFaceDetail[i]);
                                }
                            }
                        }
                    }
                }

                var hKeyList = connectorHorizontal.Keys.ToList();
                var hRotToFront = new Dictionary<FaceType, float> {{FaceType.Forward, 0}, {FaceType.Left, 90}, {FaceType.Back, 180}, {FaceType.Right, 270}};
                for (int i = 0; i < 4; ++i)
                {
                    for (int j = 0; j < hKeyList.Count; ++j)
                    {
                        // rotate each horizontal faces to FrontView Coordinate (正视图)
                        var verts = vertsOnFace[hFaceTypes[i]].Select(v =>
                        {
                            var r = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, hRotToFront[hFaceTypes[i]], 0), Vector3.one) *
                                    new Vector4(v.x, v.y, v.z, 1);
                            return new Vector3(r.x, r.y, r.z).FixedVector3(digit);
                        }).ToList();

                        if (verts.EqualList(hKeyList[j]))
                        {
                            var hFaceAndCounter = connectorHorizontal[hKeyList[j]];
                            bool isUnflipped = hFaceAndCounter.hFaceDetail.Flipped == false;
                            bool isAsymmetric = hFaceAndCounter.hFaceDetail.Symmetric == false;
                            if (isUnflipped && isAsymmetric)
                            {
                                if (connectorHorizontal[hKeyList[j + 1]].counter == 0)
                                {
                                    //CopyToHorizontalFaceDetail(emptyHFaceAndCounter.hFaceDetail, hFaceDetails[i]);
                                }
                            }
                        }
                    }
                }
            }
        }
        */
    }

    [Button("Reset All Face Details")]
    public void ResetAllFaceDetails()
    {
        var modulePrototypes = gameObject.transform.GetComponentsInChildren<ModulePrototype>();

        foreach (var mp in modulePrototypes)
        {
            mp.Forward.ResetConnector();
            mp.Back.ResetConnector();
            mp.Right.ResetConnector();
            mp.Left.ResetConnector();
            mp.Up.ResetConnector();
            mp.Down.ResetConnector();
        }
    }

    private void CopyToHorizontalFaceDetail(HorizontalFaceDetails source, HorizontalFaceDetails dest)
    {
        dest.Symmetric = source.Symmetric;
        dest.Flipped = source.Flipped;
        dest.Connector = source.Connector;
    }

    private void CopyToVerticalFaceDetail(VerticalFaceDetails source, VerticalFaceDetails dest)
    {
        dest.Invariant = source.Invariant;
        dest.Rotation = source.Rotation;
        dest.Connector = source.Connector;
    }

    private Dictionary<Tuple<int, FaceType>, int> CreateVerticalVariantInfoDict(Dictionary<List<Vector3>, VFaceAndCounter> connectorVertical)
    {
        Dictionary<Tuple<int, FaceType>, int> vFaceVariantDict = new Dictionary<Tuple<int, FaceType>, int>();
        
        foreach (var vConnector in connectorVertical)
        {
            var faceDetail = vConnector.Value.vFaceDetail;
            var faceType = vConnector.Value.faceType;
            
            if (faceDetail.Invariant == true) 
            {
                continue;
            }
            
            Tuple<int, FaceType> newElement = new Tuple<int, FaceType>(faceDetail.Connector, faceType);

            if (vFaceVariantDict.ContainsKey(newElement))
            {
                vFaceVariantDict[newElement]++;
            }
            else
            {
                vFaceVariantDict.Add(newElement, 1);
            }
        }

        return vFaceVariantDict;
    }

    public bool InDeviation(float axis, float target) {
        return Util.Round(axis) == target;
    }

    private void RoundVertexOnFace(float vertexElement, float size, FaceType faceType, Vector3 vertexFixed, Dictionary<FaceType, List<Vector3>> vertsOnFace)
    {
        if (InDeviation(vertexElement, size)) {
            if (!vertsOnFace[faceType].Contains(vertexFixed))
            {
                vertsOnFace[faceType].Add(vertexFixed);
            }
        }

    }

    public void ResetPrototype(ModulePrototype mp) {
        //空气块
        mp.Spawn = false;
        mp.Up.Invariant = true;
        mp.Down.Invariant = true;
        mp.Left.Symmetric = true;
        mp.Right.Symmetric = true;
        mp.Forward.Symmetric = true;
        mp.Back.Symmetric = true;

        mp.Up.Connector = 0;
        mp.Down.Connector = 0;
        mp.Left.Connector = 0;
        mp.Right.Connector = 0;
        mp.Forward.Connector = 0;
        mp.Back.Connector = 0;
    }

    public void OnDrawGizmos() {
        foreach (Transform t in transform) {
            if (Selection.activeGameObject) {
                if (gGameObject != Selection.activeGameObject) {
                    gGameObject = Selection.activeGameObject;
                    var mp = Selection.activeGameObject.GetComponent<ModulePrototype>();
                    if (mp) {
                        var meshFilter = mp.GetComponent<MeshFilter>();
                        if (meshFilter) {
                            var mesh = meshFilter.sharedMesh;
                            var verts = mesh.vertices;
                            var vertsGroup = new Dictionary<FaceType, List<Vector3>> {
                                {FaceType.Left, new List<Vector3>()},
                                {FaceType.Down, new List<Vector3>()},
                                {FaceType.Back, new List<Vector3>()},
                                {FaceType.Right, new List<Vector3>()},
                                {FaceType.Up, new List<Vector3>()},
                                {FaceType.Forward, new List<Vector3>()},
                            };

                            foreach (var v in verts) {
                                if (InDeviation(v.x, -AbstractMap.BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Left].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.y, -AbstractMap.BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Down].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.z, -AbstractMap.BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Back].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.x, AbstractMap.BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Right].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.y, AbstractMap.BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Up].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.z, AbstractMap.BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Forward].Add(v.FixedVector3(digit));
                                }
                            }


                            gVertsGroup = vertsGroup;
                        }
                        else {
                            gVertsGroup.Clear();
                        }
                    }
                }

                if (Selection.activeGameObject == gGameObject) {
                    var cubeSize = new Vector3(vertexSize, vertexSize, vertexSize);
                    foreach (var g in gVertsGroup) {
                        if (g.Key == FaceType.Left) {
                            foreach (var v in g.Value) {
                                Gizmos.color = new Color(Color.green.r, Color.green.g, Color.green.b, 0.5f);
                                Gizmos.DrawSphere(gGameObject.transform.TransformPoint(v), vertexSize);
                            }
                        }

                        if (g.Key == FaceType.Down) {
                            foreach (var v in g.Value) {
                                Gizmos.color = new Color(Color.blue.r, Color.blue.g, Color.blue.b, 0.5f);
                                Gizmos.DrawWireSphere(gGameObject.transform.TransformPoint(v), vertexSize);
                            }
                        }

                        if (g.Key == FaceType.Back) {
                            foreach (var v in g.Value) {
                                Gizmos.color = new Color(Color.magenta.r, Color.magenta.g, Color.magenta.b, 0.5f);
                                Gizmos.DrawCube(gGameObject.transform.TransformPoint(v), cubeSize);
                            }
                        }

                        if (g.Key == FaceType.Right) {
                            foreach (var v in g.Value) {
                                Gizmos.color = new Color(Color.yellow.r, Color.yellow.g, Color.yellow.b, 0.5f);
                                Gizmos.DrawSphere(gGameObject.transform.TransformPoint(v), vertexSize);
                            }
                        }

                        if (g.Key == FaceType.Up) {
                            foreach (var v in g.Value) {
                                Gizmos.color = new Color(Color.white.r, Color.white.g, Color.white.b, 0.5f);
                                Gizmos.DrawWireSphere(gGameObject.transform.TransformPoint(v), vertexSize);
                            }
                        }

                        if (g.Key == FaceType.Forward) {
                            foreach (var v in g.Value) {
                                Gizmos.color = new Color(Color.black.r, Color.black.g, Color.black.b, 0.5f);
                                Gizmos.DrawCube(gGameObject.transform.TransformPoint(v), cubeSize);
                            }
                        }
                    }
                }
            }
        }
    }
#endif
}