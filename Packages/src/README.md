# HierarchyFinder

## Usage

This package provides an editor window to efficiently find objects in Unity's hierarchy.

1.  **Open Window:** Select `Tools > Hierarchy Finder` from the menu bar.
2.  **Add Path:**
    *   Drag and drop a GameObject from the hierarchy to the window, and its path will be added automatically.
    *   You can also manually type or paste paths into the list fields.
    *   Search by component type is possible using the `t:ComponentName` format (e.g., `t:MeshRenderer`, `t:Image Player`).
    *   Search by name using Glob patterns (`*`, `?`) is possible (e.g., `Player*`, `Enemy?`, `A/**/Child_*`, `t: *Image`).
3.  **Execute Action:**
    *   **Ping:** Clicking the `Ping` button next to a normal path (not including `t:` or Glob patterns) will highlight (Ping) the corresponding GameObject in the hierarchy.
    *   **Paste:** Clicking the `Paste` button next to a path in `t:ComponentName` format (not including Glob patterns) will copy that search query to the hierarchy's search bar.
    *   **Search (Magnifying Glass Icon):** Clicking the search icon next to a path in `t:` format or a path containing Glob patterns (`*`, `?`) will execute the search, and the results will be displayed in a list in a popup window. If only one object is found, it will be highlighted (Pinged).

4.  **Manage List:**
    *   You can drag paths to reorder them.
    *   Click the `-` button to delete a path.
    *   The list content is automatically saved in UserSettings and restored when the window is next opened.

* This makes it easy to quickly access frequently used GameObjects or perform complex searches based on type or name patterns.

### [Display example]
WIP

### [Display of multiple selections]
WIP

## Installation
### Unity Package Manager via

1. Open Window > Package Manager
2. Click the "+" button
3. Select "Add package from git URL"
4. Enter the following URL:
   ```
   https://github.com/hatayama/HierarchyFinder.git?path=/Packages/src
   ```

### OpenUPM via

### How to use UPM with Scoped registry in Unity Package Manager
1. Open the Project Settings window and navigate to the Package Manager page.
2. Add the following entry to the Scoped Registries list:
```
Name：OpenUPM
URL: https://package.openupm.com
Scope(s)：io.github.hatayama
```

3. Open the Package Manager window, navigate to the "hatayama" page in the My Registries section.
WIP



### Command
```bash
openupm add io.github.hatayama.HierarchyFinder
```


## License

MIT License

## Author

Masamichi Hatayama
