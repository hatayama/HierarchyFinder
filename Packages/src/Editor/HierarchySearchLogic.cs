using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// Hierarchy内のGameObjectを検索するためのロジックを提供する
    /// 型名とパスの両方に対してGlobパターン検索をサポート（Unity仕様の *, ** に準拠）
    /// </summary>
    public static class HierarchySearchLogic
    {
        // 検索結果を保持するクラス
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

            // クエリを解析（t:、名前フィルター、パスGlobを区別）
            (string typePattern, string nameFilter, string pathGlobPattern) = ParseQuery(searchQuery);

            bool isTypeSearchEnabled = !string.IsNullOrEmpty(typePattern);
            bool isNameFilterEnabled = !string.IsNullOrEmpty(nameFilter);
            bool isPathGlobEnabled = !string.IsNullOrEmpty(pathGlobPattern);

            Regex pathRegex = null;
            if (isPathGlobEnabled)
            {
                // パスGlobが存在する場合、正規表現に変換（UnityのGlobToRegexを使用）
                pathRegex = new Regex(GlobToRegex(pathGlobPattern), RegexOptions.IgnoreCase);
            }

            Regex typeRegex = null;
            bool useTypeGlob = isTypeSearchEnabled && (typePattern.Contains("*") || typePattern.Contains("?"));
            if (useTypeGlob)
            {
                // 型名Globが存在する場合、正規表現に変換（UnityのGlobToRegexを使用）
                // 型名には / が含まれない想定なので、* と ** の違いは問題にならないはず
                typeRegex = new Regex(GlobToRegex(typePattern), RegexOptions.IgnoreCase);
            }

            Regex nameRegex = null; // 名前フィルター用の正規表現を追加
            bool useNameGlob = isNameFilterEnabled && (nameFilter.Contains("*") || nameFilter.Contains("?"));
            if (useNameGlob)
            {
                // 名前フィルターGlobが存在する場合、正規表現に変換（UnityのGlobToRegexを使用）
                // 名前には / が含まれない想定なので、* と ** の違いは問題にならないはず
                nameRegex = new Regex(GlobToRegex(nameFilter), RegexOptions.IgnoreCase);
            }


            foreach (GameObject obj in gameObjects)
            {
                // 1. 型フィルター
                if (isTypeSearchEnabled)
                {
                    bool typeMatch;
                    if (useTypeGlob)
                    {
                        // 型名Globマッチング
                        typeMatch = MatchesTypeGlobPattern(obj, typeRegex);
                    }
                    else
                    {
                        // 通常の型名マッチング（GameObjectの特殊ケースを含む）
                        typeMatch = typePattern.Equals("GameObject", StringComparison.OrdinalIgnoreCase) || GameObjectHasComponentType(obj, typePattern);
                    }

                    if (!typeMatch) continue; // 型が一致しない場合は次に進む
                }

                // 2. パスGlobフィルター（パスGlobが指定されている場合のみ）
                string path = null; // 必要に応じて取得
                if (isPathGlobEnabled)
                {
                    path = GetGameObjectPath(obj);
                    if (!pathRegex.IsMatch(path)) continue; // パスGlobが一致しない場合は次に進む
                }

                // 3. GameObject名フィルター（名前フィルターが指定されている場合のみ）
                // ParseQueryによると、isNameFilterEnabledがtrueの場合、pathGlobPatternはnullのはず
                if (isNameFilterEnabled)
                {
                    bool nameMatch;
                    if (useNameGlob)
                    {
                        // 名前Globマッチング
                        nameMatch = nameRegex.IsMatch(obj.name);
                    }
                    else
                    {
                        // 通常の部分一致
                        nameMatch = ContainsIgnoreCase(obj.name, nameFilter);
                    }

                    if (!nameMatch) continue; // 名前が一致しない場合は次に進む
                }

                // フィルターを通過した場合、結果に追加
                if (path == null) // pathがまだ取得されていない場合は取得する
                {
                    path = GetGameObjectPath(obj);
                }

                results.Add(new SearchResult(obj, path));
            }

            // 検索結果をHierarchy順でソート
            results = results.OrderBy(result => GetHierarchySortKey(result.gameObject)).ToList();

            return results;
        }

        /// <summary>
        /// GameObjectのHierarchyにおける順序を表すソートキーを生成する
        /// ルートからの各階層のGetSiblingIndex()をゼロ埋めし、"/"で結合した文字列になる
        /// 例: "0003/0001/0005"
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

        // クエリ文字列を解析し、型パターン、名前フィルター、パスGlobパターンを抽出するメソッド
        private static (string typePattern, string nameFilter, string pathGlobPattern) ParseQuery(string searchQuery)
        {
            // 注意: この解析ロジックでは、"t:"の後に続くものが名前フィルターかパスGlobかを
            // / の有無のみで判断している
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

                    // 修正: restPartが / または * または ? を含む場合、パスGlobとして扱う
                    if (restPart.Contains("/") || restPart.Contains("*") || restPart.Contains("?"))
                    {
                        pathGlobPattern = restPart; // パスGlobパターンとして扱う
                        nameFilter = null; // パスGlobが存在する場合、名前フィルターは使用しない
                    }
                    else
                    {
                        // / もGlob文字も含まない場合、純粋な名前フィルター（部分一致）となる
                        nameFilter = restPart;
                        pathGlobPattern = null;
                    }
                }
                else
                {
                    // "t:" の後にスペースがない場合（例: "t:*Image"）
                    typePattern = remainingQuery;
                    nameFilter = null;
                    pathGlobPattern = null;
                }
            }
            
            else if (searchQuery.Contains("/") || searchQuery.Contains("*") || searchQuery.Contains("?"))
            {
                // "t:" で始まらず、"/" か Glob文字 を含む場合は、パスGlobパターンとみなす
                pathGlobPattern = searchQuery;
                nameFilter = null;
                typePattern = null;
            }
            else if (!string.IsNullOrEmpty(searchQuery)) // 空でないことを確認
            {
                // "t:" で始まらず、"/" も Glob文字 も含まない場合は、名前フィルターとみなす
                nameFilter = searchQuery;
                pathGlobPattern = null;
                typePattern = null;
            }

            return (typePattern, nameFilter, pathGlobPattern);
        }

        // GameObjectが指定された型のコンポーネントを持つかどうかをチェック（大文字・小文字を区別しない）
        private static bool GameObjectHasComponentType(GameObject obj, string typeToFind)
        {
            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                Type componentType = comp.GetType();
                if (IsTypeOrBaseTypeMatch(componentType, typeToFind))
                {
                    return true; // 一致するコンポーネントを発見
                }
            }

            return false; // 一致するコンポーネントは見つからず
        }

        // 型またはその基底クラスが指定された型名と一致するかどうかをチェックするメソッド
        private static bool IsTypeOrBaseTypeMatch(Type componentType, string typeToFind)
        {
            Type currentType = componentType;
            while (currentType != null && currentType != typeof(object))
            {
                // 完全修飾名とも比較（ユーザー提供コードのロジックから）
                if (currentType.Name.Equals(typeToFind, StringComparison.OrdinalIgnoreCase) ||
                    currentType.FullName.Equals(typeToFind, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        // 文字列が別の文字列を含むかどうかを、大文字・小文字を無視してチェック
        public static bool ContainsIgnoreCase(string source, string searchTerm)
        {
            if (source == null || searchTerm == null) return false;

            return source.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // GameObjectのHierarchyパスを取得
        public static string GetGameObjectPath(GameObject obj)
        {
            if (obj == null) return string.Empty; // Nullチェック

            string path = obj.name;
            Transform parent = obj.transform.parent;

            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        // Globパターンを正規表現パターンに変換するメソッド（Unity仕様の * と ** に合わせて修正）
        private static string GlobToRegex(string globPattern)
        {
            StringBuilder regexBuilder = new StringBuilder();
            // パターンの先頭に ^ を追加（既になければ）
            if (string.IsNullOrEmpty(globPattern) || globPattern[0] != '^')
            {
                regexBuilder.Append('^');
            }

            // foreachではなく、先読みするためにインデックスでループ
            for (int i = 0; i < globPattern.Length; i++)
            {
                char c = globPattern[i];

                switch (c)
                {
                    case '*':
                        // 次の文字も * かどうかチェック（** のため）
                        if (i + 1 < globPattern.Length && globPattern[i + 1] == '*')
                        {
                            // "**" は任意の文字列（スラッシュを含む）に一致 -> .*
                            regexBuilder.Append(".*");
                            i++; // 次の '*' は処理済みなのでスキップ
                        }
                        else
                        {
                            // "*" はスラッシュを除く0文字以上の文字に一致 -> [^/]*
                            regexBuilder.Append("[^/]*");
                        }

                        break;
                    case '?':
                        // "?" はスラッシュを除く任意の1文字に一致 -> [^/]
                        regexBuilder.Append("[^/]");
                        break;
                    // 正規表現の特殊文字をエスケープ（バックスラッシュ自体も含む）
                    case '\\':
                    case '.':
                    case '+':
                    case '{':
                    case '}':
                    case '(':
                    case ')':
                    case '[':
                    case ']':
                    case '^': // パターン内の ^ をエスケープ
                    case '$': // パターン内の $ をエスケープ
                    case '|':
                        regexBuilder.Append('\\').Append(c);
                        break;
                    default:
                        // Add slashes and other characters as they are
                        regexBuilder.Append(c);
                        break;
                }
            }

            // パターンの末尾に $ を追加（既になければ）
            if (string.IsNullOrEmpty(globPattern) || globPattern[globPattern.Length - 1] != '$')
            {
                regexBuilder.Append('$');
            }

            return regexBuilder.ToString();
        }

        // GameObjectが持つコンポーネントの型名（基底クラスを含む）が、指定された型Globパターンに一致するかどうかをチェック
        private static bool MatchesTypeGlobPattern(GameObject obj, Regex typeRegex)
        {
            // GameObject自体の型もチェック（例: t:GameObject* の場合）
            if (typeRegex.IsMatch("GameObject"))
            {
                // 型パターンが "GameObject"（またはそれに一致するパターン）の場合、
                // nullでないGameObjectは常に一致とみなす。
                if (obj != null) return true;
            }

            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                Type currentType = comp.GetType();
                while (currentType != null && currentType != typeof(object))
                {
                    // 型名（単純名）がGlobパターン（正規表現）に一致するかチェック
                    if (typeRegex.IsMatch(currentType.Name))
                    {
                        return true; // 一致する型を発見
                    }

                    currentType = currentType.BaseType; // 親クラスをチェック
                }
            }

            return false; // 一致する型は見つからず
        }
    }
}