using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;

using HorizontalFaceDetails = ModulePrototype.HorizontalFaceDetails;
using VerticalFaceDetails = ModulePrototype.VerticalFaceDetails;


public class ModuleAnalysis : MonoBehaviour {
    public const float BLOCK_SIZE = 4f;
    public enum FaceType {
        Up,
        Down,
        Left,
        Right,
        Forward,
        Back
    }
#if UNITY_EDITOR
    public int digit = 4;
    public float vertexSize = 0.03f;
    private GameObject gGameObject = null;
    private Dictionary<FaceType, List<Vector3>> gVertsGroup = new Dictionary<FaceType, List<Vector3>>();


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
        if (modulePrototypes.Length == 0)
        {
            Debug.LogError("No module prototype component processed!");
            return;
        }

        // add empty horizontal/vertical faces to dictionary when initialized
        var hConnectorId = 0;
        var connectorHorizontal = new Dictionary<List<Vector3>, HorizontalFaceDetails>();
        var hEmptyFace = new HorizontalFaceDetails();
        hEmptyFace.Symmetric = true;
        hEmptyFace.Connector = hConnectorId;
        connectorHorizontal.Add(new List<Vector3>(), hEmptyFace);
        hConnectorId++;
        
        int vConnectorId = 0;
        var vEmptyFace = new VerticalFaceDetails();
        vEmptyFace.Invariant = true;
        vEmptyFace.Connector = vConnectorId;
        var connectorVertical = new Dictionary<List<Vector3>, VerticalFaceDetails>();
        connectorVertical.Add(new List<Vector3>(), vEmptyFace); 
        vConnectorId++;
        
        var hFaceTypes = new List<FaceType> {FaceType.Left, FaceType.Back, FaceType.Right, FaceType.Forward};
        var vFaceTypes = new List<FaceType> {FaceType.Down, FaceType.Up};

