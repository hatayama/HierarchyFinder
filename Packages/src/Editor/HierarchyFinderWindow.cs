using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// Hierarchyビューの検索を強化するウィンドウや。
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
        private const string WordWrapPrefsKey = "HierarchyFinderWindow_WordWrap";

        // 入力文字列を格納するリスト
        [SerializeField]
        private List<string> _inputFields = new();

        // スクロール位置
        private Vector2 _scrollPosition;

        // Word Wrapの有効/無効
        private bool _enableWordWrap = false;

        // ReorderableListを使用してソート可能なリストを実装
        private ReorderableList _reorderableList;

        // JsonUtilityでのシリアライズ/デシリアライズのために内部クラスを復元
        [Serializable]
        private class StringList
        {
            public List<string> items = new();
        }

        // メニュー項目を追加
        [MenuItem("Tools/Hierarchy Finder", false, 1000)]
        public static void ShowWindow()
        {
            // 既存のウィンドウを取得するか、新しいウィンドウを作成
            GetWindow<HierarchyFinderWindow>("Hierarchy Finder");
        }

        private void OnGUI()
        {
            // ドラッグアンドドロップの処理
            HandleDragAndDrop();

            EditorGUILayout.Space(2);

            // Word Wrapチェックボックス（チェックボックスを左、ラベルを右に配置）
            EditorGUILayout.BeginHorizontal();
            bool previousWordWrap = _enableWordWrap;
            _enableWordWrap = EditorGUILayout.Toggle(_enableWordWrap, GUILayout.Width(15));
            EditorGUILayout.LabelField("Word Wrap", GUILayout.Width(70));
            EditorGUILayout.EndHorizontal();
            
            if (previousWordWrap != _enableWordWrap)
            {
                SaveWordWrapSetting();
                // ReorderableListを再初期化して高さを再計算
                if (_reorderableList != null)
                {
                    InitializeReorderableList();
                }
                Repaint();
            }

            EditorGUILayout.Space(2);

            // リストが空の場合のみヘルプボックスを表示
            if (_inputFields.Count == 0)
            {
                EditorGUILayout.HelpBox("Drag and drop to automatically register paths.\n" +
                                        "If it starts with \"t:\" or contains *?, a popup will be displayed with \"Search\".\n" +
                                        "If it starts with \"t:\" and does not contain *?, enter it in the search window with \"paste\".", MessageType.Info);
                EditorGUILayout.Space(2);
            }

            // スクロールビューを開始
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // ReorderableListを描画
            _reorderableList.DoLayoutList();

            // スクロールビューを終了
            EditorGUILayout.EndScrollView();
        }

        private void OnEnable()
        {
            // 保存されたリストをロード
            LoadSavedPaths();

            // Word Wrap設定をロード
            LoadWordWrapSetting();

            // ReorderableListを初期化
            InitializeReorderableList();
        }

        private void OnDisable()
        {
            // ウィンドウが閉じられるときにリストを保存
            SavePaths();
        }

        // 保存されたパスをロード
        private void LoadSavedPaths()
        {
            // EditorUserSettingsからJSON文字列をロード
            string json = EditorUserSettings.GetConfigValue(PrefsKey);
            if (!string.IsNullOrEmpty(json))
            {
                // JSONをリストにデシリアライズ
                StringList list = JsonUtility.FromJson<StringList>(json);
                if (list != null && list.items != null)
                {
                    _inputFields = list.items;
                }
                else
                {
                    // デシリアライズに失敗した場合は空のリストで初期化
                    _inputFields = new List<string>();
                }
            }
            else
            {
                // 保存された値がない場合は空のリストで初期化
                _inputFields = new List<string>();
            }
        }

        // パスリストを保存
        private void SavePaths()
        {
            // リストをJSON文字列にシリアライズ
            StringList list = new StringList { items = _inputFields };
            string json = JsonUtility.ToJson(list);

            // JSON文字列をEditorUserSettingsに保存
            EditorUserSettings.SetConfigValue(PrefsKey, json);
        }

        // Word Wrap設定をロード
        private void LoadWordWrapSetting()
        {
            string value = EditorUserSettings.GetConfigValue(WordWrapPrefsKey);
            if (!string.IsNullOrEmpty(value))
            {
                bool.TryParse(value, out _enableWordWrap);
            }
        }

        // Word Wrap設定を保存
        private void SaveWordWrapSetting()
        {
            EditorUserSettings.SetConfigValue(WordWrapPrefsKey, _enableWordWrap.ToString());
        }

        private void OnDrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _inputFields.Count) return;

            rect.y += 2;
            rect.height -= 4;

            string fieldValue = _inputFields[index];
            // "t:" を含むか？
            bool containsT = !string.IsNullOrEmpty(fieldValue) && fieldValue.StartsWith("t:", StringComparison.OrdinalIgnoreCase);
            // Glob文字(*または?)を含むか？
            bool containsGlob = !string.IsNullOrEmpty(fieldValue) && (fieldValue.Contains("*") || fieldValue.Contains("?"));

            // スラッシュを含まない単純な文字列も検索対象とするかどうかのフラグ
            bool isSimpleNameQuery = !containsT && !containsGlob && !string.IsNullOrEmpty(fieldValue) && !fieldValue.Contains("/");

            // 検索アイコンを表示する条件を変更: "t:"、Glob、または単純な名前クエリの場合
            bool showSearchIcon = containsT || containsGlob || isSimpleNameQuery;
            // Pasteボタンの条件を変更: "t:"(Glob無) または 単純な名前検索 の場合に True
            bool showPasteButton = (containsT && !containsGlob) || isSimpleNameQuery;
            // Pingボタンの条件を変更: "t:"、Glob、単純な名前クエリのいずれでも *なく*、かつスラッシュを含む（＝フルパス指定）の場合のみ
            bool showPingButton = !showSearchIcon && !showPasteButton && !string.IsNullOrEmpty(fieldValue) && fieldValue.Contains("/");

            // 水平レイアウト調整
            float magnifyIconButtonWidth = 25; // 検索アイコンの幅
            float buttonWidth = 50; // PingまたはPasteボタンの幅
            float deleteButtonWidth = 20; // 削除ボタンの幅
            float spacing = 4;

            float actionsWidth = 0; // アクションボタンの合計幅を初期化

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

            // actionsWidthが0でない場合にのみ最後のスペーシングを減算
            if (actionsWidth > 0)
            {
                actionsWidth -= spacing;
            }

            float fieldWidth = rect.width - (actionsWidth + deleteButtonWidth + 8); // 8は左右の若干のマージンのため

            // テキストフィールドの高さを計算
            GUIStyle textFieldStyle = new GUIStyle(_enableWordWrap ? EditorStyles.textArea : EditorStyles.textField);
            textFieldStyle.alignment = _enableWordWrap ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
            textFieldStyle.wordWrap = _enableWordWrap;
            
            float actualTextHeight;
            if (_enableWordWrap)
            {
                GUIContent content = new GUIContent(fieldValue);
                float textHeight = textFieldStyle.CalcHeight(content, fieldWidth);
                actualTextHeight = Mathf.Max(EditorGUIUtility.singleLineHeight, textHeight);
            }
            else
            {
                actualTextHeight = EditorGUIUtility.singleLineHeight;
            }

            // テキストフィールド
            Rect textFieldRect = new Rect(rect.x, rect.y, fieldWidth, actualTextHeight);
            string previousValue = _inputFields[index];
            string newValue = _enableWordWrap 
                ? EditorGUI.TextArea(textFieldRect, previousValue, textFieldStyle)
                : EditorGUI.TextField(textFieldRect, previousValue, textFieldStyle);

            if (newValue != previousValue)
            {
                Undo.RecordObject(this, "Modify Input Field");
                _inputFields[index] = newValue;
                SavePaths(); // テキスト変更時に変更を保存
            }

            float currentX = rect.x + fieldWidth + spacing;

            // ボタン描画ロジック（フラグに基づいて簡略化も）
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
                // Pingボタンが最後なのでcurrentXの更新は不要
            }

            // 削除ボタン（変更なし）
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

        // ReorderableListを初期化
        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(_inputFields, typeof(string), true, false, true, false);
            _reorderableList.drawElementCallback = OnDrawElementCallback;
            _reorderableList.headerHeight = 0;
            _reorderableList.elementHeightCallback = GetElementHeight;

            // 新しい要素を追加する処理
            _reorderableList.onAddCallback = (ReorderableList list) =>
            {
                Undo.RecordObject(this, "Add Input Field");
                _inputFields.Add("");
                SavePaths();
            };

            _reorderableList.onReorderCallback = (ReorderableList list) => { SavePaths(); };
        }

        // 要素の高さを動的に計算
        private float GetElementHeight(int index)
        {
            if (index < 0 || index >= _inputFields.Count) return EditorGUIUtility.singleLineHeight + 6;

            // Word Wrapが無効の場合は固定の高さを返す
            if (!_enableWordWrap)
            {
                return EditorGUIUtility.singleLineHeight + 6;
            }

            string fieldValue = _inputFields[index];
            bool containsT = !string.IsNullOrEmpty(fieldValue) && fieldValue.StartsWith("t:", StringComparison.OrdinalIgnoreCase);
            bool containsGlob = !string.IsNullOrEmpty(fieldValue) && (fieldValue.Contains("*") || fieldValue.Contains("?"));
            bool isSimpleNameQuery = !containsT && !containsGlob && !string.IsNullOrEmpty(fieldValue) && !fieldValue.Contains("/");
            bool showSearchIcon = containsT || containsGlob || isSimpleNameQuery;
            bool showPasteButton = (containsT && !containsGlob) || isSimpleNameQuery;
            bool showPingButton = !showSearchIcon && !showPasteButton && !string.IsNullOrEmpty(fieldValue) && fieldValue.Contains("/");

            float magnifyIconButtonWidth = 25;
            float buttonWidth = 50;
            float deleteButtonWidth = 20;
            float spacing = 4;

            float actionsWidth = 0;
            if (showSearchIcon) actionsWidth += magnifyIconButtonWidth + spacing;
            if (showPasteButton) actionsWidth += buttonWidth + spacing;
            if (showPingButton) actionsWidth += buttonWidth + spacing;
            if (actionsWidth > 0) actionsWidth -= spacing;

            float fieldWidth = position.width - (actionsWidth + deleteButtonWidth + 8 + 20); // 20はスクロールバー等のマージン

            GUIStyle textFieldStyle = new GUIStyle(EditorStyles.textArea);
            textFieldStyle.alignment = TextAnchor.MiddleLeft;
            textFieldStyle.wordWrap = true;

            GUIContent content = new GUIContent(fieldValue);
            float textHeight = textFieldStyle.CalcHeight(content, fieldWidth);

            return Mathf.Max(EditorGUIUtility.singleLineHeight, textHeight) + 6;
        }

        // 通常のパスからGameObjectを検索してPingを実行
        private void PingObject(int index, Rect buttonRect = default)
        {
            if (index < 0 || index >= _inputFields.Count) return;

            string fieldValue = _inputFields[index];
            GameObject targetObject = null;

            // prefab edit modeかどうか判定
            UnityEditor.SceneManagement.PrefabStage prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                // prefab edit mode時は、prefab内のオブジェクトを検索
                GameObject prefabRoot = prefabStage.prefabContentsRoot;
                targetObject = FindChildRecursive(prefabRoot, fieldValue);
            }
            else
            {
                // 通常モード時は従来通りGameObject.Findを使用
                targetObject = GameObject.Find(fieldValue);
            }

            if (targetObject != null)
            {
                // GameObjectをPing（ハイライト）
                EditorGUIUtility.PingObject(targetObject);

                // GameObjectを選択（インスペクターに表示）
                Selection.activeGameObject = targetObject;
                return;
            }

            // GameObjectが見つからない場合はポップアップを表示
            SearchResultPopupWindow.Show(buttonRect, new List<HierarchySearchLogic.SearchResult>(), null, "");
        }

        // prefab内の子オブジェクトを再帰的に検索するヘルパーメソッド
        private GameObject FindChildRecursive(GameObject parent, string targetPath)
        {
            if (parent == null) return null;

            // 現在のオブジェクトのパスを取得
            string currentPath = HierarchySearchLogic.GetGameObjectPath(parent);
            if (currentPath == targetPath)
            {
                return parent;
            }

            // 子オブジェクトを再帰的に検索
            Transform parentTransform = parent.transform;
            for (int i = 0; i < parentTransform.childCount; i++)
            {
                GameObject child = parentTransform.GetChild(i).gameObject;
                GameObject result = FindChildRecursive(child, targetPath);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        // "t:"で始まる文字列またはGlobパターンで検索を実行し、ポップアップを表示
        private void SearchObjectsAndShowPopup(string searchQuery, Rect buttonRect)
        {
            // SearchObjectsから結果リストを受け取るように変更
            List<HierarchySearchLogic.SearchResult> searchResults = HierarchySearchLogic.SearchObjects(searchQuery);

            // 検索結果の数に応じて分岐処理！
            if (searchResults.Count == 1)
            {
                // 結果が1つの場合は直接Pingして選択
                HierarchySearchLogic.SearchResult singleResult = searchResults[0];
                EditorGUIUtility.PingObject(singleResult.gameObject);
                Selection.activeGameObject = singleResult.gameObject;
            }
            else
            {
                // 結果が0または複数の場合は従来通りポップアップを表示
                SearchResultPopupWindow.Show(buttonRect, searchResults, (selectedObject) =>
                {
                    // オブジェクトをPingしてハイライト
                    EditorGUIUtility.PingObject(selectedObject);

                    // オブジェクトを選択
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

            // ドラッグされたオブジェクトを受け入れ可能であることを示すカーソルに変更
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                // ドラッグアンドドロップ操作を完了
                DragAndDrop.AcceptDrag();

                // ドロップされたオブジェクトを処理
                foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                {
                    GameObject go = draggedObject as GameObject;
                    if (go != null)
                    {
                        // GameObjectのHierarchyパスを取得
                        string hierarchyPath = HierarchySearchLogic.GetGameObjectPath(go);

                        // 新しいフィールドを追加
                        _inputFields.Add(hierarchyPath);
                        SavePaths();
                        Repaint();
                    }
                }
            }

            evt.Use();
        }

        // Hierarchy検索ウィンドウの検索文字列を設定するメソッド
        private void SetHierarchySearchFilter(string searchString)
        {
            // SceneHierarchyWindowのインスタンスを取得
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
                // Hierarchyウィンドウが開かれていない可能性があるので、警告としておく。
                Debug.LogWarning("Hierarchy window not found. Please click the Hierarchy window once and try again.");
                return; // エラーではなく処理を中断
            }

            // SearchableEditorWindow型（SceneHierarchyWindowの親クラス）を取得
            Type searchableEditorWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow");
            if (searchableEditorWindowType == null)
            {
                Debug.LogError("SearchableEditorWindow type not found");
                return;
            }

            // リフレクションを使用してSetSearchFilterメソッドを取得
            // 4引数のオーバーロード (string, SearchMode, bool, bool) を試す
            MethodInfo setSearchFilterMethod = null;
            try
            {
                // SearchMode enum の型を取得
                Type searchModeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow+SearchMode");
                if (searchModeType != null)
                {
                    setSearchFilterMethod = searchableEditorWindowType.GetMethod("SetSearchFilter",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                        null, // Binder
                        new Type[] { typeof(string), searchModeType, typeof(bool), typeof(bool) }, // 引数の型を指定
                        null); // ParameterModifier
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to get SetSearchFilter (4 args): {e.Message}");
                setSearchFilterMethod = null; // 取得失敗
            }


            if (setSearchFilterMethod != null)
            {
                // 4引数版のSearchMode enumの値(All)を取得
                Type searchModeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow+SearchMode");
                object searchModeAll = Enum.Parse(searchModeType, "All");

                // 4引数でメソッドを実行
                setSearchFilterMethod.Invoke(hierarchyWindow,
                    new object[] { searchString, searchModeAll, true, false });
            }
            else
            {
                // 4引数のメソッドが見つからない場合、3引数のオーバーロード (string, bool, bool) を試す
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

                // 3引数でメソッドを実行
                setSearchFilterMethod.Invoke(hierarchyWindow,
                    new object[] { searchString, true, false });
            }


            // ウィンドウを再描画
            hierarchyWindow.Repaint();
        }
    }
}