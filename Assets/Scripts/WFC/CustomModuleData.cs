using System;
using System.Collections.Generic;
using System.Linq;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;
using FaceDetails = ModulePrototype.FaceDetails;
using FaceType = ModuleAnalysis.FaceType;
using NeighborsIndexOnAllFaces = System.Collections.Generic.List<CustomModuleData.NeighborIndexListWrapper>;

[CreateAssetMenu(menuName = "Wave Function Collapse/Custom Module Data", fileName = "CustomModuleData.asset")]
public class CustomModuleData : ScriptableObject, ISerializationCallbackReceiver {
    // Serialize nested lists: https://answers.unity.com/questions/289692/serialize-nested-lists.html
    [Serializable]
    public class NeighborIndexListWrapper {
        public List<int> neighborIndexOnOneFace = new List<int>();

        public NeighborIndexListWrapper() {
        }

        public NeighborIndexListWrapper(NeighborIndexListWrapper other) {
            neighborIndexOnOneFace = other.neighborIndexOnOneFace.ToList();
        }

        public void Add(int index) {
            neighborIndexOnOneFace.Add(index);
        }
    }

    [Serializable]
    public class PrototypeInfo {
        public string name;
        public int index;
        public string mesh;
        public int rotation;
        public List<FaceDetails> faceDetails;
        public NeighborsIndexOnAllFaces neighbors;
        public float probability = 1;
        public List<string> constraintToTags = new List<string>();
        public List<string> constraintFromTags = new List<string>();

        // Constructors
        public PrototypeInfo() {
        }

        public PrototypeInfo(string _name, int _index, string _mesh, int _rotation, List<FaceDetails> _faceDetails = null,
            NeighborsIndexOnAllFaces _neighbors = null, float _probability = 1, List<string> _constraintToTags = null,
            List<string> _constraintFromTags = null) {
            name = _name;
            index = _index;
            mesh = _mesh;
            rotation = _rotation;
            faceDetails = _faceDetails;
            neighbors = _neighbors;
            probability = _probability;
            constraintToTags = _constraintToTags;
            constraintFromTags = _constraintFromTags;
        }

        public PrototypeInfo(PrototypeInfo other) {
            name = other.name;
            index = other.index;
            mesh = other.mesh;
            rotation = other.rotation;
            faceDetails = other.faceDetails.Select(otherFaceDetail => new FaceDetails(otherFaceDetail)).ToList();
            neighbors = other.neighbors.Select(otherNeighbor => new NeighborIndexListWrapper(otherNeighbor)).ToList();
            probability = other.probability;
            constraintToTags = other.constraintToTags;
            constraintFromTags = other.constraintFromTags;
        }
    }

    public GameObject prototypes;

    private List<FaceType> faceTypes = new List<FaceType> {
        FaceType.Left,
        FaceType.Back,
        FaceType.Right,
        FaceType.Forward,
        FaceType.Down,
        FaceType.Up
    };

    public List<PrototypeInfo> modules;


#if UNITY_EDITOR
    [Button("Create Module Prototypes")]
    public void CreateModulePrototypes() {
        List<PrototypeInfo> prototypeMetaData = new List<PrototypeInfo>();

        if (prototypes == null) {
            Debug.Log("No prototype created.");
            return;
        }

        var allPrototypes = prototypes.GetComponentsInChildren<ModulePrototype>().ToList();

        int index = 0;
        foreach (var mp in allPrototypes) {
            var mesh = mp.name;
            float probability = mp.Probability;


            // add prototypes with rotation variants to array
            for (int rotation = 0; rotation < 4; rotation++) {
                if (rotation == 0 || !mp.CompareRotatedVariants(0, rotation)) {
                    var name = mp.gameObject.name + "_" + rotation;

                    var faceDetails = new List<FaceDetails> {
                        mp.Left,
                        mp.Back,
                        mp.Right,
                        mp.Forward,
                        mp.Down,
                        mp.Up
                    }; // order is important

                    // temp initilization
                    var newFaceDetails = new List<FaceDetails>();
                    for (int i = 0; i < faceDetails.Count; ++i) {
                        var faceDetail = i < 4 ? faceDetails[(i + rotation) % 4] : faceDetails[i];
                        var newFaceDetail = new FaceDetails(faceDetail);
                        newFaceDetails.Add(newFaceDetail);
                    }

                    if (!newFaceDetails[4].Invariant) {
                        newFaceDetails[4].Rotation = (faceDetails[4].Rotation + rotation) % 4;
                    }

                    if (!newFaceDetails[5].Invariant) {
                        newFaceDetails[5].Rotation = (faceDetails[5].Rotation + rotation) % 4;
                    }

                    var neighbors = new NeighborsIndexOnAllFaces();

                    List<string> constraintToTags = mp.constraintToTags;
                    List<string> constraintFromTags = mp.constraintFromTags;

                    var prototypeInfo = new PrototypeInfo(name, index++, mesh, rotation, newFaceDetails, neighbors, probability, constraintToTags,
                        constraintFromTags);

                    prototypeMetaData.Add(prototypeInfo);
                }
            }
        }

        // seperate loop to add face details and neighbor details (after all variants are added to modules)
        foreach (var prototypeInfo in prototypeMetaData) {
            prototypeInfo.neighbors = CreateNeighborsOnAllFaces(prototypeInfo.faceDetails, prototypeMetaData);
        }

        this.modules = prototypeMetaData;
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
    }

