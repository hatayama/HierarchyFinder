using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor; // FindObjectsByType のために必要
using System.Text; // StringBuilder のために必要

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// Hierarchy内のGameObjectを検索するロジックを提供するクラスやで。
    /// 型名とパスの両方で Glob パターン検索に対応したで！
    /// </summary>
    public static class HierarchySearchLogic
    {
        // 検索結果を保持するためのクラス
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
                return results; // クエリが空なら何も返さん
            }

            GameObject[] gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // クエリを解析 (t: と Glob パターンを考慮)
            (string typePattern, string nameFilter, string pathGlobPattern) = ParseQuery(searchQuery);

            bool isTypeSearchEnabled = !string.IsNullOrEmpty(typePattern);
            bool isNameFilterEnabled = !string.IsNullOrEmpty(nameFilter);
            bool isPathGlobEnabled = !string.IsNullOrEmpty(pathGlobPattern);

            Regex pathRegex = null;
            if (isPathGlobEnabled)
            {
                // パス Glob があれば正規表現に変換
                pathRegex = new Regex(GlobToRegex(pathGlobPattern), RegexOptions.IgnoreCase);
            }

            Regex typeRegex = null;
            bool useTypeGlob = isTypeSearchEnabled && (typePattern.Contains("*") || typePattern.Contains("?"));
            if (useTypeGlob)
            {
                // 型名 Glob があれば正規表現に変換
                typeRegex = new Regex(GlobToRegex(typePattern), RegexOptions.IgnoreCase);
            }

            foreach (GameObject obj in gameObjects)
            {
                // 1. 型フィルター
                if (isTypeSearchEnabled)
                {
                    bool typeMatch;
                    if (useTypeGlob)
                    {
                        // 型名 Glob マッチング
                        typeMatch = MatchesTypeGlobPattern(obj, typeRegex);
                    }
                    else
                    {
                        // 通常の型名マッチング (GameObject 特殊ケース含む)
                        typeMatch = typePattern.Equals("GameObject", StringComparison.OrdinalIgnoreCase) || GameObjectHasComponentType(obj, typePattern);
                    }
                    if (!typeMatch) continue; // 型がマッチしなければ次へ
                }

                // 2. パス Glob フィルター
                string path = GetGameObjectPath(obj); // パスは必ず取得
                if (isPathGlobEnabled)
                {
                    if (!pathRegex.IsMatch(path)) continue; // パス Glob がマッチしなければ次へ
                }

                // 3. GameObject 名フィルター (t: が指定されている時か、パスGlobがない時のみ有効)
                if (isNameFilterEnabled && (isTypeSearchEnabled || !isPathGlobEnabled))
                {
                     if (!ContainsIgnoreCase(obj.name, nameFilter)) continue; // 名前がマッチしなければ次へ
                }
                // 4. t: なし、パス Glob なしの場合 (名前フィルターをパス Glob の代わりに使う)
                else if (!isTypeSearchEnabled && !isPathGlobEnabled)
                {
                    // この場合、searchQuery 全体が名前フィルターのように振る舞う
                    // ただし、`HierarchyFinderWindow` 側で `GameObject.Find` を使っているため、
                    // ここで完全一致などのロジックを入れるかは要検討。
                    // 一旦、ここでは何もしない (上の条件で弾かれなければ通す)
                    // より厳密にするなら searchQuery == obj.name など？
                }


                // 全てのフィルターを通過したら結果に追加
                results.Add(new SearchResult(obj, path));
            }

            return results;
        }

        // クエリ文字列を解析して、型パターン、名前フィルター、パス Glob パターンを抽出するメソッドや
        private static (string typePattern, string nameFilter, string pathGlobPattern) ParseQuery(string searchQuery)
        {
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
                    typePattern = remainingQuery.Substring(0, firstSpaceIndex).Trim(); // 型パターンを取得
                    string pathOrNamePart = remainingQuery.Substring(firstSpaceIndex + 1).Trim(); // 残りの部分を取得

                    // 残りの部分に Glob が含まれているかチェック
                    if (pathOrNamePart.Contains("*") || pathOrNamePart.Contains("?"))
                    {
                        pathGlobPattern = pathOrNamePart; // Glob があればパス Glob パターンとして扱う
                        nameFilter = null; // この場合、名前フィルターは無し
                    }
                    else
                    {
                        nameFilter = pathOrNamePart; // Glob がなければ名前フィルターとして扱う
                        pathGlobPattern = null;
                    }
                }
                else
                {
                    // t: の後にスペースがない場合 (例: "t:*Image")
                    typePattern = remainingQuery;
                    nameFilter = null;
                    pathGlobPattern = null;
                }
            }
            // t: がなく、* か ? を含む場合は純粋なパス Glob パターン
            else if (searchQuery.Contains("*") || searchQuery.Contains("?"))
            {
                pathGlobPattern = searchQuery;
                typePattern = null;
                nameFilter = null; // 純粋なパス Glob の場合、名前フィルターは無し
            }
            // t: もなく、Glob もない場合は純粋な名前フィルター
            else
            {
                 nameFilter = searchQuery;
                 typePattern = null;
                 pathGlobPattern = null;
            }

            // 以前の TODO はこの修正で解決されるはずや
            return (typePattern, nameFilter, pathGlobPattern);
        }

        // GameObject が持つコンポーネントの型名（基底クラス含む）が指定された型 Glob パターンにマッチするかチェック
        private static bool MatchesTypeGlobPattern(GameObject obj, Regex typeRegex)
        {
            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                Type currentType = comp.GetType();
                while (currentType != null && currentType != typeof(object))
                {
                    // 型名が Glob パターン (正規表現に変換済み) にマッチするかチェック
                    if (typeRegex.IsMatch(currentType.Name))
                    {
                        return true; // 一致する型が見つかった
                    }
                    currentType = currentType.BaseType; // 親クラスをチェック
                }
            }
            return false; // 一致する型が見つからなかった
        }

        // Glob パターンを正規表現パターンに変換するヘルパーメソッドや
        private static string GlobToRegex(string globPattern)
        {
            StringBuilder regexBuilder = new StringBuilder();
            regexBuilder.Append('^');

            foreach (char c in globPattern)
            {
                switch (c)
                {
                    case '*':
                        regexBuilder.Append(".*");
                        break;
                    case '?':
                        regexBuilder.Append('.');
                        break;
                    case '\\':
                    case '.':
                    case '+':
                    case '{':
                    case '}':
                    case '(':
                    case ')':
                    case '[':
                    case ']':
                    case '^':
                    case '$':
                    case '|':
                        regexBuilder.Append('\\').Append(c);
                        break;
                    default:
                        regexBuilder.Append(c);
                        break;
                }
            }

            regexBuilder.Append('$');
            return regexBuilder.ToString();
        }

        // GameObjectが指定された型のコンポーネント（またはその派生型）を持つかチェックするメソッド
        private static bool GameObjectHasComponentType(GameObject obj, string typeToFind)
        {
            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                Type componentType = comp.GetType();
                if (IsTypeOrBaseTypeMatch(componentType, typeToFind))
                {
                    return true; // 一致するコンポーネントが見つかった
                }
            }
            return false; // 一致するコンポーネントが見つからなかった
        }

        // 型またはその基底クラスが指定された型名と一致するかチェックするメソッド
        private static bool IsTypeOrBaseTypeMatch(Type componentType, string typeToFind)
        {
            Type currentType = componentType;
            while (currentType != null && currentType != typeof(object))
            {
                string currentTypeName = currentType.Name;

                if (currentTypeName.Equals(typeToFind, StringComparison.OrdinalIgnoreCase) ||
                    currentTypeName.EndsWith("." + typeToFind, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
                currentType = currentType.BaseType;
            }
            return false;
        }

        public static bool ContainsIgnoreCase(string source, string searchTerm)
        {
            if (source == null || searchTerm == null) return false;

            return source.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // GameObjectのHierarchyパスを取得
        public static string GetGameObjectPath(GameObject obj)
        {
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