using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;
using System.Linq;
using UnityEditor.ProjectWindowCallback;

// The requirement is that the class name is the same as the script name to properly find a path to templates
namespace OnibiStudios
{
    public class ScriptTemplates
    {
        // Folder with script templates relative to this script file
        private const string TemplatesFolder = "Templates";
        // Folder with icons relative to this script file
        private const string IconsFolder = "Icons";
        // Path to the package
        private static string packagePath;

        [DidReloadScripts]
        private static void FindPath()
        {
            //AssetDatabase.GetAssetPath()
            // Check if the Package is in the Project
            if(AssetDatabase.IsValidFolder("Packages/com.onibistudios.templatescripts/Editor"))
            {
                packagePath = "Packages/com.onibistudios.templatescripts/Editor";
                return;
            }

            // Otherwise, the file is in Assets/ and needs to be searched for manually

            // Get this class name using reflection
            string className = MethodBase.GetCurrentMethod().DeclaringType.Name;
            // Search for all assets with this class name
            string[] guids = AssetDatabase.FindAssets(className);

            // If found no files
            if(guids.Length == 0)
            {
                ErrorMatchName(className);
                return;
            }

            // Search for files with the exact name
            List<string> foundFiles = new List<string>();

            foreach(string guid in guids)
            {
                string[] items = AssetDatabase.GUIDToAssetPath(guid).Split(Path.DirectorySeparatorChar);
                if(items[items.Length - 1] == className + ".cs")
                    foundFiles.Add(guid);
            }

            // Found no files with the matching name
            if(foundFiles.Count == 0)
            {
                ErrorMatchName(className);
                return;
            }

            // Found one file with exactly the same name
            else if(foundFiles.Count == 1)
            {
                // Assign path
                packagePath = AssetDatabase.GUIDToAssetPath(foundFiles[0]);
                // Remove file name
                packagePath = packagePath.Substring(0, packagePath.Length - packagePath.Split(Path.DirectorySeparatorChar).Last().Length - 1);
            }

            // Found more than one file with the same name
            else if(foundFiles.Count > 1)
            {
                ErrorMultipleNames(className);
                return;
            }
        }

        private static void ErrorMatchName(string className) =>
            Debug.LogError("The class name " + className + " and its file name must match.");

        private static void ErrorMultipleNames(string className) =>
            Debug.LogError("File name for the class " + className +
                " must be unique and must match the class name.");

        private static void ErrorNoTemplate(string templateName) =>
            Debug.LogError("Template " + templateName + " does not exist.");

        private static void ErrorNoIcon(string templateName) =>
            Debug.LogWarning("Icon for template " + templateName + " does not exist.");

        [MenuItem("Assets/Create/Templates/System", false, 60)]
        private static void CreateSystem() => CreateScriptAsset("System.cs.txt", "Gear.png", "System.cs");

        [MenuItem("Assets/Create/Templates/Component", false, 61)]
        private static void CreateComponent() => CreateScriptAsset("Component.cs.txt", "Volumes.png", "Component.cs");

        [MenuItem("Assets/Create/Templates/Tag", false, 62)]
        private static void CreateTag() => CreateScriptAsset("Tag.cs.txt", "Tag.png", "Tag.cs");

        [MenuItem("Assets/Create/Templates/Buffer Element", false, 63)]
        private static void CreateBufferElement() => CreateScriptAsset("BufferElement.cs.txt", "Stack.png", "Element.cs");

        [MenuItem("Assets/Create/Templates/Blob Asset", false, 64)]
        private static void CreateBlobAsset() => CreateScriptAsset("BlobAsset.cs.txt", "Bolt.png", "BlobAsset.cs");

        [MenuItem("Assets/Create/Templates/Extensions", false, 65)]
        private static void CreateExtensions() => CreateScriptAsset("Extensions.cs.txt", "Puzzle.png", "Extensions.cs");
        
        private static void CreateScriptAsset(string templateName, string iconName, string scriptName)
        {
            // Set template path
            char separator = Path.DirectorySeparatorChar;
            string templatePath = packagePath + separator +
                TemplatesFolder + separator + templateName;

            // Check if template exists
            if(!File.Exists(templatePath))
            {
                // Reassign path
                FindPath();
                // Template not found
                if(!File.Exists(templatePath))
                {
                    ErrorNoTemplate(templateName);
                    return;
                }
            }

            // Load icon
            Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(
                packagePath + separator + IconsFolder + separator + iconName);
            if(!icon)
                ErrorNoIcon(iconName);
            
            CreateScriptAction action = ScriptableObject.CreateInstance<CreateScriptAction>();
            action.icon = icon;
            // Handle name editing of a newly created script 
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                0, action, GetCurrentAssetDirectory() + separator + scriptName, icon, templatePath);
        }

        private class CreateScriptAction : EndNameEditAction
        {
            public Texture2D icon;

            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                // Create script
                string text = File.ReadAllText(resourceFile);
                text = text.Replace("#SCRIPTNAME#", Path.GetFileNameWithoutExtension(pathName));
                File.WriteAllText(pathName, text);
                AssetDatabase.Refresh();

                // Change icon
                MonoScript asset = AssetDatabase.LoadAssetAtPath<MonoScript>(pathName);
                MethodInfo SetIconForObject = typeof(EditorGUIUtility).
                    GetMethod("SetIconForObject", BindingFlags.Static | BindingFlags.NonPublic);
                MethodInfo CopyMonoScriptIconToImporters = typeof(MonoImporter).
                    GetMethod("CopyMonoScriptIconToImporters", BindingFlags.Static | BindingFlags.NonPublic);
                SetIconForObject.Invoke(null, new object[]{ asset, icon });
                CopyMonoScriptIconToImporters.Invoke(null, new object[] {asset});
            }
        }

        public static string GetCurrentAssetDirectory()
        {
            foreach (Object obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (Directory.Exists(path))
                    return path;
                else if (File.Exists(path))
                    return Path.GetDirectoryName(path);
            }
            
            /*Type projectWindowUtilType = typeof(ProjectWindowUtil);
            MethodInfo getActiveFolderPath = projectWindowUtilType.GetMethod(
                "GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            object obj = getActiveFolderPath.Invoke(null, new object[0]);
            string pathToCurrentFolder = obj.ToString();
            Debug.Log(pathToCurrentFolder);*/

            return "Assets";
        }

        public static string GetSelectedPathOrFallback()
        {
            string path = "Assets";
            
            foreach (Object obj in Selection.GetFiltered<Object>(SelectionMode.Assets))
            {
                path = AssetDatabase.GetAssetPath(obj);
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) 
                {
                    path = Path.GetDirectoryName(path);
                    break;
                }
            }
            return path;
        }
    }
}