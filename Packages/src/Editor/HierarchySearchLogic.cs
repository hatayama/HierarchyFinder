using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// This class provides logic to search for GameObjects in the Hierarchy.
    /// It supports Glob pattern searching for both type names and paths! (Unity spec *, ** compliant)
    /// </summary>
    public static class HierarchySearchLogic
    {
        // Class to hold search results
        public class SearchResult
        {
            public GameObject gameObject;
            public string path;

            public SearchResult(GameObject gameObject, string path)
            {
                this.gameObject = gameObject;
                this.path = path;
            }
        }

        public static List<SearchResult> SearchObjects(string searchQuery)
        {
            List<SearchResult> results = new List<SearchResult>();
            if (string.IsNullOrWhiteSpace(searchQuery))
            {
                return results;
            }

            GameObject[] gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Parse the query (distinguish t:, name filter, path Glob)
            (string typePattern, string nameFilter, string pathGlobPattern) = ParseQuery(searchQuery);

            bool isTypeSearchEnabled = !string.IsNullOrEmpty(typePattern);
            bool isNameFilterEnabled = !string.IsNullOrEmpty(nameFilter);
            bool isPathGlobEnabled = !string.IsNullOrEmpty(pathGlobPattern);

            Regex pathRegex = null;
            if (isPathGlobEnabled)
            {
                // If path Glob exists, convert to regex (using Unity's GlobToRegex)
                pathRegex = new Regex(GlobToRegex(pathGlobPattern), RegexOptions.IgnoreCase);
            }

            Regex typeRegex = null;
            bool useTypeGlob = isTypeSearchEnabled && (typePattern.Contains("*") || typePattern.Contains("?"));
            if (useTypeGlob)
            {
                // If type name Glob exists, convert to regex (using Unity's GlobToRegex)
                // Since type names are not expected to contain /, the difference between * and ** should not matter
                typeRegex = new Regex(GlobToRegex(typePattern), RegexOptions.IgnoreCase);
            }

            Regex nameRegex = null; // Add Regex for name filter
            bool useNameGlob = isNameFilterEnabled && (nameFilter.Contains("*") || nameFilter.Contains("?"));
            if (useNameGlob)
            {
                // If name filter Glob exists, convert to regex (using Unity's GlobToRegex)
                // Since names are not expected to contain /, the difference between * and ** should not matter
                nameRegex = new Regex(GlobToRegex(nameFilter), RegexOptions.IgnoreCase);
            }


            foreach (GameObject obj in gameObjects)
            {
                // 1. Type filter
                if (isTypeSearchEnabled)
                {
                    bool typeMatch;
                    if (useTypeGlob)
                    {
                        // Type name Glob matching
                        typeMatch = MatchesTypeGlobPattern(obj, typeRegex);
                    }
                    else
                    {
                        // Normal type name matching (including GameObject special case)
                        typeMatch = typePattern.Equals("GameObject", StringComparison.OrdinalIgnoreCase) || GameObjectHasComponentType(obj, typePattern);
                    }

                    if (!typeMatch) continue; // If type doesn't match, continue to next
                }

                // 2. Path Glob filter (only if path Glob is specified)
                string path = null; // Get if needed
                if (isPathGlobEnabled)
                {
                    path = GetGameObjectPath(obj);
                    if (!pathRegex.IsMatch(path)) continue; // If path Glob doesn't match, continue to next
                }

                // 3. GameObject name filter (only if name filter is specified)
                // According to ParseQuery, if isNameFilterEnabled is true, pathGlobPattern should be null
                if (isNameFilterEnabled)
                {
                    bool nameMatch;
                    if (useNameGlob)
                    {
                        // Name Glob matching
                        nameMatch = nameRegex.IsMatch(obj.name);
                    }
                    else
                    {
                        // Normal partial match
                        nameMatch = ContainsIgnoreCase(obj.name, nameFilter);
                    }

                    if (!nameMatch) continue; // If name doesn't match, continue to next
                }

                // If it passes the filters, add to results
                if (path == null) // If path hasn't been retrieved yet, retrieve it
                {
                    path = GetGameObjectPath(obj);
                }

                results.Add(new SearchResult(obj, path));
            }

            // Sort search results by Hierarchy order
            results = results.OrderBy(result => GetHierarchySortKey(result.gameObject)).ToList();

            return results;
        }

        /// <summary>
        /// Generates a sort key representing the GameObject's order in the Hierarchy.
        /// It's a string of GetSiblingIndex() from each level from the root, zero-padded and joined by "/".
        /// Example: "0003/0001/0005"
        /// </summary>
        private static string GetHierarchySortKey(GameObject obj)
        {
            if (obj == null)
            {
                return string.Empty;
            }

            Stack<string> pathParts = new Stack<string>();
            Transform currentTransform = obj.transform;

            while (currentTransform != null)
            {
                // Add GetSiblingIndex() zero-padded to 4 digits
                pathParts.Push(currentTransform.GetSiblingIndex().ToString("D4"));
                currentTransform = currentTransform.parent;
            }

            // Pop from stack and join with "/"
            return string.Join("/", pathParts);
        }

        // Method to parse the query string and extract type pattern, name filter, and path Glob pattern
        private static (string typePattern, string nameFilter, string pathGlobPattern) ParseQuery(string searchQuery)
        {
            // NOTE: This parsing logic determines if what follows "t:" is a name filter or path Glob
            // solely based on the presence of /. 
            // -> Correction: Changed to consider it a path Glob if it contains * or ? even without /.
            searchQuery = searchQuery.Trim();
            string typePattern = null;
            string nameFilter = null;
            string pathGlobPattern = null;

            if (searchQuery.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
            {
                string remainingQuery = searchQuery.Substring(2).TrimStart();
                int firstSpaceIndex = remainingQuery.IndexOf(' ');

                if (firstSpaceIndex != -1)
                {
                    typePattern = remainingQuery.Substring(0, firstSpaceIndex).Trim();
                    string restPart = remainingQuery.Substring(firstSpaceIndex + 1).Trim();

                    // Correction: If restPart contains / or * or ?, treat it as a path Glob
                    if (restPart.Contains("/") || restPart.Contains("*") || restPart.Contains("?"))
                    {
                        pathGlobPattern = restPart; // Treat as path Glob pattern
                        nameFilter = null; // If path Glob exists, name filter is not used
                    }
                    else
                    {
                        // If it contains neither / nor Glob characters, it's a pure name filter (partial match)
                        nameFilter = restPart;
                        pathGlobPattern = null;
                    }
                }
                else
                {
                    // If there's no space after "t:" (e.g., "t:*Image")
                    typePattern = remainingQuery;
                    nameFilter = null;
                    pathGlobPattern = null;
                }
            }
            // If there's no "t:" and it contains * or ?, it's a pure path Glob pattern
            else if (searchQuery.Contains("*") || searchQuery.Contains("?"))
            {
                pathGlobPattern = searchQuery;
                typePattern = null;
                nameFilter = null;
            }
            // If there's no "t:" and no Glob, it's a pure name filter
            else
            {
                nameFilter = searchQuery;
                typePattern = null;
                pathGlobPattern = null;
            }

            return (typePattern, nameFilter, pathGlobPattern);
        }


        // Check if the type name (including base classes) of components a GameObject has matches the specified type Glob pattern
        private static bool MatchesTypeGlobPattern(GameObject obj, Regex typeRegex)
        {
            // Also check the type of GameObject itself (e.g., for t:GameObject*)
            if (typeRegex.IsMatch("GameObject"))
            {
                // If the type pattern is "GameObject" (or a pattern that matches it),
                // any non-null GameObject is always considered a match.
                if (obj != null) return true;
            }

            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                Type currentType = comp.GetType();
                while (currentType != null && currentType != typeof(object))
                {
                    // Check if the type name (simple name) matches the Glob pattern (regex)
                    if (typeRegex.IsMatch(currentType.Name))
                    {
                        return true; // Found a matching type
                    }

                    currentType = currentType.BaseType; // Check parent class
                }
            }

            return false; // No matching type found
        }

        // Helper method to convert Glob pattern to regex pattern (modified for Unity spec * and **)
        private static string GlobToRegex(string globPattern)
        {
            StringBuilder regexBuilder = new StringBuilder();
            // Add ^ to the beginning of the pattern (if not already there)
            if (string.IsNullOrEmpty(globPattern) || globPattern[0] != '^')
            {
                regexBuilder.Append('^');
            }

            // Loop with index to look ahead instead of foreach
            for (int i = 0; i < globPattern.Length; i++)
            {
                char c = globPattern[i];

                switch (c)
                {
                    case '*':
                        // Check if the next character is also * (for **)
                        if (i + 1 < globPattern.Length && globPattern[i + 1] == '*')
                        {
                            // "**" matches any string (including slashes) -> .*
                            regexBuilder.Append(".*");
                            i++; // Skip the next '*' as it's already processed
                        }
                        else
                        {
                            // "*" matches zero or more characters except slash -> [^/]*
                            regexBuilder.Append("[^/]*");
                        }

                        break;
                    case '?':
                        // "?" matches any single character except slash -> [^/]
                        regexBuilder.Append("[^/]");
                        break;
                    // Escape regex special characters (including backslash itself)
                    case '\\':
                    case '.':
                    case '+':
                    case '{':
                    case '}':
                    case '(':
                    case ')':
                    case '[':
                    case ']':
                    case '^': // Escape ^ inside the pattern
                    case '$': // Escape $ inside the pattern
                    case '|':
                        regexBuilder.Append('\\').Append(c);
                        break;
                    default:
                        // Add slashes and other characters as they are
                        regexBuilder.Append(c);
                        break;
                }
            }

            // Add $ to the end of the pattern (if not already there)
            if (string.IsNullOrEmpty(globPattern) || globPattern[globPattern.Length - 1] != '$')
            {
                regexBuilder.Append('$');
            }

            return regexBuilder.ToString();
        }


        // Method to check if a GameObject has a component of the specified type (or its derived type)
        private static bool GameObjectHasComponentType(GameObject obj, string typeToFind)
        {
            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                Type componentType = comp.GetType();
                if (IsTypeOrBaseTypeMatch(componentType, typeToFind))
                {
                    return true; // Found a matching component
                }
            }

            return false; // No matching component found
        }

        // Method to check if a type or its base class matches the specified type name
        private static bool IsTypeOrBaseTypeMatch(Type componentType, string typeToFind)
        {
            Type currentType = componentType;
            while (currentType != null && currentType != typeof(object))
            {
                // Also compare with fully qualified name (logic from user-provided code)
                if (currentType.Name.Equals(typeToFind, StringComparison.OrdinalIgnoreCase) ||
                    currentType.FullName.Equals(typeToFind, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        // Check if string contains another string, ignoring case
        public static bool ContainsIgnoreCase(string source, string searchTerm)
        {
            if (source == null || searchTerm == null) return false;

            return source.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // Get Hierarchy path of GameObject
        public static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return string.Empty; // Null check

            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }
    }
}