        foreach (var mp in modulePrototypes)
        {
            var hFaceDetailMp = new List<HorizontalFaceDetails> {mp.Left, mp.Back, mp.Right, mp.Forward};
            var vFaceDetailMp = new List<VerticalFaceDetails> {mp.Down, mp.Up};

            var meshFilter = mp.GetComponent<MeshFilter>();

            // process air prototype
            if (meshFilter == null)
            {
                // empty horizontal face (air prototype)
                foreach (var hFaceDetail in hFaceDetailMp)
                {
                    CopyToHorizontalFaceDetail(hEmptyFace, hFaceDetail);
                }

                // empty vertical face (air prototype)
                foreach (var vFaceDetail in vFaceDetailMp)
                {
                    CopyToVerticalFaceDetail(vEmptyFace, vFaceDetail);
                }

                continue;
            }
            
            var allVerts = meshFilter.sharedMesh.vertices;

            var vertsOnHorizontalFace = new Dictionary<FaceType, List<Vector3>>
            {
                {FaceType.Left, new List<Vector3>()},
                {FaceType.Right, new List<Vector3>()},
                {FaceType.Back, new List<Vector3>()},
                {FaceType.Forward, new List<Vector3>()},
            };

            var vertsOnVerticalFaces = new Dictionary<FaceType, List<Vector3>>
            {
                {FaceType.Down, new List<Vector3>()},
                {FaceType.Up, new List<Vector3>()},
            };
            
            foreach (var vertex in allVerts)
            {
                var vertexFixed = vertex.FixedVector3(digit);

                RoundVertexOnFace(vertex.x, -BLOCK_SIZE / 2, FaceType.Left, vertexFixed, vertsOnHorizontalFace);
                RoundVertexOnFace(vertex.x, BLOCK_SIZE / 2, FaceType.Right, vertexFixed, vertsOnHorizontalFace);
                RoundVertexOnFace(vertex.z, -BLOCK_SIZE / 2, FaceType.Back, vertexFixed, vertsOnHorizontalFace);
                RoundVertexOnFace(vertex.z, BLOCK_SIZE / 2, FaceType.Forward, vertexFixed, vertsOnHorizontalFace);
                
                RoundVertexOnFace(vertex.y, -BLOCK_SIZE / 2, FaceType.Down, vertexFixed, vertsOnVerticalFaces);
                RoundVertexOnFace(vertex.y, BLOCK_SIZE / 2, FaceType.Up, vertexFixed, vertsOnVerticalFaces);
            }

            // --------------------------------------------Horizontal-------------------------------
            ProcessHorizontalFaces(hFaceTypes, connectorHorizontal, vertsOnHorizontalFace, hFaceDetailMp, ref hConnectorId, hEmptyFace);
            
            // --------------------------------------------Vertical-------------------------------
            ProcessVerticalFaces(vFaceTypes, connectorVertical, vertsOnVerticalFaces, vFaceDetailMp, ref vConnectorId, vEmptyFace);
        }
    }

    private void ProcessHorizontalFaces(List<FaceType> hFaceTypes, Dictionary<List<Vector3>, HorizontalFaceDetails> connectorHorizontal, 
        Dictionary<FaceType, List<Vector3>> vertsOnFace, List<HorizontalFaceDetails> hFaceDetailMp, ref int hConnectorId, HorizontalFaceDetails hEmptyFace)
    {
            var hRotToFront = new Dictionary<FaceType, float> { {FaceType.Forward, 0}, {FaceType.Left, 90}, {FaceType.Back, 180}, {FaceType.Right, 270} };

            // horizontal faces: 0, 1, 2, 3
            for (int i = 0; i < 4; ++i)
            {
                var verts = vertsOnFace[hFaceTypes[i]];

                // empty horizontal face
                if (verts.Count == 0)
                {
                    CopyToHorizontalFaceDetail(hEmptyFace, hFaceDetailMp[i]);
                }
                else
                {
                    bool connectorFound = false;
                    
                    // rotate each horizontal faces to FrontView Coordinate (正视图)
                    verts = verts.Select(v => {
                        var r = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, hRotToFront[hFaceTypes[i]], 0), Vector3.one) * new Vector4(v.x, v.y, v.z, 1);
                        return new Vector3(r.x, r.y, r.z).FixedVector3(digit);
                    }).ToList();

                    // check if there already exists a matching connector
                    foreach (var hConnector in connectorHorizontal)
                    {
                        if (verts.EqualList(hConnector.Key))
                        {
                            var hFaceDetail = hConnector.Value;
                            CopyToHorizontalFaceDetail(hFaceDetail, hFaceDetailMp[i]);
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
                            connectorHorizontal.Add(verts, newSymmetricFaceDetail);

                            var hFaceDetail = connectorHorizontal[verts];
                            CopyToHorizontalFaceDetail(hFaceDetail, hFaceDetailMp[i]);
                        }
                        else
                        {
                            // 添加当前面作为新的connector
                            var newFaceDetail = new HorizontalFaceDetails();
                            newFaceDetail.Connector = hConnectorId;
                            connectorHorizontal.Add(verts, newFaceDetail);
                            
                            // 添加当前面的flip后作为新的connector
                            var newFlippedFaceDetail = new HorizontalFaceDetails();
                            newFlippedFaceDetail.Flipped = true;
                            newFlippedFaceDetail.Connector = hConnectorId;
                            connectorHorizontal.Add(flippedVerts, newFlippedFaceDetail);

                            var hFaceDetail = connectorHorizontal[verts];
                            CopyToHorizontalFaceDetail(hFaceDetail, hFaceDetailMp[i]);
                        }
                        hConnectorId++;
                    }
                }
            }
    }
    
    void ProcessVerticalFaces(List<FaceType> vFaceTypes,Dictionary<List<Vector3>, VerticalFaceDetails> connectorVertical,
        Dictionary<FaceType, List<Vector3>> vertsOnFace, List<VerticalFaceDetails> vFaceDetailMp, ref int vConnectorId, VerticalFaceDetails vEmptyFace)
    {
        // vertical faces: 4, 5
        for (int faceIndex = 0; faceIndex < vFaceTypes.Count; ++faceIndex)
        {
            var verts = vertsOnFace[vFaceTypes[faceIndex]];

            // move top face to bottom for matching
            if (vFaceTypes[faceIndex] == FaceType.Up)
            {
                verts = verts.Select(v =>
                {
                    v.y -= 2;
                    return v;
                }).ToList();
            }

            // calculate verts for 4 rotations
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

            // empty vertical face
            if (verts.Count == 0)
            {
                CopyToVerticalFaceDetail(vEmptyFace, vFaceDetailMp[faceIndex]);
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
                        var vFaceDetail = connectorVertical[keys[k]];
                        if (vertRotList[j].EqualList(keys[k]))
                        {
                            CopyToVerticalFaceDetail(vFaceDetail, vFaceDetailMp[faceIndex]);
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
                        connectorVertical.Add(vertRotList[0], newVFaceDetail);
                        
                        CopyToVerticalFaceDetail(newVFaceDetail, vFaceDetailMp[faceIndex]);
                    }
                    else
                    {
                        // 2.2 if this new face detail is not invariant, add it and its three other variants to vConnector list

                        // add all variants to the ConnectorVertical
                        for (int rotIndex = 0; rotIndex < 4; ++rotIndex)
                        {
                            VerticalFaceDetails newVFaceDetail = new VerticalFaceDetails();
                            newVFaceDetail.Invariant = false;
                            newVFaceDetail.Rotation = rotIndex;
                            newVFaceDetail.Connector = vConnectorId;
                            connectorVertical.Add(vertRotList[rotIndex], newVFaceDetail);

                            // assign the first variant to current face
                            if (rotIndex == 0)
                            {
                                CopyToVerticalFaceDetail(newVFaceDetail, vFaceDetailMp[faceIndex]);
                            }
                        }
                    }

                    connectorFound = true;
                    vConnectorId++;
                }
            }
        }
    }

    [Button("Reset All Face Details")]
    public void ResetAllFaceDetails()
    {
        var modulePrototypes = gameObject.transform.GetComponentsInChildren<ModulePrototype>();

        foreach (var mp in modulePrototypes)
        {
            // not virtual functions, need to be called individually
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
                                if (InDeviation(v.x, -BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Left].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.y, -BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Down].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.z, -BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Back].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.x, BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Right].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.y, BLOCK_SIZE / 2)) {
                                    vertsGroup[FaceType.Up].Add(v.FixedVector3(digit));
                                }

                                if (InDeviation(v.z, BLOCK_SIZE / 2)) {
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