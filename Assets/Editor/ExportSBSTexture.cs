using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ExportSBSTexture : EditorWindow
{
    [MenuItem("Window/ExportSBSTexture")]
    static void Init() {
        ExportSBSTexture window = (ExportSBSTexture)EditorWindow.GetWindow(typeof(ExportSBSTexture));
        window.Show();
    }

    Texture diffuse;
    Texture height;

    RenderTexture rtD;
    RenderTexture rtH;

    Texture2D texD;
    Texture2D texH;

    void OnGUI() {
        diffuse = (Texture)EditorGUILayout.ObjectField("Diffuse", diffuse, typeof(Texture),false);
        height = (Texture)EditorGUILayout.ObjectField("Height", height, typeof(Texture), false);


        //EditorGUILayout.ObjectField("Diffuse", rtD, typeof(Texture), false);
        //EditorGUILayout.ObjectField("Height", rtH, typeof(Texture), false);
        //
        //
        //EditorGUILayout.ObjectField("Diffuse", texD, typeof(Texture), false);
        //EditorGUILayout.ObjectField("Height", texH, typeof(Texture), false);

        if (GUILayout.Button("Export")) {
            string filePath = EditorUtility.SaveFilePanel("select block file", "", "", "png");

            rtD = new RenderTexture(diffuse.width, diffuse.height, 24, RenderTextureFormat.ARGB32);
            rtH = new RenderTexture(diffuse.width, diffuse.height, 24, RenderTextureFormat.ARGB32);

            rtD.DiscardContents();
            rtH.DiscardContents();

            RenderTexture.active = rtD;
            GL.Clear(true, true, new Color(0, 0, 0, 0));
            RenderTexture.active = rtH;
            GL.Clear(true, true, new Color(0, 0, 0, 0));

            Graphics.Blit(diffuse, rtD);
            Graphics.Blit(height, rtH);

            texD = new Texture2D(diffuse.width, diffuse.height);
            texH = new Texture2D(diffuse.width, diffuse.height);

            RenderTexture.active = rtD;
            texD.ReadPixels(new Rect(0, 0, diffuse.width, diffuse.height),0,0);
            texD.Apply();
            RenderTexture.active = rtH;
            texH.ReadPixels(new Rect(0, 0, diffuse.width, diffuse.height), 0, 0);
            texH.Apply();

            Texture2D tex = new Texture2D(diffuse.width, diffuse.height);
            Color[] diffusePixels = texD.GetPixels();
            Color[] heightPixels = texH.GetPixels();
            Color[] colors = tex.GetPixels();
            for(int i =0; i<colors.Length; i++) {
                colors[i] = new Color(diffusePixels[i].r, diffusePixels[i].g, diffusePixels[i].b, heightPixels[i].r);
            }
            tex.SetPixels(colors);
            byte[] bytes = tex.EncodeToPNG();
            
            System.IO.File.WriteAllBytes(filePath, bytes);
        }
    }
}
