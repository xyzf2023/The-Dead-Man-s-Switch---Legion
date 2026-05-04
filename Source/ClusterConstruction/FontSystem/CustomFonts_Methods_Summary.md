# Custom Fonts MOD 关键方法总结

## 概述
Custom Fonts 是一个 RimWorld MOD，用于替换游戏字体为系统字体或自带字体包。该MOD通过Harmony补丁在运行时动态修改Unity的字体系统。

## 核心类结构

### FontSettings (设置数据类)
```csharp
public class FontSettings : ModSettings
{
    public const string DefaultFontName = "Default";
    public static string CurrentUIFontName;
    public static string CurrentWorldFontName;
    public static float ScaleFactor = 1.0f;
    public static int VerticalOffset = 0;
}
```
**功能**：存储字体设置数据，支持通过Verse的Scribe系统持久化保存。

### CustomFonts (主MOD类)
包含字体加载、替换和管理的核心逻辑。

---

## 字体读取方法

### 1. SetupBundledFonts()
**位置**: `CustomFonts.cs:216-236`
**功能**: 从AssetBundle加载自带字体包
```csharp
public static void SetupBundledFonts()
```
**机制**:
- 使用 `AssetBundle.LoadFromFile("rimfonts")` 加载字体资源文件
- 通过 `cab.LoadAllAssets<Font>()` 提取所有Font对象
- 存储在 `BundledFonts` 字典中，键格式为 "(Bundled) {fontName}"

**异常处理**: 加载失败时记录日志但不中断游戏运行

### 2. SetupOSInstalledFontNames()
**位置**: `CustomFonts.cs:193-199`
**功能**: 获取系统安装的字体列表
```csharp
public static void SetupOSInstalledFontNames()
```
**机制**:
- 调用Unity API `Font.GetOSInstalledFontNames()`
- 将结果转换为List并排序
- 存储在 `_fontNames` 私有字段中

### 3. SetupOSFontPaths()
**位置**: `CustomFonts.cs:201-214`
**功能**: 获取系统字体的完整路径
```csharp
public static void SetupOSFontPaths()
```
**机制**:
- 调用 `Font.GetPathsToOSFonts()` 获取字体路径
- 为每个路径创建TMP_FontAsset用于TextMeshPro
- 存储在 `OSFontPaths` 字典中，键为 "字体名 (样式名)"

---

## 字体替换方法

### 1. UpdateFont() - 重载版本
**位置**: `CustomFonts.cs:253-260`
**功能**: 更新所有字体类型的字体设置
```csharp
public static void UpdateFont()
```
**逻辑**:
1. 调用 `SetupBundledFonts()` 确保字体已加载
2. 遍历所有 `GameFont` 枚举值
3. 对每个字体类型调用私有 `UpdateFont(GameFont fontIndex)`

### 2. UpdateFont(GameFont fontIndex) - 私有方法
**位置**: `CustomFonts.cs:262-296`
**功能**: 更新指定字体类型的字体
```csharp
private static void UpdateFont(GameFont fontIndex)
```

**字体选择逻辑**:
```csharp
var isBundled = BundledFonts.ContainsKey(FontSettings.CurrentUIFontName);
Font font;

if (isBundled)
{
    font = BundledFonts[FontSettings.CurrentUIFontName];
}
else
{
    font = FontSettings.CurrentUIFontName != FontSettings.DefaultFontName
        ? Font.CreateDynamicFontFromOSFont(FontSettings.CurrentUIFontName, fontSize)
        : DefaultFonts[fontIndex];
}
```

**Unity Text组件更新**:
- `Text.fontStyles[(int)fontIndex]` - 标准文本样式
- `Text.textFieldStyles[(int)fontIndex]` - 输入框样式
- `Text.textAreaStyles[(int)fontIndex]` - 文本区域样式
- `Text.textAreaReadOnlyStyles[(int)fontIndex]` - 只读文本区域样式

**字体大小计算**:
```csharp
var fontSize = (int)Math.Round(DefaultFonts[fontIndex].fontSize * FontSettings.ScaleFactor);
```

### 3. RecalcCustomLineHeights()
**位置**: `CustomFonts.cs:298-318`
**功能**: 重新计算字体行高和垂直偏移
```csharp
public static void RecalcCustomLineHeights()
public static void RecalcCustomLineHeights(GameFont fontType)
```

**机制**:
- 设置 `contentOffset` 为垂直偏移向量 `(0f, FontSettings.VerticalOffset)`
- 通过反射修改Text组件的偏移属性

---

## Harmony补丁类 (HarmoneyPatchers)

### 1. StartOfOnGUIPatcher
**位置**: `CustomFonts.cs:325-344`
**目标**: `Text.StartOfOnGUI()`
**类型**: HarmonyPostfix

