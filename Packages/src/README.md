# {{REPOSITORY_NAME}}

## Usage


## Installation
### Unity Package Manager via

1. Open Window > Package Manager
2. Click the "+" button
3. Select "Add package from git URL"
4. Enter the following URL:
   ```
   https://github.com/hatayama/{{REPOSITORY_NAME}}.git?path=/Packages/src
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
![](https://github.com/hatayama/InspectorAutoAssigner/blob/main/Assets/Images/4.png?raw=true)

3. Open the Package Manager window, navigate to the "Masamichi Hatayama" page in the My Registries section.
![](https://github.com/hatayama/InspectorAutoAssigner/blob/main/Assets/Images/5.png?raw=true)


### Command
```bash
openupm add io.github.hatayama.{{REPOSITORY_NAME}}
```


## License

MIT License

## Author

Masamichi Hatayama