using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

public class QuickSceneSwitcher : EditorWindow
{
    private List<SceneAsset> scenes;
    private List<string> hiddenScenes;
    private bool editMode = false;

    private Vector2 scrollPosition = Vector2.zero;
    private GUILayoutOption heightLayout;

    private long nextLoad = 0;

    // Prefix used to store preferences
    // Format is: QuickSceneSwitcher/DefaultCompany/ProjectName/
    private string PREFS_PREFIX;

    // Initialization
    void OnEnable()
    {
        PREFS_PREFIX = $"QuickSceneSwitcher/{Application.companyName}/{Application.productName}/";

        // Style vars
        heightLayout = GUILayout.Height(22);

        // Load preferences and scenes when this window is opened
        LoadPrefs();
    }

    [MenuItem("Window/Quick Scene Switcher")]
    public static void ShowWindow()
    {
        GetWindow<QuickSceneSwitcher>("Scene Switcher");
    }

    void OnGUI()
    {
        if (DateTime.Now.ToFileTimeUtc() > nextLoad) LoadPrefs();
        
        // Buttons Style
        GUIStyle buttonStyleBold = new GUIStyle(GUI.skin.button);
        buttonStyleBold.fixedHeight = 22;
        buttonStyleBold.fontStyle = FontStyle.Bold;

        // Title label style
        GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontStyle = FontStyle.Bold;
        labelStyle.fixedHeight = 22;
        labelStyle.fontSize = 14;

        Color originalContentColor = GUI.contentColor;

        // Scrollbar handling
        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        // Label 'Quick Scenes'
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        GUILayout.Label(" Quick Scene Switcher", labelStyle);
        GUILayout.FlexibleSpace();

        if (Application.isPlaying)
        {
            editMode = false;
            GUI.enabled = false;
        }
        
        // Toggle 'EditMode' Button
        // GUI.backgroundColor = editMode ? GetButtonColorBasedOnEditorTheme(Color.white) : darkGray;
        // UpdateButtonStyleTextColor(GUI.backgroundColor, buttonStyleBold);
        editMode = GUILayout.Toggle(editMode, editMode ? " Exit Edit Mode " : " Edit Mode ", buttonStyleBold);
        
        GUI.backgroundColor = originalContentColor;
        buttonStyleBold.normal.textColor = GUI.skin.button.normal.textColor;
        buttonStyleBold.hover.textColor = GUI.skin.button.hover.textColor;
        buttonStyleBold.focused.textColor = GUI.skin.button.focused.textColor;
        buttonStyleBold.active.textColor = GUI.skin.button.active.textColor;
        
        GUILayout.EndHorizontal();
        GUILayout.Space(3);

        bool needsSave = false;
        
        if (Application.isPlaying)
        {
            GUILayout.Space(2);
            GUILayout.Button("This tool is not usable in play mode.", GUILayout.Height(30), GUILayout.MinWidth(10));
            GUI.enabled = true;
        }
        else
        {
            string lastCategory = "";
            
            // Draw each scene button (or each scene options if in 'EditMode')
            for (int i = 0; i < scenes.Count; i++)
            {
                string guid = AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(scenes[i])).ToString();
                bool hidden = hiddenScenes.Contains(guid);

                string category = GetSceneCategory(scenes[i]);
                
                if (editMode)
                {
                    if (category != lastCategory)
                    {
                        if (!string.IsNullOrEmpty(lastCategory)) EditorGUILayout.Space(20);
                        EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
                        lastCategory = category;
                    }
                    
                    GUILayout.BeginHorizontal();

                    if (!hidden && !GUILayout.Toggle(true, "-", buttonStyleBold, heightLayout, GUILayout.MaxWidth(25)))
                    {
                        hiddenScenes.Add(guid);
                        needsSave = true;
                    }
                    else if (hidden && GUILayout.Toggle(false, "+", buttonStyleBold, heightLayout, GUILayout.MaxWidth(25)))
                    {
                        hiddenScenes.Remove(guid);
                        needsSave = true;
                    }

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(scenes[i], typeof(SceneAsset), false, heightLayout);
                    EditorGUI.EndDisabledGroup();

                    GUILayout.EndHorizontal();
                }
                else // if NOT in 'EditMode'
                {
                    if (scenes[i] == null) continue;
                    
                    if (hidden) continue;
                    
                    if (category != lastCategory)
                    {
                        if (!string.IsNullOrEmpty(lastCategory)) EditorGUILayout.Space(20);
                        EditorGUILayout.LabelField(category, EditorStyles.boldLabel);
                        lastCategory = category;
                    }

                    GUILayout.BeginHorizontal();
                    
                    // Disable button if it corresponds to the currently open scene
                    bool isCurrentScene = IsCurrentScene(scenes[i]);
                    
                    if (isCurrentScene)
                    {
                        EditorGUI.BeginDisabledGroup(true);
                        GUILayout.Toggle(true, "X", buttonStyleBold, heightLayout, GUILayout.MaxWidth(25));
                        EditorGUI.EndDisabledGroup();
                    }
                    else
                    {
                        if (GUILayout.Toggle(false, "O", buttonStyleBold, heightLayout, GUILayout.MaxWidth(25))) OpenScene(scenes[i]);
                    }

                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.ObjectField(scenes[i], typeof(SceneAsset), false, heightLayout);
                    EditorGUI.EndDisabledGroup();

                    GUILayout.EndHorizontal();
                }
            }

            if (!editMode)
            {
                // If NOT in 'EditMode' and there are no scenes buttons to show,
                // display an info message about how to add them
                if (scenes.Count == 0 || !(scenes.Any(i => i != null)))
                {
                    GUILayout.Space(2);
                    EditorGUI.BeginDisabledGroup(true);
                    GUILayout.Button($"There are no scenes.", GUILayout.Height(30), GUILayout.MinWidth(10));
                    EditorGUI.EndDisabledGroup();
                }
            }

            // Draw Footer
            if (editMode)
            {
                GUILayout.FlexibleSpace();
                GUIStyle footerStyle = EditorStyles.centeredGreyMiniLabel;
                Color footerColor = (EditorGUIUtility.isProSkin ? Color.white : Color.black);
                footerColor.a = 0.25f;
                footerStyle.normal.textColor = footerColor;
                GUILayout.Label("", footerStyle);
                GUILayout.Space(5);
            }
        }

