# 补给倍率 1.0.0 · 独立模组

## 文件都在哪里

```text
mods/supply-multiplier/1.0.0/
  mod.json                    模组说明与通用运行入口
  config.json                 本模组自己的配置
  supported-server.json       本模组支持的服务端版本
  bin/BF4SupplyMod.exe         实际执行补给功能的独立程序
  source/                     补给实现源码与项目文件
```

补给速度、缓释时间、弹药补给量的实现都在本目录的程序与源码中，不在启动器 EXE 中。
启动器通过外部模组接口启动 bin/BF4SupplyMod.exe，并传入上下文文件。拿走该程序后，本功能无法运行，没有启动器内置实现作为后备。

## 使用

启动器启动时扫描 mods，也可点击“刷新目录”。在列表中找到“补给倍率”，打开配置后启用。
默认医疗、弹药倍率均为 1，范围为 1～10，支持小数。初次安装默认关闭。

- 医疗：提高持续医疗包回血速度；小急救包将原始每秒回血速度乘以倍率，保持单包总量不变。
- 弹药：提高持续补给速度、单次补给量和对应供给容量。
- 小急救包的总回血量、容量、受伤中断和补给等待规则保持原样。
- 不改变补给半径、士兵血量上限或武器容量，也不打开无限弹药。

修改后重新投放补给包验证新值；测试弹药变化时使用普通弹药模式。
这是服务端模组，对本实例的相关补给资源生效，不向客户端安装文件。
停用时撤回资源参数修改，已收到的血量/弹药不会被扣回。

## 实现与数据

source/SupplyPolicy.cs：倍率计算与原始数据所有权。
source/SupplyMemory.cs：补给资源的类型验证与进程读写。
source/SupplySession.cs：原始记录、恢复与更新。
source/Program.cs：外部接口上下文与生命周期。

原始记录保存在宿主提供的 stateDirectory/baseline.json，日志和状态文件也由上下文提供。
程序自行核对本实例服务端路径、PID/启动时间、文件版本和类型名称/尺寸；不修改游戏磁盘文件。

## 2026-11-01 回血速度修正

修正小急救包 `DistributeHealingOverTime` 的字段含义：原生代码用“回血量 / 此字段”计算持续时间，因此此字段在该路径中表示回血速率。现在使用乘法提高速度，不再把它当持续时间做除法。
例如默认约 10 HP/s，10 倍对应约 100 HP/s；缺少 80 HP 时，小包效果自身的耗时约为 0.8 秒，不包括其他等待或中断。
公开模组版本仍为 1.0.0，程序文件版本为 1.0.0.1。更新时保留原有配置。

## 验证

已在真实隔离服务端验证独立程序路径、倍率写入回读、配置热更新、停用还原。
另验证移走本模组 EXE 后功能不能加载，以及不修改启动器即可放入并运行另一个新代码模组。
尚未进行客户端人物拾取后的回血时长计时。

## 构建

Windows + .NET 8 SDK：

```powershell
dotnet publish source/SupplyMod.csproj -c Release -o bin --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None -p:DebugSymbols=false
```
