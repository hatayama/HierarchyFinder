using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Linq;

namespace io.github.hatayama.HierarchyFinder
{
    /// <summary>
    /// Hierarchy内のGameObjectを検索するロジックを提供するクラスやで。
    /// 型名とパスの両方で Glob パターン検索に対応したで！ (Unity仕様 *, ** 準拠)
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
                return results;
            }

            GameObject[] gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // クエリを解析 (t:、名前フィルター、パスGlobを区別)
            (string typePattern, string nameFilter, string pathGlobPattern) = ParseQuery(searchQuery);

            bool isTypeSearchEnabled = !string.IsNullOrEmpty(typePattern);
            bool isNameFilterEnabled = !string.IsNullOrEmpty(nameFilter);
            bool isPathGlobEnabled = !string.IsNullOrEmpty(pathGlobPattern);

            Regex pathRegex = null;
            if (isPathGlobEnabled)
            {
                // パス Glob があれば正規表現に変換 (Unity仕様のGlobToRegexを使用)
                pathRegex = new Regex(GlobToRegex(pathGlobPattern), RegexOptions.IgnoreCase);
            }

            Regex typeRegex = null;
            bool useTypeGlob = isTypeSearchEnabled && (typePattern.Contains("*") || typePattern.Contains("?"));
            if (useTypeGlob)
            {
                // 型名 Glob があれば正規表現に変換 (Unity仕様のGlobToRegexを使用)
                // 型名には / が含まれない想定なので、* と ** の違いは影響しないはず
                typeRegex = new Regex(GlobToRegex(typePattern), RegexOptions.IgnoreCase);
            }

            Regex nameRegex = null; // 名前フィルター用の Regex を追加
            bool useNameGlob = isNameFilterEnabled && (nameFilter.Contains("*") || nameFilter.Contains("?"));
            if (useNameGlob)
            {
                // 名前フィルター Glob があれば正規表現に変換 (Unity仕様のGlobToRegexを使用)
                // 名前には / が含まれない想定なので、* と ** の違いは影響しないはず
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

                // 2. パス Glob フィルター (パスGlobが指定されている場合のみ)
                string path = null; // 必要になったら取得
                if (isPathGlobEnabled)
                {
                    path = GetGameObjectPath(obj);
                    if (!pathRegex.IsMatch(path)) continue; // パス Glob がマッチしなければ次へ
                }

                // 3. GameObject 名フィルター (名前フィルターが指定されている場合のみ)
                // ParseQueryにより、isNameFilterEnabled が true の場合、pathGlobPattern は null のはず
                if (isNameFilterEnabled)
                {
                    bool nameMatch;
                    if (useNameGlob)
                    {
                        // 名前 Glob マッチング
                        nameMatch = nameRegex.IsMatch(obj.name);
                    }
                    else
                    {
                        // 通常の部分一致
                        nameMatch = ContainsIgnoreCase(obj.name, nameFilter);
                    }

                    if (!nameMatch) continue; // 名前がマッチしなければ次へ
                }

                // フィルターを通過したら結果に追加
                if (path == null) // path がまだ取得されていなければ取得
                {
                    path = GetGameObjectPath(obj);
                }

                results.Add(new SearchResult(obj, path));
            }

            // 検索結果を Hierarchy 順序でソート
            results = results.OrderBy(result => GetHierarchySortKey(result.gameObject)).ToList();

            return results;
        }

        /// <summary>
        /// GameObject の Hierarchy 上の順序を示すソートキーを生成するで。
        /// ルートからの各階層の GetSiblingIndex() をゼロ埋めして "/" で連結した文字列や。
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
                // GetSiblingIndex() を4桁ゼロ埋めして追加
                pathParts.Push(currentTransform.GetSiblingIndex().ToString("D4"));
                currentTransform = currentTransform.parent;
            }

            // スタックから取り出して "/" で連結
            return string.Join("/", pathParts);
        }

        // クエリ文字列を解析して、型パターン、名前フィルター、パス Glob パターンを抽出するメソッド
        private static (string typePattern, string nameFilter, string pathGlobPattern) ParseQuery(string searchQuery)
        {
            // NOTE: この解析ロジックは、t: の後が名前フィルターかパスGlobか を / の有無だけで判断している。
            // → 修正： / がなくても * or ? があればパス Glob とみなすように変更
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

                    // 修正点: restPart に / が含まれるか、* または ? が含まれていればパス Glob とする
                    if (restPart.Contains("/") || restPart.Contains("*") || restPart.Contains("?"))
                    {
                        pathGlobPattern = restPart; // パス Glob パターンとして扱う
                        nameFilter = null; // パス Glob があれば名前フィルターは使わない
                    }
                    else
                    {
                        // / も Glob 文字も含まない場合は、純粋な名前フィルター (部分一致)
                        nameFilter = restPart;
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
                nameFilter = null;
            }
            // t: もなく、Glob もない場合は純粋な名前フィルター
            else
            {
                nameFilter = searchQuery;
                typePattern = null;
                pathGlobPattern = null;
            }

            return (typePattern, nameFilter, pathGlobPattern);
        }


        // GameObject が持つコンポーネントの型名（基底クラス含む）が指定された型 Glob パターンにマッチするかチェック
        private static bool MatchesTypeGlobPattern(GameObject obj, Regex typeRegex)
        {
            // GameObject 自体の型もチェック (例: t:GameObject* の場合)
            if (typeRegex.IsMatch("GameObject"))
            {
                // 型パターンが "GameObject" (またはそれにマッチするパターン) なら、
                // null でない GameObject は常にマッチするとみなす
                if (obj != null) return true;
            }

            Component[] components = obj.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (comp == null) continue;

                Type currentType = comp.GetType();
                while (currentType != null && currentType != typeof(object))
                {
                    // 型名 (シンプル名) が Glob パターン (正規表現) にマッチするかチェック
                    if (typeRegex.IsMatch(currentType.Name))
                    {
                        return true; // 一致する型が見つかった
                    }

                    currentType = currentType.BaseType; // 親クラスをチェック
                }
            }

            return false; // 一致する型が見つからなかった
        }

        // Glob パターンを正規表現パターンに変換するヘルパーメソッドや (Unity 仕様 * と ** に合わせた修正)
        private static string GlobToRegex(string globPattern)
        {
            StringBuilder regexBuilder = new StringBuilder();
            // パターンの先頭に ^ を追加 (既についていなければ)
            if (string.IsNullOrEmpty(globPattern) || globPattern[0] != '^')
            {
                regexBuilder.Append('^');
            }

            // foreach ではなくインデックスでループして先読みする
            for (int i = 0; i < globPattern.Length; i++)
            {
                char c = globPattern[i];

                switch (c)
                {
                    case '*':
                        // 次の文字も * かどうかチェック (**) の場合
                        if (i + 1 < globPattern.Length && globPattern[i + 1] == '*')
                        {
                            // "**" は任意の文字列 (スラッシュ含む) にマッチ -> .*
                            regexBuilder.Append(".*");
                            i++; // 次の '*' も処理済みとしてスキップ
                        }
                        else
                        {
                            // "*" はスラッシュ以外の0文字以上にマッチ -> [^/]*
                            regexBuilder.Append("[^/]*");
                        }

                        break;
                    case '?':
                        // "?" はスラッシュ以外の任意の一文字 -> [^/]
                        regexBuilder.Append("[^/]");
                        break;
                    // 正規表現の特殊文字をエスケープ (バックスラッシュ自体を含む)
                    case '\\':
                    case '.':
                    case '+':
                    case '{':
                    case '}':
                    case '(':
                    case ')':
                    case '[':
                    case ']':
                    case '^': // パターン内部の ^ はエスケープ
                    case '$': // パターン内部の $ はエスケープ
                    case '|':
                        regexBuilder.Append('\\').Append(c);
                        break;
                    default:
                        // スラッシュや他の文字はそのまま追加
                        regexBuilder.Append(c);
                        break;
                }
            }

            // パターンの末尾に $ を追加 (既についていなければ)
            if (string.IsNullOrEmpty(globPattern) || globPattern[globPattern.Length - 1] != '$')
            {
                regexBuilder.Append('$');
            }

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
                // 完全修飾名でも比較 (ユーザー提供コードのロジック)
                if (currentType.Name.Equals(typeToFind, StringComparison.OrdinalIgnoreCase) ||
                    currentType.FullName.Equals(typeToFind, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                currentType = currentType.BaseType;
            }

            return false;
        }

        // 大文字小文字を無視して文字列が含まれるかチェック
        public static bool ContainsIgnoreCase(string source, string searchTerm)
        {
            if (source == null || searchTerm == null) return false;

            return source.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // GameObjectのHierarchyパスを取得
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