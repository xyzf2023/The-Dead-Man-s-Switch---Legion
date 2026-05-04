# 空中支援系统 - 效果组件开发指南

## 重要提醒：效果组件接口规范

### 参数要求

**所有效果组件必须严格遵循标准接口签名，即使不需要某些参数也要接收。**

#### 自定义直线支援效果组件

所有自定义直线支援效果组件必须提供以下静态方法：

```csharp
public static void UpdateDuringFlight(
    CustomLineFlight flight,           // 必须接收（即使不使用）
    float progress,                     // 必须接收（即使不使用）
    float startProgress,                 // 必须接收（即使不使用）
    float endProgress,                  // 必须接收（即使不使用）
    AerialSupportTypeDef supportType,   // 必须接收（即使不使用）
    Map map,                            // 必须接收（即使不使用）
    CompProperties_AerialSupportEffect_XXX props,  // 必须接收
    ref [状态字段类型] stateField       // 根据需求接收
)
```

#### 普通支援和多点支援效果组件

所有普通支援和多点支援效果组件必须提供以下静态方法：

```csharp
public static void ExecuteEffect(
    IntVec3 targetPos,                  // 必须接收（即使不使用）
    AerialSupportTypeDef supportType,   // 必须接收（即使不使用）
    Map map,                            // 必须接收（即使不使用）
    CompProperties_AerialSupportEffect_XXX props  // 必须接收
)
```

### 为什么必须接收所有参数？

1. **反射调用要求**：框架使用反射动态调用效果组件，参数数量、类型、顺序必须完全匹配
2. **参数不匹配会报错**：如果方法签名与标准接口不匹配，会抛出 `TargetParameterCountException` 或 `ArgumentException`
3. **统一接口规范**：所有效果组件遵循相同接口，便于维护和扩展

### 如果不需要某些参数怎么办？

**仍然要接收，但不使用即可。**

```csharp
public static void UpdateDuringFlight(
    CustomLineFlight flight,      // 接收但不使用
    float progress,                // 接收但不使用
    float startProgress,           // 接收但不使用
    float endProgress,             // 接收但不使用
    AerialSupportTypeDef supportType,  // 接收但不使用
    Map map,                       // 需要使用
    CompProperties_AerialSupportEffect_XXX props,  // 需要使用
    ref List<IntVec3> cachedPositions  // 需要使用
)
{
    // 只使用需要的参数
    // 不使用其他参数不会报错，只是浪费了参数传递（开销可忽略）
}
```

### 常见错误

❌ **错误示例1：缺少参数**
```csharp
// 错误：缺少 progress 参数
public static void UpdateDuringFlight(
    CustomLineFlight flight,
    // progress 缺失
    float startProgress,
    ...
)
// 结果：TargetParameterCountException
```

❌ **错误示例2：参数类型错误**
```csharp
// 错误：progress 类型应该是 float，不是 int
public static void UpdateDuringFlight(
    CustomLineFlight flight,
    int progress,  // 应该是 float
    ...
)
// 结果：ArgumentException
```

❌ **错误示例3：参数顺序错误**
```csharp
// 错误：参数顺序与标准接口不一致
public static void UpdateDuringFlight(
    float progress,  // 应该在 flight 之后
    CustomLineFlight flight,
    ...
)
// 结果：参数值错误或类型转换异常
```

✅ **正确示例：遵循标准接口**
```csharp
// 正确：完全遵循标准接口签名
public static void UpdateDuringFlight(
    CustomLineFlight flight,
    float progress,
    float startProgress,
    float endProgress,
    AerialSupportTypeDef supportType,
    Map map,
    CompProperties_AerialSupportEffect_XXX props,
    ref List<IntVec3> cachedPositions
)
{
    // 实现效果逻辑
}
```

### 异常处理

框架已实现异常捕获机制：

```csharp
try
{
    updateMethod.Invoke(null, methodArgs);
}
catch (Exception ex)
{
    Log.Error($"[空中支援] 调用效果组件失败: {compProps.GetType().Name} - {ex.Message}");
}
```

如果参数不匹配：
- 会抛出异常并被捕获
- 记录错误日志
- 跳过该效果组件，不影响其他效果
- **不会导致整个系统崩溃**

### 总结

- ✅ **必须遵循标准接口签名**
- ✅ **即使不需要某些参数也要接收**
- ✅ **不使用的参数不会报错，只是不会被使用**
- ❌ **参数不匹配会导致反射调用失败**

### 相关文档

- 自定义直线支援接口规范：`自定义直线支援解耦计划.md`
- 普通支援和多点支援接口规范：`普通和多点支援反射重构计划.md`

---

**最后更新**：2024年（根据实际日期更新）
