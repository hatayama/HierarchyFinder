using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using UnityEditorInternal;
using System;

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
                EditorGUILayout.HelpBox("D & Dでパスを自動登録。\nt: で始まるか、*? を含む場合は「検索」で検索結果をポップアップ表示。\nt: で始まる場合のみ「paste」で検索窓に入力します。", MessageType.Info);
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
            bool isTypeSearch = !string.IsNullOrEmpty(fieldValue) && fieldValue.Contains("t:");
            bool isGlobSearch = !string.IsNullOrEmpty(fieldValue) && (fieldValue.Contains("*") || fieldValue.Contains("?"));

            // 横方向のレイアウト調整（ボタンの数に応じて）
            float magnifyIconButtonWidth = 30;
            float buttonWidth = 50;
            float deleteButtonWidth = 25;
            float spacing = 4;

            float actionsWidth = 0;

            bool showSearchIcon = isTypeSearch || isGlobSearch;
            bool showPasteButton = isTypeSearch;
            bool showPingButton = !isTypeSearch && !isGlobSearch;

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

            if (actionsWidth > 0)
            {
                actionsWidth -= spacing;
            }

            float fieldWidth = rect.width - (actionsWidth + deleteButtonWidth + 8);

            // テキストフィールド
            Rect textFieldRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);
            _inputFields[index] = EditorGUI.TextField(textFieldRect, _inputFields[index]);

            float currentX = rect.x + fieldWidth + spacing;

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
            }

            // 削除ボタンを右端に追加
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

            _reorderableList.onReorderCallback = (ReorderableList list) =>
            {
                SavePaths();
            };
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

            // クリックしたボタンの下にポップアップを表示（ソースインデックスも渡す）
            SearchResultPopupWindow.Show(buttonRect, searchResults, (selectedObject) =>
            {
                // オブジェクトをPingしてハイライト表示
                EditorGUIUtility.PingObject(selectedObject);

                // オブジェクトを選択状態にする
                Selection.activeGameObject = selectedObject;
            }, searchQuery);
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
            // SearchableEditorWindowタイプ（SceneHierarchyWindowの親クラス）を取得
            Type searchableEditorWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SearchableEditorWindow");
            if (searchableEditorWindowType == null)
            {
                Debug.LogError("SearchableEditorWindowタイプが見つかりませんでした");
                return;
            }

            // SetSearchFilter メソッドをリフレクションで取得
            var setSearchFilterMethod = searchableEditorWindowType.GetMethod("SetSearchFilter",
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

            if (setSearchFilterMethod == null)
            {
                Debug.LogError("検索メソッドが見つかりませんでした");
                return;
            }

            // 検索メソッドを実行
            setSearchFilterMethod.Invoke(focusedWindow,
                new object[] { searchString, SearchableEditorWindow.SearchMode.All, true, false });

            // ウィンドウを再描画
            focusedWindow.Repaint();
        }
    }
}
