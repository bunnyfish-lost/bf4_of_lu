# 第三方材料核对清单

这个清单区分“已保留许可文本”和“已确认可以按某种方式重新发布”。本次整理只完成前者，不代替许可核对。

| 材料 | 当前保留的信息 | 公开前需要确认 |
| --- | --- | --- |
| Node.js Windows 运行时 | `engine-template/host/node/LICENSE`、`SHASUMS256.txt` 及引擎 `THIRD-PARTY.txt` | 按发行版的许可和第三方通知处理 |
| SharpDX | `engine-template/host/core/local-fun/SharpDX-LICENSE.txt`，敌人显示各版本也有许可文件 | 保留原版权及许可文字，并核对所分发版本 |
| BF4-MOD-Easy / BF4-Host-Panel 相关整理代码、协议与 SDK | 模组 `ORIGIN.md` 及现有来源说明 | 核实原项目许可；根据原二进制恢复的部分也需要核对，不因重写部分生命周期逻辑而自动变为全新授权 |
| BF1/BFV/CF 音效 | `mods/battle-audio/1.1.0/resources/sounds/SOURCES.json` 中的来源与哈希 | 核实素材的公开分发范围，来源记录本身不是素材许可 |
| 死亡音效片段 | 引擎 `THIRD-PARTY.txt` 及相关资源 | 核实原素材许可和摘录范围 |
| 界面背景图片 | `Assets/Bf4Backdrop.jpg` | 核实图片来源及使用范围 |
| 本项目新增代码 | 根目录程序、模组和相关重写部分 | 由有权授权者确定项目级许可证及适用范围 |

## 不包含的玩家材料

本目录不收集完整游戏、完整服务端、个人存档、实际实例的根配置、运行进程记录或个人音频。
模组内的 `config.json` 均按 manifest 默认值生成。

## 不要删掉现有来源文件

保留全部 `LICENSE`、`SharpDX-LICENSE.txt`、`ORIGIN.md`、`SOURCES.json` 和 `THIRD-PARTY.txt`。
如果决定暂时不公开某些材料，应先同步调整构建或资源准备步骤，而不是直接删文件后保留原打包说明。
