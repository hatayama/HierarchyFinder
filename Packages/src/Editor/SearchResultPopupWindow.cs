using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// 検索結果を表示するためのポップアップウィンドウクラス
    /// </summary>
    public class SearchResultPopupWindow : EditorWindow
    {
        private const float SearchWindowMaxHeight = 500; // 最大高さ
        private const int MaxDisplayCount = 200; // 表示上限
        private const float DefaultMinWindowWidth = 240f; // ★追加: ポップアップの最小幅のデフォルト値
        private List<HierarchySearchLogic.SearchResult> _results = new();
        private Vector2 _scrollPosition;
        private Action<GameObject> _onObjectSelected;
        private string _sourceIndex = string.Empty; // どの検索ボタンから開かれたかを示すインデックス

        // 各検索ボタン（入力欄インデックス）ごとに開いているウィンドウを追跡する辞書
        private static Dictionary<string, SearchResultPopupWindow> _activeWindows = new();

        // ウィンドウのドラッグ処理用
        private Vector2 _dragStartPosition;
        private bool _isDragging = false;

        // 閉じるボタン用
        private Rect _closeButtonRect;
        private GUIStyle _closeButtonStyle;

        public static void Show(Rect buttonRect, List<HierarchySearchLogic.SearchResult> results, Action<GameObject> onObjectSelected,
            string path)
        {
            // 同じソースインデックスからのウィンドウが既に存在する場合は閉じる
            if (_activeWindows.TryGetValue(path, out SearchResultPopupWindow existingWindow))
            {
                // 同じウィンドウを更新（結果のリストをコピー）
                existingWindow._results = new List<HierarchySearchLogic.SearchResult>(results);
                existingWindow._onObjectSelected = onObjectSelected;
                existingWindow.Repaint();
                existingWindow.Focus();
                return;
            }

            // 新しいウィンドウを作成
            SearchResultPopupWindow window = CreateInstance<SearchResultPopupWindow>();
            window._results = new List<HierarchySearchLogic.SearchResult>(results); // 結果のコピーを保存
            window._onObjectSelected = onObjectSelected;
            window._sourceIndex = path;

            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);

            // ボタンのテキストの最大幅を計算
            float maxButtonTextWidth = 0;
            foreach (HierarchySearchLogic.SearchResult result in results)
            {
                // ボタン（オブジェクト名）の幅を計算
                float nameWidth = buttonStyle.CalcSize(new GUIContent(result.gameObject.name)).x;
                maxButtonTextWidth = Mathf.Max(maxButtonTextWidth, nameWidth);
            }

            // helpBox の左右パディングを取得
            // EditorStyles.helpBox.padding は left, right, top, bottom を持つので、horizontal で左右合計を取得
            int helpBoxHorizontalPadding = EditorStyles.helpBox.padding.horizontal;

            // ボタンの最大テキスト幅に helpBox のパディングを加えたものが、コンテンツの最大幅の基本となる
            float contentMaxWidth = maxButtonTextWidth + helpBoxHorizontalPadding;

            // ウィンドウ全体の左右マージンを考慮 (例: 閉じるボタンやウィンドウ枠の視覚的な余白)
            // この値は好みやUI全体のバランスを見て調整するとええで
            float windowHorizontalMargin = 20f;
            float calculatedMaxWidth = contentMaxWidth + windowHorizontalMargin;


            // コンテンツの高さを計算して、スクロールバーが必要かどうか判断
            float itemHeight = 50; // ボタン+パス+余白+ボックス
            float titleHeight = 30; // タイトルと余白の高さ

            int itemCount = results.Count;
            if (results.Count >= MaxDisplayCount)
            {
                itemCount = 0; // MaxDisplayCount以上の場合はメッセージ表示のため、アイテムカウントは0扱い
            }

            float contentHeight = (itemCount * itemHeight) + titleHeight + 20; // 上下に少し余白を追加
            bool needsScrollbar = contentHeight > SearchWindowMaxHeight;

            // 実際に表示する高さを決定
            float height = needsScrollbar ? SearchWindowMaxHeight : contentHeight;

            // スクロールバーが必要な場合、その幅を考慮して calculatedMaxWidth を調整
            if (needsScrollbar)
            {
                // Unity標準のスクロールバーの幅は大体15-20px程度やけど、
                // スキンによって変わる可能性もあるから、少し余裕を見て固定値で加算する。
                // GUI.skin.verticalScrollbar.fixedWidth は OnGUI 外では信頼できない場合があるから注意や。
                calculatedMaxWidth += 25f; // スクロールバーの幅と、隣接するコンテンツとの間のわずかな余白を考慮
            }

            // 最小幅を設定。これより小さくはならんようにする。
            // 例えば、ウィンドウタイトルや閉じるボタンが最低限表示できる幅は確保したいところやな。
            float minWindowWidth = DefaultMinWindowWidth; // ★変更: 定数を使用
            float finalWidth = Mathf.Max(calculatedMaxWidth, minWindowWidth);

            // ウィンドウの位置を設定（検索ボタンの右下とポップアップの右上が一致するように）
            Vector2 popupPosition = GUIUtility.GUIToScreenPoint(
                new Vector2(buttonRect.x + buttonRect.width, buttonRect.y + buttonRect.height));

            // ポップアップの右上をボタンの右下に合わせる
            popupPosition = new Vector2(popupPosition.x - finalWidth, popupPosition.y);

            // ポップアップウィンドウとして表示
            window.position = new Rect(popupPosition.x, popupPosition.y, finalWidth, height);
            window.ShowPopup();

            // 初期化用にRepaintを呼ぶ
            window.Repaint();
        }

        private void OnGUI()
        {
            // 背景
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.2f, 0.2f, 0.2f, 1f));

            // 閉じるボタンを描画（右上）
            // 閉じるボタン用スタイルの初期化
            _closeButtonStyle = new GUIStyle(EditorStyles.miniButton);
            _closeButtonStyle.normal.textColor = Color.white;
            _closeButtonStyle.fontSize = 16;
            _closeButtonStyle.fontStyle = FontStyle.Bold;
            _closeButtonStyle.alignment = TextAnchor.MiddleCenter;
            _closeButtonStyle.fixedWidth = 24;
            _closeButtonStyle.fixedHeight = 24;
            _closeButtonRect = new Rect(position.width - 30, 5, 24, 24);
            if (GUI.Button(_closeButtonRect, "×", _closeButtonStyle))
            {
                Close();
                // イベントを使用して、下にあるUIへの伝播を防ぐ
                Event.current.Use();
                return;
            }

            // UIレイアウトの開始
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.Space(5);

            // ボタンのGUIスタイルをカスタマイズ
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);
            buttonStyle.alignment = TextAnchor.MiddleCenter; // 中央寄せ

            // パス表示用のGUIスタイルをカスタマイズ
            GUIStyle pathStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            pathStyle.wordWrap = true;
            pathStyle.fontSize = 10;
            pathStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            // 検索結果のタイトル
            GUILayout.Label($"Search Results ({_results.Count} items)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            if (_results.Count == 0)
            {
                GUILayout.Label($"Nothing found", EditorStyles.label);
                return;
            }

            if (_results.Count >= MaxDisplayCount)
            {
                GUILayout.Label($"Too many search results. \nWe recommend narrowing down by gameObject name or using the paste function.", EditorStyles.label);
                return;
            }

            // 検索結果1件の場合はスクロールバーを無効化
            if (_results.Count <= 1)
            {
                // 結果リストを表示（スクロールなし）
                foreach (HierarchySearchLogic.SearchResult result in _results)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (GUILayout.Button(result.gameObject.name, buttonStyle))
                    {
                        _onObjectSelected?.Invoke(result.gameObject);
                    }

                    // パスをラベルとして表示
                    EditorGUILayout.LabelField(result.path, pathStyle);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                // 検索結果のスクロールビュー（複数件の場合のみ）
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                // 結果リストを表示
                foreach (HierarchySearchLogic.SearchResult result in _results)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (GUILayout.Button(result.gameObject.name, buttonStyle))
                    {
                        _onObjectSelected?.Invoke(result.gameObject);
                    }

                    // パスをラベルとして表示
                    EditorGUILayout.LabelField(result.path, pathStyle);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }

                EditorGUILayout.EndScrollView();
            }

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.KeyDown)
            {
                Close();
                Event.current.Use();
            }

            // 処理順序の変更: すべてのGUIが描画された後でドラッグ処理をチェック
            HandleWindowDrag();
        }

        private void HandleWindowDrag()
        {
            Event evt = Event.current;

            // 閉じるボタン上ではドラッグを開始しない
            if (_closeButtonRect.Contains(evt.mousePosition)) return;

            switch (evt.type)
            {
                case EventType.MouseDown:
                    if (evt.button == 0) // 左クリック
                    {
                        // ドラッグを開始する前に、このイベントが他のコントロールに処理されないか確認
                        // (ボタンなどのUI要素の上にマウスがある場合は、ドラッグを開始しない)
                        if (!EditorGUIUtility.hotControl.Equals(0)) return;

                        _isDragging = true;
                        _dragStartPosition = evt.mousePosition;
                        evt.Use();
                    }

                    break;

                case EventType.MouseDrag:
                    if (_isDragging)
                    {
                        Vector2 delta = evt.mousePosition - _dragStartPosition;
                        Rect windowPos = position;
                        windowPos.position += delta;
                        position = windowPos;
                        evt.Use();
                    }

                    break;

                case EventType.MouseUp:
                    if (_isDragging)
                    {
                        _isDragging = false;
                        evt.Use();
                    }

                    break;
                default:
                    break;
            }
        }

        private void OnDestroy()
        {
            // ウィンドウが閉じられたら辞書から削除
            if (!string.IsNullOrEmpty(_sourceIndex) && _activeWindows.ContainsKey(_sourceIndex))
            {
                _activeWindows.Remove(_sourceIndex);
            }
        }
    }
}