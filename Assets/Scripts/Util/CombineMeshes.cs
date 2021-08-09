using System;
using UnityEngine;
using System.Collections;
using System.Linq;
using System.Collections.Generic;

public class CombineMeshes : MonoBehaviour {

    public bool CombineOnStart = true;
    private void Start() {
        if (CombineOnStart) CombineMeshProcess();
    }

    public bool CombineMeshProcess() {
        var mfChildren = GetComponentsInChildren<MeshFilter>();
        if (mfChildren.Length == 0) return false;
        
        var mrChildren = GetComponentsInChildren<MeshRenderer>();

        var mrSelf = gameObject.GetComponent<MeshRenderer>();
        var mfSelf = gameObject.GetComponent<MeshFilter>();

        if (!mrSelf || !mfSelf) {
            mrSelf = gameObject.AddComponent<MeshRenderer>();
            mfSelf = gameObject.AddComponent<MeshFilter>();
        }

        var combineMats = new List<Material>();
        foreach (var render in mrChildren) {
            if(render.transform == transform)
                continue;
            var localMats = render.sharedMaterials;
            foreach (var mat in localMats) {
                if (!combineMats.Contains(mat)) {
                    combineMats.Add(mat);
                }
            }
        }
        
        //提取submesh
        var subMeshs = new List<Mesh>();
        foreach (var material in combineMats) {
            var combines = new List<CombineInstance>();
            for (int i = 0; i < mfChildren.Length; i++) {
                if (mfChildren[i].transform == transform) {
                    continue;
                }
                var localMats = mfChildren[i].GetComponent<MeshRenderer>().sharedMaterials;
                for (int j = 0; j < localMats.Length; j++) {
                    if (localMats[j] != material) {
                        continue;
                    }
                    var ci = new CombineInstance();
                    ci.mesh = mfChildren[i].sharedMesh;
                    ci.transform = mfChildren[i].transform.localToWorldMatrix;
                    ci.subMeshIndex = j;
                    mrChildren[i].enabled = false;
                    combines.Add(ci);
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
        mrSelf.sharedMaterials = combineMats.ToArray();
        mrSelf.enabled = true;
        
        return true;
    }
}