using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// This window enhances searching in the Hierarchy view.
    /// </summary>
    public class HierarchyFinderWindow : EditorWindow
    {
        private class ButtonTexts
        {
            public const string Ping = "Ping";
            public const string Paste = "Paste";
            public const string Delete = "-";
        }

        private const string PrefsKey = "HierarchyFinderWindow";

        // List to store input strings
        [SerializeField]
        private List<string> _inputFields = new();

        // Scroll position
        private Vector2 _scrollPosition;

        // Implement a sortable list using ReorderableList
        private ReorderableList _reorderableList;

        // Restore internal class for serialization/deserialization with JsonUtility
        [Serializable]
        private class StringList
        {
            public List<string> items = new();
        }

        // Add menu item
        [MenuItem("Tools/Hierarchy Finder", false, 1000)]
        public static void ShowWindow()
        {
            // Get existing window or create a new one
            GetWindow<HierarchyFinderWindow>("Hierarchy Finder");
        }

        private void OnGUI()
        {
            // Handle drag and drop
            HandleDragAndDrop();

            EditorGUILayout.Space(2);

            // Show help box only if the list is empty
            if (_inputFields.Count == 0)
            {
                EditorGUILayout.HelpBox("Drag and drop to automatically register paths.\n" +
                                        "If it starts with \"t:\" or contains *?, a popup will be displayed with \"Search\".\n" +
                                        "If it starts with \"t:\" and does not contain *?, enter it in the search window with \"paste\".", MessageType.Info);
                EditorGUILayout.Space(2);
            }

            // Start scroll view
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // Draw ReorderableList
            _reorderableList.DoLayoutList();

            // End scroll view
            EditorGUILayout.EndScrollView();
        }

        private void OnEnable()
        {
            // Load saved list
            LoadSavedPaths();

            // Initialize ReorderableList
            InitializeReorderableList();
        }

        private void OnDisable()
        {
            // Save list when the window is closed
            SavePaths();
        }

        // Load saved paths
        private void LoadSavedPaths()
        {
            // Load JSON string from EditorUserSettings
            string json = EditorUserSettings.GetConfigValue(PrefsKey);
            if (!string.IsNullOrEmpty(json))
            {
                // Deserialize JSON to list
                StringList list = JsonUtility.FromJson<StringList>(json);
                if (list != null && list.items != null)
                {
                    _inputFields = list.items;
                }
                else
                {
                    // Initialize with an empty list if deserialization fails
                    _inputFields = new List<string>();
                }
            }
            else
            {
                // Initialize with an empty list if there is no saved value
                _inputFields = new List<string>();
            }
        }

        // Save path list
        private void SavePaths()
        {
            // Serialize list to JSON string
            StringList list = new StringList { items = _inputFields };
            string json = JsonUtility.ToJson(list);

            // Save JSON string to EditorUserSettings
            EditorUserSettings.SetConfigValue(PrefsKey, json);
        }

        private void OnDrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _inputFields.Count) return;

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            string fieldValue = _inputFields[index];
            // Does it contain "t:"?
            bool containsT = !string.IsNullOrEmpty(fieldValue) && fieldValue.StartsWith("t:", StringComparison.OrdinalIgnoreCase);
            // Does it contain Glob characters (* or ?)?
            bool containsGlob = !string.IsNullOrEmpty(fieldValue) && (fieldValue.Contains("*") || fieldValue.Contains("?"));

            // スラッシュを含まない単純な文字列も検索対象とするかどうかのフラグ
            bool isSimpleNameQuery = !containsT && !containsGlob && !string.IsNullOrEmpty(fieldValue) && !fieldValue.Contains("/");

            // 検索アイコンを表示する条件を変更: "t:"、Glob、または単純な名前クエリの場合
            bool showSearchIcon = containsT || containsGlob || isSimpleNameQuery;
            // Pasteボタンの条件を変更: "t:"(Glob無) または 単純な名前検索 の場合に True
            bool showPasteButton = (containsT && !containsGlob) || isSimpleNameQuery;
            // Pingボタンの条件を変更: "t:"、Glob、単純な名前クエリのいずれでも *なく*、かつスラッシュを含む（＝フルパス指定）の場合のみ
            bool showPingButton = !showSearchIcon && !showPasteButton && !string.IsNullOrEmpty(fieldValue) && fieldValue.Contains("/");

            // Horizontal layout adjustment
            float magnifyIconButtonWidth = 25; // Width of the search icon
            float buttonWidth = 50; // Width of Ping or Paste button
            float deleteButtonWidth = 20; // Width of delete button
            float spacing = 4;

            float actionsWidth = 0; // Initialize total width of action buttons

            if (showSearchIcon)
            {
                actionsWidth += magnifyIconButtonWidth + spacing;
            }

            if (showPasteButton)
            {
                actionsWidth += buttonWidth + spacing;
            }

            if (showPingButton)
            {
                actionsWidth += buttonWidth + spacing;
            }

            // Subtract the last spacing only if actionsWidth is not 0
            if (actionsWidth > 0)
            {
                actionsWidth -= spacing;
            }

            float fieldWidth = rect.width - (actionsWidth + deleteButtonWidth + 8); // 8 is for some margin on left/right

            // Text field
            Rect textFieldRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            string previousValue = _inputFields[index];
            string newValue = EditorGUI.TextField(textFieldRect, previousValue);

            if (newValue != previousValue)
            {
                Undo.RecordObject(this, "Modify Input Field");
                _inputFields[index] = newValue;
                SavePaths(); // Save changes when text is modified
            }

            float currentX = rect.x + fieldWidth + spacing;

            // Button drawing logic (also simplified based on flags)
            if (showSearchIcon)
            {
                Rect searchButtonRect = new Rect(currentX, rect.y, magnifyIconButtonWidth, rect.height);
                Texture2D magnifyIcon = EditorGUIUtility.FindTexture("Search Icon");
                if (GUI.Button(searchButtonRect, new GUIContent(magnifyIcon)))
                {
                    SearchObjectsAndShowPopup(fieldValue, searchButtonRect);
                    GUIUtility.ExitGUI();
                }

                currentX += magnifyIconButtonWidth + spacing;
            }

            if (showPasteButton)
            {
                Rect pasteButtonRect = new Rect(currentX, rect.y, buttonWidth, rect.height);
                if (GUI.Button(pasteButtonRect, ButtonTexts.Paste))
                {
                    SetHierarchySearchFilter(fieldValue);
                }

                currentX += buttonWidth + spacing;
            }

            if (showPingButton)
            {
                Rect pingButtonRect = new Rect(currentX, rect.y, buttonWidth, rect.height);
                if (GUI.Button(pingButtonRect, ButtonTexts.Ping))
                {
                    PingObject(index, pingButtonRect);
                }
                // No need to update currentX as Ping button is the last one
            }

            // Delete button (no change)
            Rect deleteButtonRect = new Rect(rect.x + rect.width - deleteButtonWidth, rect.y, deleteButtonWidth, rect.height);
            GUIStyle deleteButtonStyle = new GUIStyle(EditorStyles.miniButton);
            deleteButtonStyle.alignment = TextAnchor.MiddleCenter;

            if (GUI.Button(deleteButtonRect, ButtonTexts.Delete, deleteButtonStyle))
            {
                Undo.RecordObject(this, "Remove Input Field");
                _inputFields.RemoveAt(index);
                SavePaths();
                Repaint();
            }
        }

        // Initialize ReorderableList
        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(_inputFields, typeof(string), true, false, true, false);
            _reorderableList.drawElementCallback = OnDrawElementCallback;
            _reorderableList.headerHeight = 0;
            _reorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 6;

            // Process for adding new elements
            _reorderableList.onAddCallback = (ReorderableList list) =>
            {
                _inputFields.Add("");
                SavePaths();
            };

            _reorderableList.onReorderCallback = (ReorderableList list) => { SavePaths(); };
        }

        // Search and Ping GameObject from a normal path
        private void PingObject(int index, Rect buttonRect = default)
        {
            if (index < 0 || index >= _inputFields.Count) return;

            string fieldValue = _inputFields[index];
            GameObject targetObject = GameObject.Find(fieldValue);
            if (targetObject != null)
            {
                // Ping (highlight) the GameObject
                EditorGUIUtility.PingObject(targetObject);

                // Select the GameObject (show in Inspector)
                Selection.activeGameObject = targetObject;
                return;
            }

            // Show popup if GameObject is not found
            SearchResultPopupWindow.Show(buttonRect, new List<HierarchySearchLogic.SearchResult>(), null, "");
        }

        // Execute search with string starting with "t:" or Glob pattern and show popup
        private void SearchObjectsAndShowPopup(string searchQuery, Rect buttonRect)
        {
            // Changed to receive result list from SearchObjects
            List<HierarchySearchLogic.SearchResult> searchResults = HierarchySearchLogic.SearchObjects(searchQuery);

            // Branch processing based on the number of search results!
            if (searchResults.Count == 1)
            {
                // If there's one result, ping and select it directly
                HierarchySearchLogic.SearchResult singleResult = searchResults[0];
                EditorGUIUtility.PingObject(singleResult.gameObject);
                Selection.activeGameObject = singleResult.gameObject;
            }
            else
            {
                // If there are 0 or multiple results, show the popup as before
                SearchResultPopupWindow.Show(buttonRect, searchResults, (selectedObject) =>
                {
                    // Ping and highlight the object
                    EditorGUIUtility.PingObject(selectedObject);

                    // Select the object
                    Selection.activeGameObject = selectedObject;
                }, searchQuery);
            }
        }

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            if (evt.type != EventType.DragUpdated && evt.type != EventType.DragPerform)
            {
                return;
            }

            // Change cursor to indicate that dragged objects can be accepted
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                // Complete drag and drop operation
                DragAndDrop.AcceptDrag();

                // Process dropped objects
                foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                {
                    GameObject go = draggedObject as GameObject;
                    if (go != null)
                    {
                        // Get Hierarchy path of the GameObject
                        string hierarchyPath = HierarchySearchLogic.GetGameObjectPath(go);

                        // Add new field
                        _inputFields.Add(hierarchyPath);
                        SavePaths();
                        Repaint();
                    }
                }
            }

            evt.Use();
        }

        // Method to set the search string in the Hierarchy search window
        private void SetHierarchySearchFilter(string searchString)
        {
            // Get instance of SceneHierarchyWindow
            Type sceneHierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            if (sceneHierarchyWindowType == null)
            {
                Debug.LogError("UnityEditor.SceneHierarchyWindow type not found.");
                return;
            }

            PropertyInfo lastInteractedHierarchyWindowProperty = sceneHierarchyWindowType.GetProperty("lastInteractedHierarchyWindow", BindingFlags.Public | BindingFlags.Static);
            if (lastInteractedHierarchyWindowProperty == null)
            {
                Debug.LogError("lastInteractedHierarchyWindow property not found.");
                return;
            }

            EditorWindow hierarchyWindow = lastInteractedHierarchyWindowProperty.GetValue(null) as EditorWindow;

            if (hierarchyWindow == null)
            {
                // The Hierarchy window might not be open, so keep it as a warning.
                Debug.LogWarning("Hierarchy window not found. Please click the Hierarchy window once and try again.");
                return; // Interrupt processing instead of error
            }

            // Get SearchableEditorWindow type (parent class of SceneHierarchyWindow)
            Type searchableEditorWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow");
            if (searchableEditorWindowType == null)
            {
                Debug.LogError("SearchableEditorWindow type not found");
                return;
            }

            // Get SetSearchFilter method using reflection
            // Try the overload with 4 arguments (string, SearchMode, bool, bool)
            MethodInfo setSearchFilterMethod = null;
            try
            {
                // Get the type of the SearchMode enum
                Type searchModeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow+SearchMode");
                if (searchModeType != null)
                {
                    setSearchFilterMethod = searchableEditorWindowType.GetMethod("SetSearchFilter",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                        null, // Binder
                        new Type[] { typeof(string), searchModeType, typeof(bool), typeof(bool) }, // Specify argument types
                        null); // ParameterModifier
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to get SetSearchFilter (4 args): {e.Message}");
                setSearchFilterMethod = null; // Acquisition failed
            }


            if (setSearchFilterMethod != null)
            {
                // Get the value of the SearchMode enum for the 4-argument version (All)
                Type searchModeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow+SearchMode");
                object searchModeAll = Enum.Parse(searchModeType, "All");

                // Execute the method with 4 arguments
                setSearchFilterMethod.Invoke(hierarchyWindow,
                    new object[] { searchString, searchModeAll, true, false });
            }
            else
            {
                // If the method with 4 arguments is not found, try the overload with 3 arguments (string, bool, bool)
                setSearchFilterMethod = searchableEditorWindowType.GetMethod("SetSearchFilter",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(string), typeof(bool), typeof(bool) },
                    null);

                if (setSearchFilterMethod == null)
                {
                    Debug.LogError("SetSearchFilter method not found.");
                    return;
                }

                // Execute the method with 3 arguments
                setSearchFilterMethod.Invoke(hierarchyWindow,
                    new object[] { searchString, true, false });
            }


            // Redraw the window
            hierarchyWindow.Repaint();
        }
    }
}