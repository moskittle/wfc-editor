using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;

using FaceDetails = ModulePrototype.FaceDetails;
using FaceType =  ModuleAnalysis.FaceType;
using NeighborsIndexOnAllFaces = System.Collections.Generic.List<CustomModuleData.NeighborIndexListWrapper>;

[CreateAssetMenu(menuName = "Wave Function Collapse/Custom Module Data", fileName = "CustomModuleData.asset")]
public class CustomModuleData : ScriptableObject, ISerializationCallbackReceiver
{
    // Serialize nested lists: https://answers.unity.com/questions/289692/serialize-nested-lists.html
    [Serializable]
    public class NeighborIndexListWrapper
    {
        public List<int> neighborIndexOnOneFace = new List<int>();

        public void Add(int index)
        {
            neighborIndexOnOneFace.Add(index);
        }
    }
    
    [Serializable]
    public class PrototypeInfo
    {
        public string name;
        public int index;
        public Mesh mesh;
        public int rotation;
        public List<FaceDetails> faceDetails; 
        public NeighborsIndexOnAllFaces neighbors;
        public List<FaceDetails> constraintTo;
        public List<FaceDetails> constraintFrom;
        public int weight = 1;

        // Constructors
        public PrototypeInfo(){}
        public PrototypeInfo(string _name, int _index, Mesh _mesh, int _rotation, List<FaceDetails> _faceDetails,
            NeighborsIndexOnAllFaces _neighbors, List<FaceDetails> _constraintTo = null,
            List<FaceDetails> _constraintFrom = null, int _weight = 1)
        {
            name = _name;
            index = _index;
            mesh = _mesh;
            rotation = _rotation;
            faceDetails = _faceDetails;
            neighbors = _neighbors;
            constraintTo = _constraintTo;
            constraintFrom = _constraintFrom;
            weight = _weight;
        }

        public PrototypeInfo(PrototypeInfo other)
        {
            name = other.name;
            index = other.index;
            mesh = other.mesh;
            rotation = other.rotation;
            faceDetails = other.faceDetails;
            neighbors = other.neighbors;
            constraintTo = other.constraintTo;
            constraintFrom = other.constraintFrom;
            weight = other.weight;
        }
    }
    
    public GameObject prototypes;
    
    private List<FaceType> faceTypes = new List<FaceType>
    {
        FaceType.Left, FaceType.Back, FaceType.Right, FaceType.Forward, FaceType.Down, FaceType.Up
    };

