# 空中支援XML定义使用指南

本文档详细说明了空中支援系统的XML配置方法，包括所有支援类型的基础配置、多目标支援的时间间隔配置等。

## 目录
- [基础配置](#基础配置)
- [支援类型](#支援类型)
- [多目标支援配置](#多目标支援配置)
- [支援起始方向配置](#支援起始方向配置)
- [延迟配置](#延迟配置)
- [效果组件](#效果组件)
- [完整示例](#完整示例)

## 基础配置

所有空中支援类型都继承自 `AerialSupportTypeDef`，具有以下基础属性：

### 基本信息
```xml
<DMS_Legion.GroundSupport.AerialSupportTypeDef>
  <defName>DMSL_AerialSupport_Example</defName>        <!-- 唯一标识符 -->
  <label>示例支援</label>                              <!-- 显示名称 -->
  <description>这是一个示例空中支援的描述</description> <!-- 详细描述 -->
</DMS_Legion.GroundSupport.AerialSupportTypeDef>
```

### 视觉和音频配置
```xml
<aircraftTexturePath>GroundSupport/LASB-25</aircraftTexturePath>  <!-- 飞机贴图路径 -->
<drawSize>10.0</drawSize>                                         <!-- 贴图绘制大小 -->
<appearSoundDef>AerialSupport_Normal</appearSoundDef>             <!-- 出现音效定义 -->
```

### 飞行配置
```xml
<flightSpeed>0.01</flightSpeed>              <!-- 飞行速度（每tick前进距离） -->
<continueDrawingAfterArrival>true</continueDrawingAfterArrival>  <!-- 到达后是否继续绘制 -->
<flightPathType>Normal</flightPathType>      <!-- 飞行路径类型 -->
```

### 冷却时间
```xml
<cooldownTicks>0</cooldownTicks>  <!-- 冷却时间（ticks），0表示无冷却 -->
```

## 支援类型

### Normal（普通支援）
标准的单目标点支援，飞机从地图边缘飞向目标点并执行效果。

```xml
<flightPathType>Normal</flightPathType>
<!-- 可选：指定支援起始方向 -->
<startDirection>North</startDirection>
<!-- Normal类型不需要额外的路径配置 -->
```

### CustomLine（自定义直线）
玩家选择两个点，飞机沿这两个点构成的延长直线飞行并执行效果。

```xml
<flightPathType>CustomLine</flightPathType>
<!-- CustomLine类型也不需要额外配置，由玩家选择决定 -->
```

### MultiTarget（多目标支援）
玩家选择多个目标点，飞机依次对每个目标点执行完整的支援流程。

```xml
<flightPathType>MultiTarget</flightPathType>
<selectionPointCount>3</selectionPointCount>  <!-- 选择的目标点数量 -->
<!-- 时间间隔配置详见下文 -->
```

## 多目标支援配置

多目标支援需要配置选择的目标点数量和间隔时间：

```xml
<selectionPointCount>3</selectionPointCount>      <!-- 选择的目标点数量 -->
<selectionIntervalFrames>60</selectionIntervalFrames>  <!-- 每个目标之间的间隔时间 -->
```

## 支援起始方向配置

Normal和MultiTarget支援支持指定飞机从哪个地图边缘开始绘制：

### startDirection
指定支援的起始方向，可选值：
- `"Random"` 或 空值：随机选择边缘（默认）
- `"North"`：从地图北边（上方）进入
- `"South"`：从地图南边（下方）进入
- `"East"`：从地图东边（右方）进入
- `"West"`：从地图西边（左方）进入

### preferNorthEntry
东/西进入时的优先北部位置设置（仅当startDirection为"East"或"West"时有效）

如果设置为true，则从东边或西边进入时，起点位置的z坐标将选择目标点z坐标以上的区域，这样可以让飞机从目标点的北边进入，提供更好的视觉效果和更长的飞行路径。

```xml
<!-- 随机选择边缘（默认行为） -->
<startDirection>Random</startDirection>

<!-- 从东边进入，且优先选择目标点北边的位置 -->
<startDirection>East</startDirection>
<preferNorthEntry>true</preferNorthEntry>
```

## 延迟配置

延迟配置允许你控制飞机绘制和音效播放的时机，两者完全独立，都以飞行实例创建时间为基准点。

### renderDelayTicks（绘制延迟）

从飞行实例创建到开始绘制的帧数（ticks）。

- **默认值**：`0`（立即开始绘制）
- **单位**：游戏 ticks（60 ticks = 1秒）
- **推荐范围**：0 ~ 360000 ticks（0 ~ 100分钟）

```xml
<renderDelayTicks>10</renderDelayTicks>  <!-- 延迟10 ticks后开始绘制 -->
```

### soundDelayTicks（音效延迟）

从飞行实例创建到播放音效的帧数（ticks）。

- **默认值**：`0`（立即播放音效）
- **单位**：游戏 ticks（60 ticks = 1秒）
- **推荐范围**：0 ~ 360000 ticks（0 ~ 100分钟）

```xml
<soundDelayTicks>5</soundDelayTicks>  <!-- 延迟5 ticks后播放音效 -->
```

### 延迟配置示例

#### 声音先播放，然后绘制
```xml
<renderDelayTicks>10</renderDelayTicks>  <!-- 10 ticks后开始绘制 -->
<soundDelayTicks>5</soundDelayTicks>    <!-- 5 ticks后播放音效 -->
```

#### 同时开始
```xml
<renderDelayTicks>10</renderDelayTicks>  <!-- 10 ticks后开始绘制 -->
<soundDelayTicks>10</soundDelayTicks>    <!-- 10 ticks后播放音效 -->
```

#### 绘制先开始，声音后播放
```xml
<renderDelayTicks>5</renderDelayTicks>    <!-- 5 ticks后开始绘制 -->
<soundDelayTicks>15</soundDelayTicks>    <!-- 15 ticks后播放音效 -->
```

#### 立即执行（默认行为）
```xml
<renderDelayTicks>0</renderDelayTicks>   <!-- 立即开始绘制 -->
<soundDelayTicks>0</soundDelayTicks>     <!-- 立即播放音效 -->
<!-- 或者直接省略这两个字段，默认值就是0 -->
```

### 延迟配置注意事项

1. **基准点**：所有延迟都以飞行实例创建时间为基准点，不是以绘制开始时间为基准
2. **独立控制**：绘制延迟和音效延迟完全独立，可以任意组合
3. **性能影响**：即使填写很大的值（如60000 ticks），性能影响也极小
4. **技术限制**：理论上可填写到 2,147,483,647，但建议不超过 360000 ticks（100分钟）

## 效果组件

空中支援的效果通过 `effectComps` 配置，支持以下组件：

### 轰炸效果
```xml
<li Class="DMS_Legion.GroundSupport.SupportEffects.CompProperties_AerialSupportEffect_Bombing">
  <explosionRadius>10</explosionRadius>          <!-- 爆炸半径 -->
  <damageAmount>50</damageAmount>                <!-- 伤害值 -->
  <explosionCount>5</explosionCount>             <!-- 爆炸数量 -->
  <targetAreaRadius>5</targetAreaRadius>         <!-- 目标区域半径 -->
  <explosionsPerTick>1</explosionsPerTick>       <!-- 每tick爆炸数量 -->
  <explosionIntervalSeconds>0.2</explosionIntervalSeconds>  <!-- 爆炸间隔 -->
  <damageDef>Bomb</damageDef>                    <!-- 伤害类型 -->
</li>
```

### 消息效果
```xml
<li Class="DMS_Legion.GroundSupport.SupportEffects.CompProperties_AerialSupportEffect_Message">
  <message>轰炸已到达目标位置</message>  <!-- 显示的消息 -->
</li>
```

### 自定义直线轰炸
```xml
<li Class="DMS_Legion.GroundSupport.SupportEffects.CompProperties_AerialSupportEffect_CustomLineBombing">
  <explosionCount>8</explosionCount>      <!-- 爆炸数量 -->
  <damageAmount>50</damageAmount>         <!-- 伤害值 -->
  <explosionRadius>15</explosionRadius>   <!-- 爆炸半径 -->
  <damageDef>Bomb</damageDef>             <!-- 伤害类型 -->
</li>
```

## 尾气效果配置

可选的飞机尾气视觉效果：

```xml
<enableExhaust>true</enableExhaust>              <!-- 是否启用尾气 -->
<exhaustSpawnRate>1.0</exhaustSpawnRate>         <!-- 每tick生成概率 -->
<exhaustParticlesPerTick>30</exhaustParticlesPerTick>  <!-- 每tick粒子数 -->
<exhaustBaseScale>3</exhaustBaseScale>           <!-- 基础缩放 -->
<exhaustMinSpeed>0.7</exhaustMinSpeed>           <!-- 扩散最小速度 -->
<exhaustMaxSpeed>1.1</exhaustMaxSpeed>           <!-- 扩散最大速度 -->
<exhaustAngleVariance>4</exhaustAngleVariance>   <!-- 角度扰动范围 -->
<exhaustRotationRange>10</exhaustRotationRange>  <!-- 旋转范围 -->
```

## 完整示例

### 普通支援示例
```xml
<DMS_Legion.GroundSupport.AerialSupportTypeDef>
  <defName>DMSL_AerialSupport_Bombing</defName>
  <label>空中轰炸</label>
  <description>召唤空中支援，对指定地点进行猛烈轰炸。</description>
  <aircraftTexturePath>GroundSupport/LASB-25</aircraftTexturePath>
  <flightSpeed>0.01</flightSpeed>
  <cooldownTicks>0</cooldownTicks>
  <drawSize>10.0</drawSize>
  <appearSoundDef>AerialSupport_Normal</appearSoundDef>
  <continueDrawingAfterArrival>true</continueDrawingAfterArrival>
  <flightPathType>Normal</flightPathType>
  <renderDelayTicks>0</renderDelayTicks>
  <soundDelayTicks>0</soundDelayTicks>
  <effectComps>
    <li Class="DMS_Legion.GroundSupport.SupportEffects.CompProperties_AerialSupportEffect_Bombing">
      <explosionRadius>10</explosionRadius>
      <damageAmount>50</damageAmount>
      <explosionCount>10</explosionCount>
      <targetAreaRadius>10</targetAreaRadius>
      <explosionsPerTick>1</explosionsPerTick>
      <explosionIntervalSeconds>0.2</explosionIntervalSeconds>
      <damageDef>Bomb</damageDef>
    </li>
  </effectComps>
</DMS_Legion.GroundSupport.AerialSupportTypeDef>
```

### 多目标支援示例
```xml
<DMS_Legion.GroundSupport.AerialSupportTypeDef>
  <defName>DMSL_AerialSupport_MultiTarget</defName>
  <label>多目标精确打击</label>
  <description>选择3个目标位置，飞机将依次对每个目标执行精确打击。</description>
  <aircraftTexturePath>GroundSupport/LASB-25</aircraftTexturePath>
  <flightSpeed>0.02</flightSpeed>
  <cooldownTicks>0</cooldownTicks>
  <drawSize>25.0</drawSize>
  <appearSoundDef>AerialSupport_Normal</appearSoundDef>
  <continueDrawingAfterArrival>true</continueDrawingAfterArrival>
  <flightPathType>MultiTarget</flightPathType>
  <selectionPointCount>3</selectionPointCount>
  <selectionIntervalFrames>90</selectionIntervalFrames>  <!-- 90 ticks间隔 -->
  <renderDelayTicks>0</renderDelayTicks>
  <soundDelayTicks>0</soundDelayTicks>
  <effectComps>
    <li Class="DMS_Legion.GroundSupport.SupportEffects.CompProperties_AerialSupportEffect_Bombing">
      <explosionRadius>8</explosionRadius>
      <damageAmount>200</damageAmount>
      <explosionCount>3</explosionCount>
      <targetAreaRadius>2</targetAreaRadius>
      <damageDef>Bomb</damageDef>
    </li>
  </effectComps>
</DMS_Legion.GroundSupport.AerialSupportTypeDef>
```

### 自定义直线支援示例
```xml
<DMS_Legion.GroundSupport.AerialSupportTypeDef>
  <defName>DMSL_AerialSupport_CustomLineTest</defName>
  <label>自定义直线支援</label>
  <description>玩家选择两个点，飞机沿自定义直线飞行并执行效果。</description>
  <aircraftTexturePath>GroundSupport/LASB-25</aircraftTexturePath>
  <flightSpeed>0.025</flightSpeed>
  <cooldownTicks>0</cooldownTicks>
  <drawSize>35.0</drawSize>
  <appearSoundDef>AerialSupport_Normal</appearSoundDef>
  <continueDrawingAfterArrival>true</continueDrawingAfterArrival>
  <flightPathType>CustomLine</flightPathType>
  <renderDelayTicks>0</renderDelayTicks>
  <soundDelayTicks>0</soundDelayTicks>
  <effectComps>
    <li Class="DMS_Legion.GroundSupport.SupportEffects.CompProperties_AerialSupportEffect_CustomLineBombing">
      <explosionCount>8</explosionCount>
      <damageAmount>50</damageAmount>
      <explosionRadius>15</explosionRadius>
      <damageDef>Bomb</damageDef>
    </li>
  </effectComps>
</DMS_Legion.GroundSupport.AerialSupportTypeDef>
```

## 配置建议

1. **飞行速度**：0.005-0.03之间，数值越大飞行越快
2. **绘制大小**：根据贴图大小调整，通常10-35之间
3. **爆炸数量**：根据效果强度调整，普通支援5-20，多目标支援2-5
4. **时间间隔**：多目标支援建议60-180 ticks间隔，便于观察效果
5. **冷却时间**：测试时设为0，正式使用时根据平衡性设置
6. **延迟配置**：通常使用0（立即执行），特殊效果可设置5-60 ticks的延迟

## 注意事项

- `defName` 必须唯一，且以 `DMSL_AerialSupport_` 开头
- 多目标支援的 `selectionPointCount` 至少为2
- 效果组件可以组合使用，执行顺序与XML中的顺序一致
- 所有坐标相关的数值都以游戏单元（tiles）为单位
- 延迟配置的基准点是飞行实例创建时间，不是绘制开始时间
- 如果不需要延迟，可以省略 `renderDelayTicks` 和 `soundDelayTicks` 字段（默认值为0）</content>
</xai:function_call">The file d:\steam\steamapps\common\RimWorld\Mods\The Dead Man's Switch - Legion\1.6\Defs\GroundSupportDefs\AerialSupport_XML_Usage.md has been created.