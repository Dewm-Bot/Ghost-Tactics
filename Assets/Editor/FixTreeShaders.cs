using UnityEngine;
using UnityEditor;
using System.IO;

public static class FixTreeShaders
{
    [MenuItem("Tools/Fix Tree Creator Shaders")]
    static void FixShaders()
    {
        // Find the custom shaders
        Shader leavesShader = Shader.Find("Nature/Tree Creator Leaves");
        Shader barkShader = Shader.Find("Nature/Tree Creator Bark");

        if (leavesShader == null || barkShader == null)
        {
            Debug.LogError("Could not find custom Tree Creator shaders. Make sure they are imported.");
            return;
        }

        Debug.Log("Found custom shaders:");
        Debug.Log("Leaves: " + leavesShader.name);
        Debug.Log("Bark: " + barkShader.name);

        // Find all tree prefabs
        string[] prefabPaths = new string[]
        {
            "Assets/Imported/NatureStarterKit2/Nature/tree01.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/tree02.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/tree03.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/tree04.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/bush01.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/bush02.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/bush03.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/bush04.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/bush05.prefab",
            "Assets/Imported/NatureStarterKit2/Nature/bush06.prefab"
        };

        int fixedCount = 0;

        foreach (string path in prefabPaths)
        {
            if (!File.Exists(path))
            {
                Debug.LogWarning("Prefab not found: " + path);
                continue;
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("Could not load prefab: " + path);
                continue;
            }

            // Get all materials in the prefab
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat == null) continue;

                    string matName = mat.name.ToLower();
                    
                    // Fix leaf materials
                    if (matName.Contains("leaf"))
                    {
                        if (mat.shader.name != "Nature/Tree Creator Leaves")
                        {
                            Debug.Log($"Updating {mat.name} in {path} to use custom Leaves shader");
                            mat.shader = leavesShader;
                            fixedCount++;
                        }
                    }
                    // Fix bark materials
                    else if (matName.Contains("bark") || matName.Contains("optimized bark"))
                    {
                        if (mat.shader.name != "Nature/Tree Creator Bark")
                        {
                            Debug.Log($"Updating {mat.name} in {path} to use custom Bark shader");
                            mat.shader = barkShader;
                            fixedCount++;
                        }
                    }
                }
            }

            EditorUtility.SetDirty(prefab);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Fixed {fixedCount} materials to use custom Tree Creator shaders.");
    }
}

