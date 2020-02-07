using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Text;

public class CopyImageToReferenceFolders
{
    // Add a new menu item that is accessed by right-clicking on an asset in the project view

    [MenuItem("Assets/CopyToReferenceImages", priority = 1)]
    private static void CopyImages()
    {
        string referenceImagesPath = Path.Combine("Assets", "ReferenceImages", "Linear");

        Texture2D selected = Selection.activeObject as Texture2D;
        if (selected != null)
        {
            string pathToOriginalImage = AssetDatabase.GetAssetPath(selected);
            string extension = Path.GetExtension(pathToOriginalImage);
            string imageName = selected.name + extension;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Copied \"" + imageName + "\" to...");
            foreach (string rootfolder in EnumerateLeafFolders(referenceImagesPath))
            {
                sb.AppendLine("-> " + rootfolder);
                AssetDatabase.CopyAsset(pathToOriginalImage, Path.Combine(rootfolder, imageName));
            }
            Debug.Log(sb);
        }
    }

    private static IEnumerable<string> EnumerateLeafFolders(string root)
    {
        Stack<string> dir = new Stack<string>();
        dir.Push(root);

        while (dir.Count != 0)
        {
            bool anySubfolders = false;
            root = dir.Pop();

            foreach (var subfolder in Directory.EnumerateDirectories(root))
            {
                dir.Push(subfolder);
                anySubfolders = true;
            }

            if (!anySubfolders)
            {
                yield return root;
            }
        }
    }
}