    public NeighborsIndexOnAllFaces CreateNeighborsOnAllFaces(List<FaceDetails> faceDetails, List<PrototypeInfo> prototypeMetaData) {
        NeighborsIndexOnAllFaces neighbors = new NeighborsIndexOnAllFaces(6);

        for (int currFaceIndex = 0; currFaceIndex < faceDetails.Count; ++currFaceIndex) {
            var currentFace = faceDetails[currFaceIndex];
            var neighborsIndexOnFace = new NeighborIndexListWrapper();

            for (int otherProtoIndex = 0; otherProtoIndex < prototypeMetaData.Count; ++otherProtoIndex) {
                var otherPrototype = prototypeMetaData[otherProtoIndex];
                List<FaceDetails> otherFaceDetails = new List<FaceDetails> {
                    new FaceDetails(otherPrototype.faceDetails[0]), // Left
                    new FaceDetails(otherPrototype.faceDetails[1]), // Back
                    new FaceDetails(otherPrototype.faceDetails[2]), // Right
                    new FaceDetails(otherPrototype.faceDetails[3]), // Forward
                    new FaceDetails(otherPrototype.faceDetails[4]), // Down
                    new FaceDetails(otherPrototype.faceDetails[5]), // Up
                };

                FaceDetails otherFace = new FaceDetails();
                // compare opposing faces: Left <=> Right, Back <=> Up, Down <=> Forward
                if (currFaceIndex < 4) {
                    otherFace = otherFaceDetails[(currFaceIndex + 2) % 4];
                }
                else if (currFaceIndex == 4) {
                    otherFace = otherFaceDetails[5];
                }
                else if (currFaceIndex == 5) {
                    otherFace = otherFaceDetails[4];
                }

                bool isValid = ValidateNeighbor(currentFace, otherFace, currFaceIndex);

                if (isValid) {
                    neighborsIndexOnFace.Add(otherProtoIndex);
                }
            }

            neighbors.Add(neighborsIndexOnFace);
        }

        return neighbors;
    }

    private bool ValidateNeighbor(FaceDetails currentFace, FaceDetails otherFace, int currFaceIndex) {
        if (currentFace.Connector == otherFace.Connector) {
            // Horizontal face
            if (currFaceIndex < 4) {
                bool symmetricMatch = currentFace.Symmetric && otherFace.Symmetric;
                bool asymmetricMatch = (currentFace.Flipped && !otherFace.Flipped) || (!currentFace.Flipped && otherFace.Flipped);

                if (symmetricMatch || asymmetricMatch) {
                    return true;
                }
            }
            // Vertical face
            else {
                bool invariantMatch = currentFace.Invariant && otherFace.Invariant;
                bool variantMatch = (!currentFace.Invariant && !otherFace.Invariant) && currentFace.Rotation == otherFace.Rotation;

                if (invariantMatch || variantMatch) {
                    return true;
                }
            }
        }

        return false;
    }

#endif // UNITY_EDITOR

    public void OnBeforeSerialize() {
    }

    public void OnAfterDeserialize() {
    }
}