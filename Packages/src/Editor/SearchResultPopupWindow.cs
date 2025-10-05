using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// 検索結果を表示するためのポップアップウィンドウクラスや。
    /// </summary>
    public class SearchResultPopupWindow : EditorWindow
    {
        private const int MaxDisplayCount = 200; // 表示数の上限
        private const float DefaultMinWindowWidth = 240f; // ポップアップのデフォルトの最小幅
        private List<HierarchySearchLogic.SearchResult> _results = new();
        private Vector2 _scrollPosition;
        private Action<GameObject> _onObjectSelected;
        private string _sourceIndex = string.Empty; // どの検索ボタンから開かれたかを示すインデックス

        // 各検索ボタン（入力フィールドのインデックス）ごとに開いているウィンドウを追跡するための辞書
        private static Dictionary<string, SearchResultPopupWindow> _activeWindows = new();

        public static void Show(Rect buttonRect, List<HierarchySearchLogic.SearchResult> results, Action<GameObject> onObjectSelected,
            string path)
        {
            // 同じソースインデックスのウィンドウが既に存在する場合は閉じる
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

            // ボタンテキストの最大幅を計算
            float maxButtonTextWidth = 0;
            foreach (HierarchySearchLogic.SearchResult result in results)
            {
                // ボタン（オブジェクト名）の幅を計算
                float nameWidth = buttonStyle.CalcSize(new GUIContent(result.gameObject.name)).x;
                maxButtonTextWidth = Mathf.Max(maxButtonTextWidth, nameWidth);
            }

            // helpBoxの左右のパディングを取得
            // EditorStyles.helpBox.padding は left, right, top, bottom を持つので、左右の合計パディングを取得
            int helpBoxHorizontalPadding = EditorStyles.helpBox.padding.horizontal;

            // ボタンの最大テキスト幅とhelpBoxのパディングを足したものが、最大コンテンツ幅の基準
            float contentMaxWidth = maxButtonTextWidth + helpBoxHorizontalPadding;

            // ウィンドウ全体の左右のマージンを考慮（例：閉じるボタンやウィンドウ枠の視覚的なマージン）
            // この値は好みやUI全体のバランスで調整
            float windowHorizontalMargin = 20f;
            float calculatedMaxWidth = contentMaxWidth + windowHorizontalMargin;

            // スクロールバーが必要かどうかを判断するためにコンテンツの高さを計算
            float itemHeight = 50; // ボタン + パス + マージン + ボックス
            float titleHeight = 30; // タイトルとマージンの高さ

            int itemCount = results.Count;
            if (results.Count >= MaxDisplayCount)
            {
                itemCount = 0; // MaxDisplayCount以上の場合は、メッセージ表示のためアイテム数を0として扱う
            }

            float contentHeight = (itemCount * itemHeight) + titleHeight + 10; // 上下に若干のマージンを追加

            // 最小幅を設定。これより小さくはならない。
            float minWindowWidth = DefaultMinWindowWidth; 
            float finalWidth = Mathf.Max(calculatedMaxWidth, minWindowWidth);

            // ウィンドウの位置を設定（検索ボタンの右下にポップアップの右上を合わせるように）
            Vector2 popupPosition = GUIUtility.GUIToScreenPoint(
                new Vector2(buttonRect.x + buttonRect.width, buttonRect.y + buttonRect.height));

            // ポップアップの右上をボタンの右下に合わせる
            popupPosition = new Vector2(popupPosition.x - finalWidth, popupPosition.y);

            // ポップアップウィンドウとして表示
            window.minSize = new Vector2(DefaultMinWindowWidth, 50f); 
            window.titleContent = new GUIContent("Search Results"); 
            window.position = new Rect(popupPosition.x, popupPosition.y, finalWidth, contentHeight);
            window.Show(); 

            // 初期化のためにRepaintを呼ぶ
            window.Repaint();
        }

        private void OnGUI()
        {
            // 背景
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.2f, 0.2f, 0.2f, 1f));

            // UIレイアウト開始
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.Space(5);

            // ボタン用のGUIStyleをカスタマイズ
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);
            buttonStyle.alignment = TextAnchor.MiddleCenter; // 中央揃え

            // パス表示用のGUIStyleをカスタマイズ
            GUIStyle pathStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            pathStyle.wordWrap = true;
            pathStyle.fontSize = 10;
            pathStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            GUILayout.Label($"Search Results ({_results.Count} items)", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);

            if (_results.Count == 0)
            {
                GUILayout.Label($"No search results found.", EditorStyles.label);
                EditorGUILayout.EndVertical();
                return;
            }

            if (_results.Count >= MaxDisplayCount)
            {
                GUILayout.Label($"Too many search results.\nIt is recommended to narrow down by object name or use the paste function.", EditorStyles.label);
                EditorGUILayout.EndVertical();
                return;
            }

            // 検索結果が1件以下の場合はスクロールバーを無効化
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

                    // パスをラベルで表示
                    EditorGUILayout.LabelField(result.path, pathStyle);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                // 検索結果のスクロールビュー（複数アイテムの場合のみ）
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                // 結果リストを表示
                foreach (HierarchySearchLogic.SearchResult result in _results)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (GUILayout.Button(result.gameObject.name, buttonStyle))
                    { 
                        _onObjectSelected?.Invoke(result.gameObject);
                    }

                    // パスをラベルで表示
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
        }

        private void OnLostFocus()
        {
            Close();
        }

        private void OnDestroy()
        {
            // ウィンドウが閉じられたときに辞書から削除
            if (!string.IsNullOrEmpty(_sourceIndex) && _activeWindows.ContainsKey(_sourceIndex))
            {
                _activeWindows.Remove(_sourceIndex);
            }
        }
    }
}