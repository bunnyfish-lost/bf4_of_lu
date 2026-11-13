# 当前构建依赖

## 为什么源码目录仍有 EXE / DLL

现有构建脚本将 `engine-template/` 制作为基础引擎资源，打包脚本直接使用各模组版本内的程序和依赖。
所以此上传候选目录保留了这些必需的二进制文件，而非只保留文本。

- `engine-template/host/node/node.exe`：当前附带 Node.js v24.19.0，约 88.5 MiB。
- `engine-template/host/core/`：公共辅助程序、图形库及可用的构建源码。
- `mods/*/*/bin/`、`payload/`、`resources/`：相应版本的程序、依赖、原生组件和素材。
- 补给倍率辅助程序包含自己的运行支持，文件体积比部分其他模组更大。

保留这些文件是为了不改变当前构建、测试和打包方式。若以后希望源码仓库不带二进制，需先增加独立依赖准备及辅助程序重建流程。
必要运行程序的许可核对见 [第三方材料核对清单](UPLOAD_REVIEW.md)。

## 依赖完整性

- 模组有 `checksums.json` 时，打包脚本会核对列出的文件。
- `.gitattributes` 关闭自动换行转换，避免 Git 改写脚本和 manifest 字节后导致校验失败。
- 不应通过忽略全部 EXE、DLL 或所有名为 bin 的目录来“精简”当前仓库。
- Node 与 SharpDX 的许可文件应随依赖保留。

## 不要提交的运行目录

根目录的 `host/`、`engine/`、`data/`、`logs/` 由启动器运行时产生，不是 `engine-template/` 的替代物。
实际实例配置、私钥、凭据和个人音频不属于源码。
