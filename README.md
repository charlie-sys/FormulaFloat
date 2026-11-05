# FormulaFloat · 轻量悬浮公式提取

面向 Windows 10 / 11 的原生 WPF 桌面原型。截图 → 视觉模型 → LaTeX → 自然语言调整，关闭面板后仍保留悬浮入口。没有浏览器运行时和本地模型依赖，无第三方 NuGet 包。

## 使用

1. 运行 `release/FormulaFloat/FormulaFloat.exe`。轻量版需要 **.NET 8 Desktop Runtime x64**；自包含构建可在未安装运行时的机器上使用。
2. 点击「设置」，填写视觉模型的接口地址、模型 ID 和 API Key。地址可填 `https://你的服务/v1` 或完整的 `/chat/completions` 路径。不会自动猜测 `/v1`。
3. 点击「截取公式」或悬浮球，拖拽框选，松开后开始识别。支持多屏矩形框选；Esc 或右键取消截图。
4. 核对 LaTeX 源码，可直接手动修改。复制纯 LaTeX，或复制带 `$$` 的公式。
5. 在底部输入调整要求并回车，例如「把变量 x 改成 α，保留其他内容」「整理为 aligned 环境」。每次调整都传入原图和当前源码，可连续修订并撤销模型修改。

快捷键 **Ctrl + Alt + M** 可全局截图。若被其他软件占用，会提示改用悬浮球。拖拽悬浮球移动，右键打开结果、设置或退出；系统托盘也提供同样入口。关闭结果窗口只是收起，完全退出请使用托盘或悬浮球右键菜单。

## 模型接口

当前支持 OpenAI 兼容 **Chat Completions**：POST JSON、`messages`、`image_url` 的 Base64 PNG 输入、`choices[0].message.content` 文本/文本块输出。模型必须支持视觉输入。可连接云端兼容服务，或本机带视觉模型的兼容服务（如 `http://localhost:11434/v1`）。原生 Anthropic/Gemini/Responses 协议尚未实现，可通过兼容网关接入。

不会预置或绑定模型，不自动提交测试请求或消耗用户额度。配置保存后首次截图即使用真实接口。请求可取消，超时可设为 10–300 秒；失败保留当前公式与调整要求，方便重试。错误提示不展示服务商原始响应或密钥。

## 数据

- 配置文件：`%LOCALAPPDATA%/FormulaFloat/settings.json`。
- API Key 使用 Windows DPAPI 按当前用户加密，不能直接复制到另一台电脑使用。
- 完整桌面在框选期间暂存在内存，只有选中的矩形 PNG 会传给模型；不写入磁盘、不自动保存历史。
- 每次模型调整会再次发送当前截图、当前公式及调整要求。数据去向由用户配置的接口地址决定。
- 云端要求 HTTPS，本机回环服务允许 HTTP。HTTP 重定向禁用，避免授权头和截图被意外转发。

## 开发与打包

使用 .NET 8 SDK，在 Windows 上执行：

```powershell
dotnet run --project FormulaFloat
dotnet run --project FormulaFloat.Tests -c Release
dotnet run --project FormulaFloat.Tests -c Release -- --render  # 离屏渲染布局示例
./build.ps1                    # 依赖桌面运行时的轻量单文件包
./build.ps1 -SelfContained     # 自包含包，体积更大
```

如存在项目内 `.tools/dotnet/dotnet.exe`，构建脚本优先使用它。测试通过本地模拟 HTTP handler 验证协议、上下文、错误和取消，不调用付费 API。

## 当前边界

此版本提供原截图对照和可编辑 LaTeX 源码，尚无数学排版预览、历史数据库、自启或安装器。识别准确率取决于模型与截图清晰度，应核对上下标、矩阵、括号等细节。受保护的视频/远程桌面内容可能无法截图。多屏不同缩放已按物理像素设计，但仍需在不同硬件上验证；当前交付不表示已实测全部 Win10/11 与 DPI 组合。

平台参考：[Windows 全局快捷键](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)、[视觉输入协议](https://developers.openai.com/api/docs/guides/images-vision)。

## 本次交付验证

已在当前 Windows 环境成功构建发布版，并通过 20 项离线逻辑检查；真实进程启动成功，能读取完整的原生控件树；主面板和设置页已用 WPF 离屏渲染检查布局。默认发布 EXE 约 208 KiB，依赖系统桌面运行时，文件体积不等同于内存占用：本机首次打开主面板时观察到工作集约 192 MiB、私有内存约 112 MiB，尚未进行长期常驻性能优化。

解锁桌面后已检查主窗口实际布局、源码编辑及复制按钮的启用状态；用户手动确认设置窗口可以正常打开，并已通过界面完成本地模拟服务配置的保存。实际鼠标拖拽截图、多屏混合 DPI、剪贴板操作及完整模型交互仍未完成端到端验证；未配置真实模型密钥，因此未验证服务商连通性或识别准确率。`artifacts/layout-*.png` 为代码渲染的布局示例，不是实际模型识别截图。