    public List<PrototypeInfo> modules;
    
    
#if UNITY_EDITOR
    [Button("Create Module Prototypes")]
    public void CreateModulePrototypes()
    {
        List<PrototypeInfo> prototypeMetaData = new List<PrototypeInfo>();
        
        if (prototypes == null)
        {
            Debug.Log("No prototype created.");
            return;
        }

        var allPrototypes = prototypes.GetComponentsInChildren<ModulePrototype>().ToList();

        int index = 0;
        foreach (var mp in allPrototypes)
        {
            var meshFilter = mp.GetComponent<MeshFilter>();
            Mesh mesh = meshFilter ? meshFilter.sharedMesh : null;
            int weight = 1;

            var faceDetails = new List<FaceDetails> { mp.Left, mp.Back, mp.Right, mp.Forward, mp.Down, mp.Up};    // order is important

            // add prototypes with rotation variants to array
            for (int rotation = 0; rotation < 4; rotation++)
            {
                if (rotation == 0 || !mp.CompareRotatedVariants(0, rotation))
                {
                    var name = mp.gameObject.name + "_" + rotation;

                    var newFaceDetails = new List<FaceDetails>();
                    for (int i = 0; i < faceDetails.Count; ++i)
                    {
                        var faceDetail = i < 4 ? faceDetails[(i + rotation) % 4] : faceDetails[i];
                        var newFaceDetail = new FaceDetails(faceDetail);
                        newFaceDetails.Add(newFaceDetail);
                    }
                    
                    if (!newFaceDetails[4].Invariant)
                    {
                        newFaceDetails[4].Rotation = (faceDetails[4].Rotation + rotation) % 4;
                    }
                    
                    if (!newFaceDetails[5].Invariant)
                    {
                        newFaceDetails[5].Rotation = (faceDetails[5].Rotation + rotation) % 4;
                    }
                    
                    // var neighbors = new NeighborsIndexOnAllFaces();
                    var neighbors = CreateNeighborsOnAllFaces(newFaceDetails, allPrototypes);

                    // TODO: implement extra constraints
                    List<FaceDetails> constraintsTo = new List<FaceDetails>();
                    List<FaceDetails> constraintsFrom = new List<FaceDetails>();

                    var prototypeInfo = new PrototypeInfo(name, index++, mesh, rotation, newFaceDetails, neighbors, constraintsTo, constraintsFrom, weight);

                    prototypeMetaData.Add(prototypeInfo);
                }


            }
        }
        
        this.modules = prototypeMetaData;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    public NeighborsIndexOnAllFaces CreateNeighborsOnAllFaces(List<FaceDetails> faceDetails, List<ModulePrototype> allPrototypes)
    {
        NeighborsIndexOnAllFaces neighbors = new NeighborsIndexOnAllFaces(6);

        for (int i = 0; i < faceDetails.Count; ++i)
        {
            var currentFace = faceDetails[i];
            var neighborsIndexOnFace = new NeighborIndexListWrapper();

            for (int otherProtoIndex = 0; otherProtoIndex < allPrototypes.Count; ++otherProtoIndex)
            {
                var otherPrototype =  allPrototypes[otherProtoIndex];
                List<FaceDetails> otherFaceDetails = new List<FaceDetails>
                { 
                    new FaceDetails(otherPrototype.Left), 
                    new FaceDetails(otherPrototype.Back), 
                    new FaceDetails(otherPrototype.Right), 
                    new FaceDetails(otherPrototype.Forward), 
                    new FaceDetails(otherPrototype.Down), 
                    new FaceDetails(otherPrototype.Up), 
                };

                FaceDetails otherFace = new FaceDetails();
                // compare opposing faces: Left <=> Right, Back <=> Up, Down <=> Forward
                if (i < 4)
                {
                    otherFace = otherFaceDetails[(i + 2) % 4];
                }
                else if (i == 4)
                {
                    otherFace = otherFaceDetails[5];
                }
                else if (i == 5)
                {
                    otherFace = otherFaceDetails[4];
                }

                bool isValid = ValidateNeighbor(currentFace, otherFace);

                if (isValid)
                {
                    neighborsIndexOnFace.Add(otherProtoIndex);
                }
            }

            // if (neighborsIndexOnFace.neighborIndexOnOneFace.Count == 0)
            // {
            //     neighborsIndexOnFace.neighborIndexOnOneFace = new List<int> { 0 };    // add air
            // }
            
            neighbors.Add(neighborsIndexOnFace);
        }

        return neighbors;
    }

    private bool ValidateNeighbor(FaceDetails currentFace, FaceDetails otherFace)
    {
        if (currentFace.Connector == otherFace.Connector)
        {
            bool symmetricMatch = currentFace.Symmetric && otherFace.Symmetric;
            bool asymmetricMatch = (currentFace.Flipped && !otherFace.Flipped) || (!currentFace.Flipped && otherFace.Flipped);
            bool invariantMatch = currentFace.Invariant && otherFace.Invariant;
            bool variantMatch = (!currentFace.Invariant && !otherFace.Invariant) && currentFace.Rotation == otherFace.Rotation;
            
            if (symmetricMatch || asymmetricMatch || invariantMatch || variantMatch)
            {
                return true;
            }
        }

        // if (currentFace.Connector == 0 && otherFace.Connector == 0 && currentFace.Invariant && otherFace.Invariant)
        // {
        //     return true;
        // }
        // else if (currentFace.Flipped)
        // {
        //     if (currentFace.Connector == otherFace.Connector && otherFace.Flipped == false)
        //     {
        //         return true;
        //     }
        // }
        // else if (otherFace.Flipped)
        // {
        //     if (currentFace.Connector == otherFace.Connector && currentFace.Flipped == false)
        //     {
        //         return true;
        //     }
        // }
        // else if (currentFace.Connector == otherFace.Connector)
        // {
        //     if ((currentFace.Symmetric && otherFace.Symmetric) || (currentFace.Invariant && otherFace.Invariant)
        //                                                        || (!currentFace.Invariant && !otherFace.Invariant && currentFace.Rotation == otherFace.Rotation))
        //     {
        //         return true;
        //     }
        // }

        return false;
    }

#endif    // UNITY_EDITOR

    public void OnBeforeSerialize()
    {
    }

    public void OnAfterDeserialize()
    {
    }
}
