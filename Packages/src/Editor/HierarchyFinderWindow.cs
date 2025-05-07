using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// Hierarchyでの検索を便利にするwindowです
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

        // 入力文字列を保存するリスト
        private List<string> _inputFields = new();

        // スクロール位置
        private Vector2 _scrollPosition;

        // ReorderableListを使用して並べ替え可能なリストを実装
        private ReorderableList _reorderableList;

        // JsonUtilityでシリアライズ/デシリアライズするための内部クラスを復活させる
        [Serializable]
        private class StringList
        {
            public List<string> items = new();
        }

        // メニューアイテムの追加
        [MenuItem("Tools/Hierarchy Finder", false, 1000)]
        public static void ShowWindow()
        {
            // 既存のウィンドウを取得するか、新しいウィンドウを作成
            GetWindow<HierarchyFinderWindow>("Hierarchy Finder");
        }

        private void OnGUI()
        {
            // ドラッグ＆ドロップの処理
            HandleDragAndDrop();

            EditorGUILayout.Space(2);

            // リストが空の場合のみヘルプボックスを表示
            if (_inputFields.Count == 0)
            {
                EditorGUILayout.HelpBox("D & Dでパスを自動登録。\n" +
                                        "t: で始まる場合や *? を含む場合は「検索」でポップアップ表示。\n" +
                                        "t: で始まり、かつ *? を含まない場合のみ「paste」で検索窓に入力します。", MessageType.Info);
                EditorGUILayout.Space(2);
            }

            // スクロールビューの開始
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            // ReorderableListを描画
            _reorderableList.DoLayoutList();

            // スクロールビューの終了
            EditorGUILayout.EndScrollView();
        }

        private void OnEnable()
        {
            // 保存されたリストを読み込む
            LoadSavedPaths();

            // ReorderableListの初期化
            InitializeReorderableList();
        }

        private void OnDisable()
        {
            // ウィンドウが閉じられる際にリストを保存
            SavePaths();
        }

        // 保存されたパスを読み込む
        private void LoadSavedPaths()
        {
            // EditorUserSettingsからJSON文字列を読み込む
            string json = EditorUserSettings.GetConfigValue(PrefsKey);
            if (!string.IsNullOrEmpty(json))
            {
                // JSONからリストにデシリアライズ
                StringList list = JsonUtility.FromJson<StringList>(json);
                if (list != null && list.items != null)
                {
                    _inputFields = list.items;
                }
                else
                {
                    // デシリアライズ失敗時は空リストで初期化
                    _inputFields = new List<string>();
                }
            }
            else
            {
                // 保存された値がない場合も空リストで初期化
                _inputFields = new List<string>();
            }
        }

        // パスリストを保存
        private void SavePaths()
        {
            // リストをJSON文字列にシリアライズ
            StringList list = new StringList { items = _inputFields };
            string json = JsonUtility.ToJson(list);

            // EditorUserSettingsにJSON文字列を保存
            EditorUserSettings.SetConfigValue(PrefsKey, json);
        }

        private void OnDrawElementCallback(Rect rect, int index, bool isActive, bool isFocused)
        {
            if (index < 0 || index >= _inputFields.Count) return;

            rect.y += 2;
            rect.height = EditorGUIUtility.singleLineHeight;

            string fieldValue = _inputFields[index];
            // t: を含むか？
            bool containsT = !string.IsNullOrEmpty(fieldValue) && fieldValue.StartsWith("t:", StringComparison.OrdinalIgnoreCase);
            // Glob文字 (* か ?) を含むか？
            bool containsGlob = !string.IsNullOrEmpty(fieldValue) && (fieldValue.Contains("*") || fieldValue.Contains("?"));

            // ボタン表示ロジックをシンプルに
            bool showSearchIcon = containsT || containsGlob; // t: を含むか、Glob文字を含めば検索アイコン表示
            bool showPasteButton = containsT && !containsGlob; // t: を含み、かつGlob文字を含まない場合のみPasteボタン表示
            bool showPingButton = !containsT && !containsGlob; // t: もGlob文字も含まない場合のみPingボタン表示

            // 横方向のレイアウト調整
            float magnifyIconButtonWidth = 25; // 検索アイコンの幅
            float buttonWidth = 50; // Ping または Paste ボタンの幅
            float deleteButtonWidth = 20; // 削除ボタンの幅
            float spacing = 4;

            float actionsWidth = 0; // アクションボタン全体の幅を初期化

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

            // actionsWidth が 0 でない場合のみ最後の spacing を引く
            if (actionsWidth > 0)
            {
                actionsWidth -= spacing;
            }

            float fieldWidth = rect.width - (actionsWidth + deleteButtonWidth + 8); // 8は左右のマージン的な余裕

            // テキストフィールド
            Rect textFieldRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            _inputFields[index] = EditorGUI.TextField(textFieldRect, _inputFields[index]);

            float currentX = rect.x + fieldWidth + spacing;

            // ボタン描画ロジック (ここもフラグに基づいてシンプルに)
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
                // Pingボタンが最後なので currentX の更新は不要や
            }

            // 削除ボタン (変更なし)
            Rect deleteButtonRect = new Rect(rect.x + rect.width - deleteButtonWidth, rect.y, deleteButtonWidth, rect.height);
            GUIStyle deleteButtonStyle = new GUIStyle(EditorStyles.miniButton);
            deleteButtonStyle.alignment = TextAnchor.MiddleCenter;

            if (GUI.Button(deleteButtonRect, ButtonTexts.Delete, deleteButtonStyle))
            {
                _inputFields.RemoveAt(index);
                SavePaths();
                Repaint();
            }
        }

        // ReorderableListの初期化
        private void InitializeReorderableList()
        {
            _reorderableList = new ReorderableList(_inputFields, typeof(string), true, false, true, false);
            _reorderableList.drawElementCallback = OnDrawElementCallback;
            _reorderableList.headerHeight = 0;
            _reorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 6;

            // 新しい要素の追加処理
            _reorderableList.onAddCallback = (ReorderableList list) =>
            {
                _inputFields.Add("");
                SavePaths();
            };

            _reorderableList.onReorderCallback = (ReorderableList list) => { SavePaths(); };
        }

        // 通常のパスからGameObjectを検索してPing
        private void PingObject(int index, Rect buttonRect = default)
        {
            if (index < 0 || index >= _inputFields.Count) return;

            string fieldValue = _inputFields[index];
            GameObject targetObject = GameObject.Find(fieldValue);
            if (targetObject != null)
            {
                // GameObjectをPing（ハイライト表示）
                EditorGUIUtility.PingObject(targetObject);

                // GameObjectを選択状態にする（Inspectorに表示）
                Selection.activeGameObject = targetObject;
                return;
            }

            // GameObjectが見つからない場合はポップアップを表示
            SearchResultPopupWindow.Show(buttonRect, new List<HierarchySearchLogic.SearchResult>(), null, "");
        }

        // t:で始まる文字列、またはGlobパターンで検索を実行しポップアップを表示
        private void SearchObjectsAndShowPopup(string searchQuery, Rect buttonRect)
        {
            // SearchObjectsから結果リストを受け取るように変更
            List<HierarchySearchLogic.SearchResult> searchResults = HierarchySearchLogic.SearchObjects(searchQuery);

            // 検索結果の件数で処理を分岐させるで！
            if (searchResults.Count == 1)
            {
                // 結果が1件やったら、そのままPingして選択状態にするんや
                HierarchySearchLogic.SearchResult singleResult = searchResults[0];
                EditorGUIUtility.PingObject(singleResult.gameObject);
                Selection.activeGameObject = singleResult.gameObject;
            }
            else
            {
                // 結果が0件か複数件やったら、従来通りポップアップを表示するで
                SearchResultPopupWindow.Show(buttonRect, searchResults, (selectedObject) =>
                {
                    // オブジェクトをPingしてハイライト表示
                    EditorGUIUtility.PingObject(selectedObject);

                    // オブジェクトを選択状態にする
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

            // ドラッグされているオブジェクトを受け入れられるようにカーソルを変更
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (evt.type == EventType.DragPerform)
            {
                // ドラッグ＆ドロップ操作を完了
                DragAndDrop.AcceptDrag();

                // ドロップされたオブジェクトの処理
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

        // Hierarchyの検索窓に検索文字列を設定するメソッド
        private void SetHierarchySearchFilter(string searchString)
        {
            // SceneHierarchyWindow のインスタンスを取得
            Type sceneHierarchyWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
            if (sceneHierarchyWindowType == null)
            {
                Debug.LogError("UnityEditor.SceneHierarchyWindow タイプが見つかりませんでした。");
                return;
            }

            PropertyInfo lastInteractedHierarchyWindowProperty = sceneHierarchyWindowType.GetProperty("lastInteractedHierarchyWindow", BindingFlags.Public | BindingFlags.Static);
            if (lastInteractedHierarchyWindowProperty == null)
            {
                Debug.LogError("lastInteractedHierarchyWindow プロパティが見つかりませんでした。");
                return;
            }

            EditorWindow hierarchyWindow = lastInteractedHierarchyWindowProperty.GetValue(null) as EditorWindow;

            if (hierarchyWindow == null)
            {
                // Hierarchy ウィンドウが開かれていない場合があるため、警告に留める
                Debug.LogWarning("Hierarchy ウィンドウが見つかりません。Hierarchy ウィンドウを一度クリックしてから試してください。");
                return; // エラーではなく処理を中断
            }

            // SearchableEditorWindowタイプ（SceneHierarchyWindowの親クラス）を取得
            Type searchableEditorWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow");
            if (searchableEditorWindowType == null)
            {
                Debug.LogError("SearchableEditorWindow タイプが見つかりませんでした");
                return;
            }

            // SetSearchFilter メソッドをリフレクションで取得
            // 引数が4つのオーバーロード (string, SearchMode, bool, bool) を試す
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
                Debug.LogWarning($"SetSearchFilter (4 args) の取得に失敗しました: {e.Message}");
                setSearchFilterMethod = null; // 取得失敗
            }


            if (setSearchFilterMethod != null)
            {
                // 引数が4つのバージョンの SearchMode enum の値を取得 (All)
                Type searchModeType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow+SearchMode");
                object searchModeAll = Enum.Parse(searchModeType, "All");

                // 引数が4つのバージョンのメソッドを実行
                setSearchFilterMethod.Invoke(hierarchyWindow,
                    new object[] { searchString, searchModeAll, true, false });
            }
            else
            {
                // 引数が4つのメソッドが見つからない場合、引数が3つのオーバーロード (string, bool, bool) を試す
                setSearchFilterMethod = searchableEditorWindowType.GetMethod("SetSearchFilter",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new Type[] { typeof(string), typeof(bool), typeof(bool) },
                    null);

                if (setSearchFilterMethod == null)
                {
                    Debug.LogError("SetSearchFilter メソッドが見つかりませんでした。");
                    return;
                }

                // 引数が3つのバージョンのメソッドを実行
                setSearchFilterMethod.Invoke(hierarchyWindow,
                    new object[] { searchString, true, false });
            }


            // ウィンドウを再描画
            hierarchyWindow.Repaint();
        }
    }
}