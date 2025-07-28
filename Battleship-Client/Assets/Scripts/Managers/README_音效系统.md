# 音效系统使用说明

## 概述
为了解决battle界面音效播放问题，我们创建了一个完整的音效管理系统，支持多种音效类型和场景切换时的音效保持。

## 系统组件

### 1. SoundEffectManager (音效管理器)
- **位置**: `Assets/Scripts/Managers/SoundEffectManager.cs`
- **功能**: 管理所有游戏音效的播放
- **特性**: 
  - 支持同时播放多个音效
  - 自动管理AudioSource池
  - 场景切换时保持活跃
  - 支持音量和音效类型控制

### 2. 音效类型
```csharp
public enum SoundEffectType
{
    Fire = 0,           // 射击音效
    Hit = 1,            // 击中音效
    Miss = 2,           // 未击中音效
    ShipSunk = 3,       // 船只沉没音效
    Skill = 4,          // 技能音效（通用）
    ButtonClick = 5,    // 按钮点击音效
    TurnSwitch = 6,     // 回合切换音效
    Victory = 7,        // 胜利音效
    Defeat = 8          // 失败音效
}

public enum HeroSkillType
{
    GuoJia = 0,         // 郭嘉技能音效
    Chengyu = 1,        // 程昱技能音效
    Zhugeliang = 2,     // 诸葛亮技能音效
    Zhouyu = 3,         // 周瑜技能音效
    // 可以继续添加更多武将
}
```

## 使用方法

### 1. 基本播放
```csharp
// 播放射击音效
SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.Fire);

// 播放击中音效
SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.Hit);

// 播放武将技能音效
SoundEffectManager.Instance.PlayHeroSkillSound(SoundEffectManager.HeroSkillType.GuoJia);

// 根据武将ID播放技能音效
SoundEffectManager.Instance.PlayHeroSkillSoundById(1); // 1=郭嘉, 2=程昱, 3=诸葛亮, 4=周瑜
```

### 2. 异步播放（等待完成）
```csharp
// 播放音效并等待完成
StartCoroutine(SoundEffectManager.Instance.PlaySoundEffectAsync(SoundEffectManager.SoundEffectType.Skill));
```

### 3. 音量控制
```csharp
// 设置主音量
SoundEffectManager.Instance.SetMasterVolume(0.8f);

// 设置音效音量
SoundEffectManager.Instance.SetSFXVolume(0.6f);
```

## 在Battle界面中的集成

### 已集成的音效
1. **射击音效**: 在`PlayShotEffectOnOpponentMap`方法中
2. **击中/未击中音效**: 在`OnPlayerShotsChanged`和`SetMarker`方法中
3. **武将技能音效**: 在`OnUseSkillButtonClicked`方法中（根据武将ID播放对应音效）
4. **回合切换音效**: 在`SwitchTurns`方法中
5. **胜利/失败音效**: 在`ShowResult`方法中（游戏结束时播放）
6. **按钮点击音效**: 自动为所有按钮添加（通过ButtonController和ButtonSoundEffect）

### 音效触发时机
- **射击**: 玩家点击敌方地图时
- **击中**: 射击命中敌方船只时
- **未击中**: 射击未命中时
- **武将技能**: 使用技能时（根据武将ID播放对应音效）
  - 武将ID 1 (郭嘉): 眩晕技能
  - 武将ID 2 (程昱): 照明技能
  - 武将ID 3 (诸葛亮): 揭示技能
  - 武将ID 4 (周瑜): 多方向开火技能
- **回合切换**: 回合切换时
- **胜利音效**: 游戏胜利时播放
- **失败音效**: 游戏失败时播放
- **按钮点击音效**: 点击任何按钮时播放

## 设置步骤

### 1. 创建音效管理器
1. 在场景中创建一个空的GameObject
2. 添加`SoundEffectManager`组件
3. 在Inspector中设置音效文件数组

### 2. 使用设置脚本（推荐）
1. 在场景中创建一个空的GameObject
2. 添加`SoundEffectManagerSetup`组件
3. 在Inspector中拖入音效文件：
   - **基础音效**: Fire, Hit, Miss, ShipSunk, Skill, ButtonClick, TurnSwitch, Victory, Defeat
   - **武将技能音效**: 郭嘉, 程昱, 诸葛亮, 周瑜
4. 右键点击组件，选择"设置音效管理器"

### 3. 音效文件要求
- 格式: WAV, MP3, OGG等Unity支持的音频格式
- 建议长度: 0.1-3秒（音效）
- 建议采样率: 44.1kHz
- 建议位深度: 16位

### 4. 按钮音效设置（推荐）
1. **分析按钮**：在场景中创建空GameObject，添加`ButtonAnalyzer`组件
   - 右键点击组件，选择"分析场景中的按钮"
   - 查看控制台输出，确认按钮识别情况
2. **批量添加音效**：创建空GameObject，添加`ButtonSoundEffectSetup`组件
   - 调整过滤选项（如需要）
   - 右键点击组件，选择"为所有按钮添加音效"
3. **手动添加**：为特定按钮添加`ButtonSoundEffect`组件

## 解决的核心问题

### 1. 场景切换太快的问题
- 使用`DontDestroyOnLoad`确保音效管理器在场景切换时不被销毁
- 在`GameSceneManager`中添加了音效管理器的检查

### 2. 音效播放时机问题
- 在正确的游戏事件中触发音效
- 使用多个AudioSource支持同时播放多个音效

### 3. 音效管理问题
- 统一的音效管理器
- 支持音量控制
- 支持音效类型管理

## 扩展功能

### 添加新音效类型
1. 在`SoundEffectType`枚举中添加新类型
2. 在`SoundEffectManager`的`soundEffectClips`数组中添加对应音效
3. 在需要的地方调用`PlaySoundEffect`

### 自定义音效播放
```csharp
// 播放指定索引的音效
SoundEffectManager.Instance.PlaySoundEffect(0);

// 播放自定义音效（需要先设置到数组中）
SoundEffectManager.Instance.PlaySoundEffect(SoundEffectManager.SoundEffectType.Custom);
```

## 注意事项

1. **音效文件**: 确保所有音效文件都已正确导入到Unity项目中
2. **性能**: 同时播放的音效数量有限制（默认8个），可根据需要调整
3. **内存**: 音效文件会占用内存，建议压缩音频文件
4. **平台**: 不同平台的音频支持可能不同，建议测试

## 故障排除

### 音效不播放
1. 检查`SoundEffectManager`是否存在
2. 检查音效文件是否正确设置
3. 检查音量设置
4. 检查AudioSource池是否已满

### 音效播放不完整
1. 检查音效文件是否损坏
2. 检查AudioSource池大小是否足够
3. 检查是否有其他音效干扰

### 场景切换后音效消失
1. 确保`SoundEffectManager`设置了`DontDestroyOnLoad`
2. 检查`GameSceneManager`中的音效管理器检查逻辑

### 按钮音效问题
1. **按钮识别错误**：使用`ButtonAnalyzer`分析场景中的按钮
2. **音效不播放**：检查按钮是否有`ButtonController`或`ButtonSoundEffect`组件
3. **误添加音效**：调整`ButtonSoundEffectSetup`的过滤选项
4. **排除特定按钮**：在`excludeKeywords`数组中添加关键词 