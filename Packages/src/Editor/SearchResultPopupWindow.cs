using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// Popup window class for displaying search results.
    /// </summary>
    public class SearchResultPopupWindow : EditorWindow
    {
        private const float SearchWindowMaxHeight = 500; // Maximum height
        private const int MaxDisplayCount = 200; // Display limit
        private const float DefaultMinWindowWidth = 240f; // Default minimum width of the popup
        private List<HierarchySearchLogic.SearchResult> _results = new();
        private Vector2 _scrollPosition;
        private Action<GameObject> _onObjectSelected;
        private string _sourceIndex = string.Empty; // Index indicating which search button opened this

        // Dictionary to track open windows for each search button (input field index)
        private static Dictionary<string, SearchResultPopupWindow> _activeWindows = new();

        public static void Show(Rect buttonRect, List<HierarchySearchLogic.SearchResult> results, Action<GameObject> onObjectSelected,
            string path)
        {
            // If a window from the same source index already exists, close it
            if (_activeWindows.TryGetValue(path, out SearchResultPopupWindow existingWindow))
            {
                // Update the same window (copy the list of results)
                existingWindow._results = new List<HierarchySearchLogic.SearchResult>(results);
                existingWindow._onObjectSelected = onObjectSelected;
                existingWindow.Repaint();
                existingWindow.Focus();
                return;
            }

            // Create a new window
            SearchResultPopupWindow window = CreateInstance<SearchResultPopupWindow>();
            window._results = new List<HierarchySearchLogic.SearchResult>(results); // Save a copy of the results
            window._onObjectSelected = onObjectSelected;
            window._sourceIndex = path;

            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);

            // Calculate the maximum width of the button text
            float maxButtonTextWidth = 0;
            foreach (HierarchySearchLogic.SearchResult result in results)
            {
                // Calculate the width of the button (object name)
                float nameWidth = buttonStyle.CalcSize(new GUIContent(result.gameObject.name)).x;
                maxButtonTextWidth = Mathf.Max(maxButtonTextWidth, nameWidth);
            }

            // Get left and right padding of helpBox
            // EditorStyles.helpBox.padding has left, right, top, bottom, so get the total horizontal padding
            int helpBoxHorizontalPadding = EditorStyles.helpBox.padding.horizontal;

            // The maximum text width of the button plus the helpBox padding is the basis for the maximum content width
            float contentMaxWidth = maxButtonTextWidth + helpBoxHorizontalPadding;

            // Consider the left and right margins of the entire window (e.g., visual margins for the close button and window frame)
            // Adjust this value based on preference and the overall balance of the UI
            float windowHorizontalMargin = 20f;
            float calculatedMaxWidth = contentMaxWidth + windowHorizontalMargin;

            // Calculate content height to determine if a scrollbar is needed
            float itemHeight = 50; // Button + path + margin + box
            float titleHeight = 30; // Height of title and margin

            int itemCount = results.Count;
            if (results.Count >= MaxDisplayCount)
            {
                itemCount = 0; // If MaxDisplayCount or more, treat item count as 0 for message display
            }

            float contentHeight = (itemCount * itemHeight) + titleHeight + 20; // Add some margin top and bottom
            bool needsScrollbar = contentHeight > SearchWindowMaxHeight;

            // Determine the actual display height
            float height = needsScrollbar ? SearchWindowMaxHeight : contentHeight;

            // If a scrollbar is needed, adjust calculatedMaxWidth to account for its width
            if (needsScrollbar)
            {
                // The width of a standard Unity scrollbar is about 15-20px,
                // but it can change depending on the skin, so add a fixed value with some leeway.
                // Be careful as GUI.skin.verticalScrollbar.fixedWidth may not be reliable outside OnGUI.
                calculatedMaxWidth += 25f; // Consider the width of the scrollbar and a small margin between it and adjacent content
            }

            // Set the minimum width. It won't get smaller than this.
            // For example, we want to ensure enough width to display the window title and close button at a minimum.
            float minWindowWidth = DefaultMinWindowWidth; 
            float finalWidth = Mathf.Max(calculatedMaxWidth, minWindowWidth);

            // Set window position (so that the bottom right of the search button aligns with the top right of the popup)
            Vector2 popupPosition = GUIUtility.GUIToScreenPoint(
                new Vector2(buttonRect.x + buttonRect.width, buttonRect.y + buttonRect.height));

            // Align the top right of the popup with the bottom right of the button
            popupPosition = new Vector2(popupPosition.x - finalWidth, popupPosition.y);

            // Display as a popup window
            window.minSize = new Vector2(DefaultMinWindowWidth, 50f); 
            window.titleContent = new GUIContent("Search Results"); 
            window.position = new Rect(popupPosition.x, popupPosition.y, finalWidth, height);
            window.Show(); 

            // Call Repaint for initialization
            window.Repaint();
        }

        private void OnGUI()
        {
            // Background
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(0.2f, 0.2f, 0.2f, 1f));

            // Start UI layout
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.Space(5);

            // Customize GUI style for buttons
            GUIStyle buttonStyle = new GUIStyle(EditorStyles.miniButton);
            buttonStyle.alignment = TextAnchor.MiddleCenter; // Center align

            // Customize GUI style for path display
            GUIStyle pathStyle = new GUIStyle(EditorStyles.wordWrappedLabel);
            pathStyle.wordWrap = true;
            pathStyle.fontSize = 10;
            pathStyle.normal.textColor = new Color(0.7f, 0.7f, 0.7f);

            // Title for search results
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

            // Disable scrollbar if there is only one search result
            if (_results.Count <= 1)
            {
                // Display result list (no scroll)
                foreach (HierarchySearchLogic.SearchResult result in _results)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (GUILayout.Button(result.gameObject.name, buttonStyle))
                    { 
                        _onObjectSelected?.Invoke(result.gameObject);
                    }

                    // Display path as label
                    EditorGUILayout.LabelField(result.path, pathStyle);
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(2);
                }
            }
            else
            {
                // Scroll view for search results (only if multiple items)
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

                // Display result list
                foreach (HierarchySearchLogic.SearchResult result in _results)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    if (GUILayout.Button(result.gameObject.name, buttonStyle))
                    { 
                        _onObjectSelected?.Invoke(result.gameObject);
                    }

                    // Display path as label
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
            // Remove from dictionary when window is closed
            if (!string.IsNullOrEmpty(_sourceIndex) && _activeWindows.ContainsKey(_sourceIndex))
            {
                _activeWindows.Remove(_sourceIndex);
            }
        }
    }
}