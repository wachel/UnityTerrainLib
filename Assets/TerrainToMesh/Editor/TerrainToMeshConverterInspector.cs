﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace TerrainConverter
{

    [CustomEditor(typeof(TerrainToMeshConverter))]
    public class TerrainToMeshConverterInspector : Editor
    {
        TerrainToMeshConverter converter;
        public void OnEnable()
        {
            converter = target as TerrainToMeshConverter;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (converter.terrain) {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("测试")) {
                    converter.bakedControlTexture = TerrainToMeshTool.BakeTextureIndex(converter.terrain.terrainData);
                }
                if (GUILayout.Button("生成分层网格")) {
                    converter.trees = new LodNodeTree[converter.maxLodLevel + 1];
                    for (int i= 0; i<=converter.maxLodLevel; i++) {
                        converter.trees[i] = new LodNodeTree();
                        float error = converter.minError * Mathf.Pow(Mathf.Pow(converter.maxError / converter.minError, 1.0f / (converter.maxLodLevel)), i);
                        Node tempNode = CreateNode(error,converter.gridSize);
                        List<byte> bytes = new List<byte>();
                        tempNode.ToBytes(bytes);
                        converter.trees[i].tree = bytes.ToArray();
                        converter.trees[i].alphaLayers = tempNode.GetAlphaBytes();
                    }

                    converter.LoadNodes();

                    converter.ClearChildren();
                    converter.CollectInfos();
                    if (converter.staticLodMesh) {
                        converter.CreateStaticMeshes();
                        converter.ClearCollectedInfo();
                    }
                    else {
                        converter.Update();
                    }
                }
                GUILayout.EndHorizontal();
            }
            EditorGUILayout.LabelField("内存", converter.GetMemorySize().ToString());
        }

        float GetHeightError(float[,] heights, int x, int y, int step,out bool swapEdge)
        {
            float p0 = heights[x, y];
            float p1 = heights[x + step, y];
            float p2 = heights[x, y + step];
            float p3 = heights[x + step, y + step];
            float center = heights[x + step / 2, y + step / 2];
            float bottom = heights[x + step / 2, y];
            float left = heights[x, y + step / 2];
            float top = heights[x + step / 2, y + step];
            float right = heights[x + step, y + step / 2];
            float error0 = Mathf.Abs(center - (p0 + p3) / 2);
            float error1 = Mathf.Abs(center - (p1 + p2) / 2);
            swapEdge = error0 < error1;

            float error = Mathf.Min(error0, error1);
            error += Mathf.Abs(bottom - (p0 + p1) / 2);
            error += Mathf.Abs(left - (p0 + p2) / 2);
            error += Mathf.Abs(top - (p2 + p3) / 2);
            error += Mathf.Abs(right - (p3 + p1) / 2);

            //起伏越大的地方，对误差容忍度越高
            float avg = p0 + p1 + p2 + p3;
            float errorScale = 0.5f / (0.5f + Mathf.Abs(p0 - avg) + Mathf.Abs(p1 - avg) + Mathf.Abs(p2 - avg) + Mathf.Abs(p3 - avg));

            return error * errorScale;
        }

        bool CheckSourrond(Node root, int x, int y, int size)
        {
            if (x - size >= 0) {
                Node node = root.FindSizeNode(x - size, y, size);
                if(node.childs != null  && (node.childs[1].childs != null || node.childs[3].childs != null)) {
                    return false;
                }
            }
            if(x + size < root.size) {
                Node node = root.FindSizeNode(x + size, y, size);
                if (node.childs != null && (node.childs[0].childs != null || node.childs[2].childs != null)) {
                    return false;
                }
            }

            if (y - size >= 0) {
                Node node = root.FindSizeNode(x, y - size, size);
                if (node.childs != null && (node.childs[2].childs != null || node.childs[3].childs != null)) {
                    return false;
                }
            }
            if (y + size < root.size) {
                Node node = root.FindSizeNode(x, y + size, size);
                if (node.childs != null && (node.childs[0].childs != null || node.childs[1].childs != null)) {
                    return false;
                }
            }
            
            return true;
        }

        //2 3
        //0 1
        void AddNode(Node root)
        {
            root.childs = new Node[4];
            int half = root.size / 2;
            root.childs[0] = new Node(root.x, root.y, half);
            root.childs[1] = new Node(root.x + half, root.y, half);
            root.childs[2] = new Node(root.x, root.y + half, half);
            root.childs[3] = new Node(root.x + half, root.y + half, half);
            if (half > 1) {
                for (int i = 0; i < 4; i++) {
                    AddNode(root.childs[i]);
                }
            }
        }

        public bool TestAlphaMap(float[,,] alphamaps, int x, int y, int size, int layer)
        {
            if (size == 0) {
                size = 1;
            }
            for (int i = -2; i < size + 2; i++) {
                for (int j = -2; j < size + 2; j++) {
                    int xx = x + i;
                    int yy = y + j;
                    if (xx >= 0 && xx < alphamaps.GetLength(0) && yy >= 0 && yy < alphamaps.GetLength(1))
                    {
                        if (alphamaps[x + i, y + j, layer] > converter.alphaMapThreshold)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        Node CreateNode(float maxError,int maxSize)
        {
            int w = converter.terrain.terrainData.heightmapWidth;
            int h = converter.terrain.terrainData.heightmapHeight;
            float[,] heights = converter.terrain.terrainData.GetHeights(0, 0, w, h);
            int aw = converter.terrain.terrainData.alphamapWidth;
            int ah = converter.terrain.terrainData.alphamapHeight;
            float[,,] alphamaps = converter.terrain.terrainData.GetAlphamaps(0, 0, aw, ah);

            Node root = new Node(0, 0, w-1);
            AddNode(root);

            //统计不透明的格子数量
            root.PostorderTraversal((Node node) => {
                node.validNums = new int[converter.terrain.terrainData.alphamapLayers];
                for (int alphaLayer = 0; alphaLayer < node.validNums.Length; alphaLayer++) {
                    if (node.size == 1) {
                        node.validNums[alphaLayer] = TestAlphaMap(alphamaps, node.x * aw / (w - 1), node.y * ah / (h - 1), aw / (w - 1), alphaLayer) ? 1 : 0;
                    } else {
                        node.validNums[alphaLayer] = node.childs[0].validNums[alphaLayer] + node.childs[1].validNums[alphaLayer] + node.childs[2].validNums[alphaLayer] + node.childs[3].validNums[alphaLayer];
                    }
                }
            });

            root.PostorderTraversal((Node node) => {
                int totalNum =0 ;
                for(int i = 0; i<node.validNums.Length; i++) {
                    totalNum += node.validNums[i];
                }
                if(totalNum < node.size * node.size) {
                    int a = 0;
                }
            });

            //合并格子
            for (int m = 1; 1 << m < w; m++) {
                int step = 1 << m;
                if (step < maxSize) {
                    root.TraversalSize(step, (Node node) => {
                        bool allChildrenIsMerged = node.childs != null && node.childs[0].childs == null && node.childs[1].childs == null && node.childs[2].childs == null && node.childs[3].childs == null;
                        if (allChildrenIsMerged) {
                            float childErrorSum = node.childs[0].error + node.childs[1].error + node.childs[2].error + node.childs[3].error;
                            float error = childErrorSum* 0.3f + GetHeightError(heights, node.x, node.y, node.size, out node.swapEdge) * converter.terrain.terrainData.size.y;
                            if (error < maxError && CheckSourrond(root, node.x, node.y, node.size)) {
                                node.error = error;
                                node.childs = null;
                            }
                        }
                    });
                }
            }

            //为了消除T接缝，如果相邻格子比自己大，则靠近大格子的两个三角形要合并为一个
            root.PreorderTraversal((Node node) => {
                if (node.childs != null) {
                    //x - 1
                    if (node.childs[0].childs == null && node.childs[2].childs == null && TerrainToMeshTool.IsSizeLeaf(root, node.x - 1, node.y, node.size)) {
                        node.mergeTriangle |= 1 << 1;
                    }
                    //y - 1
                    if (node.childs[0].childs == null && node.childs[1].childs == null && TerrainToMeshTool.IsSizeLeaf(root, node.x, node.y - 1, node.size)) {
                        node.mergeTriangle |= 1 << 0;
                    }

                    //x + 1
                    if (node.childs[1].childs == null && node.childs[3].childs == null && TerrainToMeshTool.IsSizeLeaf(root, node.x + node.size + 1, node.y, node.size)) {
                        node.mergeTriangle |= 1 << 3;
                    }
                    //y + 1
                    if (node.childs[2].childs == null && node.childs[3].childs == null && TerrainToMeshTool.IsSizeLeaf(root, node.x, node.y + node.size + 1, node.size)) {
                        node.mergeTriangle |= 1 << 2;
                    }
                }
            });

            return root;
        }

    }
}