**功能**: 在游戏GUI开始时初始化字体系统
```csharp
[HarmonyPatch(typeof(Text), nameof(Text.StartOfOnGUI))]
class StartOfOnGUIPatcher
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        // 单次初始化逻辑
        CustomFonts.SetupOSInstalledFontNames();
        CustomFonts.SetupOSFontPaths();
        CustomFonts.SetupBundledFonts();
        CustomFonts.UpdateFont();
        // 设置TextMeshPro默认字体
        // 修改ForceLegacyText标志
    }
}
```

### 2. GoToMainMenuPatcher
**位置**: `CustomFonts.cs:346-354`
**目标**: `GenScene.GoToMainMenu()`
**类型**: HarmonyPostfix

**功能**: 返回主菜单时重新应用字体设置
```csharp
[HarmonyPatch(typeof(GenScene), nameof(GenScene.GoToMainMenu))]
class GoToMainMenuPatcher
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        CustomFonts.UpdateFont();
    }
}
```

### 3. WorldMapHasCharacterPatcher
**位置**: `CustomFonts.cs:356-367`
**目标**: `WorldFeatures.HasCharacter()`
**类型**: HarmonyPrefix

**功能**: 控制世界地图文字渲染方式
```csharp
[HarmonyPatch(typeof(WorldFeatures), "HasCharacter")]
class WorldMapHasCharacterPatcher
{
    [HarmonyPrefix]
    public static bool Prefix(ref bool __result)
    {
        if (CustomFonts.ForceLegacyText)
            return true;  // 跳过原方法
        __result = true;
        return false;     // 继续执行原方法
    }
}
```

### 4. WorldMapInitPatcher
**位置**: `CustomFonts.cs:369-404`
**目标**: `WorldFeatureTextMesh_TextMeshPro.Init()`
**类型**: HarmonyPrefix

**功能**: 初始化世界地图TextMeshPro字体
```csharp
[HarmonyPatch(typeof(WorldFeatureTextMesh_TextMeshPro), "Init")]
class WorldMapInitPatcher
{
    [HarmonyPrefix]
    public static void Prefix()
    {
        TMP_FontAsset fontAsset;

        // 根据设置选择字体源
        if (CustomFonts.BundledFonts.ContainsKey(FontSettings.CurrentWorldFontName))
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                CustomFonts.BundledFonts[FontSettings.CurrentWorldFontName]);
        }
        else if (FontSettings.CurrentWorldFontName == FontSettings.DefaultFontName)
        {
            fontAsset = CustomFonts.DefaultTMPFontAsset;
        }
        else
        {
            fontAsset = TMP_FontAsset.CreateFontAsset(
                new Font(CustomFonts.OSFontPaths[FontSettings.CurrentWorldFontName]));
        }

        // 应用字体到预制件
        var prefab = WorldFeatureTextMesh_TextMeshPro.WorldTextPrefab.GetComponent<TextMeshPro>();
        prefab.font = fontAsset;
        prefab.UpdateFontAsset();

        // 设置缩放因子
        AccessTools.StaticFieldRefAccess<float>(typeof(WorldFeatureTextMesh_TextMeshPro), "TextScale") =
            1.75f * FontSettings.ScaleFactor;
    }
}
```

---

## 设置管理方法

### 1. SaveFont(string fontName, bool forceUpdate = false)
**位置**: `CustomFonts.cs:238-243`
**功能**: 保存界面字体设置并应用更改

### 2. SaveWorldFont(string fontName)
**位置**: `CustomFonts.cs:245-251`
**功能**: 保存世界地图字体设置

---

## 性能优化策略

1. **单次初始化标志**: 使用 `_hasBundledFonts`、`_hasInstalledFontNames` 等布尔标志防止重复加载资源

2. **条件更新**: 只在字体设置实际改变时才执行更新操作

3. **延迟加载**: 字体资源按需加载，避免启动时大量I/O操作

4. **错误容忍**: AssetBundle加载失败时不中断游戏，自动回退到默认字体

---

## 技术特点

1. **双字体系统**: 分别处理界面字体和世界地图字体
2. **动态字体创建**: 使用 `Font.CreateDynamicFontFromOSFont()` 实现运行时字体切换
3. **TextMeshPro兼容**: 特殊处理世界地图的TextMeshPro组件
4. **反射技术**: 使用Harmony的 `AccessTools` 修改私有字段
5. **跨版本兼容**: 支持RimWorld 1.3和1.4版本

---

## 使用限制

1. **世界地图字体**: 更改后需要重新加载存档才能生效
2. **字体兼容性**: 某些系统字体可能在不同平台上显示效果不一致
3. **资源依赖**: 自带字体包通过Git LFS管理，需要完整下载