        GUILayout.EndScrollView();

        // If any preferences have changed, save them to persistent data
        if (needsSave) SavePrefs();
    }

    public string GetSceneCategory(SceneAsset s)
    {
        return string.Join('/', AssetDatabase.GetAssetOrScenePath(s).Split("/").Take(2));
    }
    
    #region SceneManagement

    // Closes the current scene and opens the specified scene
    private void OpenScene(SceneAsset scene)
    {
        string scenePath = AssetDatabase.GetAssetPath(scene);

        // Check if there are unsaved changes in the current scene and
        // ask if the user wants to save them before switching to the new scene
        // If the user chooses 'cancel', don't switch scenes
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            EditorSceneManager.OpenScene(scenePath);
            SavePrefs();
        }
    }

    // Returns true if 'scene' is the scene currently open
    private bool IsCurrentScene(SceneAsset scene)
    {
        return EditorSceneManager.GetActiveScene().path == AssetDatabase.GetAssetPath(scene);
    }

    #endregion

    #region PersistentData

    private static string configPath => Path.Combine(Application.dataPath, "..", "ProjectSettings", "Packages", "QuickSceneSwitcher.txt");
    
    // Save current preferences to persistent data
    private void SavePrefs()
    {
        File.WriteAllLines(configPath, hiddenScenes.ToArray());
    }

    // Load and parse preferences from persistent saved data
    private void LoadPrefs()
    {
        if (!File.Exists(configPath)) File.WriteAllText(configPath, "");
        
        scenes = AssetDatabase.FindAssets("t:SceneAsset").Select(AssetDatabase.GUIDToAssetPath)
                              .Where(p => PackageInfo.FindForAssetPath(p) is null or { source: PackageSource.Embedded or PackageSource.Local })
                              .Select(AssetDatabase.LoadAssetAtPath<SceneAsset>)
                              .OrderBy(GetSceneCategory)
                              .ThenBy(s => s.name).ToList();
        hiddenScenes = File.ReadAllLines(configPath).ToList();

        nextLoad = DateTime.Now.AddSeconds(15).ToFileTimeUtc();
    }

    #endregion
}