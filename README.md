# UnityMCP — MCP for Unity

把 AI 助手（Claude Code、Cursor、VS Code、Windsurf、OpenClaw 等）通过 **MCP（Model Context Protocol）** 接入 Unity 编辑器：让 AI 客户端直接创建对象、改组件、写脚本、跑测试、截屏、管理资源，而不只是"把代码贴给你"。

本仓库是 Unity 包 [`com.coplaydev.unity-mcp`](https://github.com/CoplayDev/unity-mcp) v10.3.0 的镜像/打包副本，方便以 **UPM Git 依赖** 的方式安装和自用定制。

- 包名：`com.coplaydev.unity-mcp`　版本：`10.3.0`　最低 Unity：`2021.3`
- 编辑器入口：`Window > MCP for Unity`（Windows/Linux `Ctrl+Shift+M`，macOS `Cmd+Shift+M`）

---

## 特性

- **一键接入**：窗口内 Auto-Setup 自动探测环境、写客户端配置并拉起 MCP 服务器
- **26 个内置客户端配置器**：Cursor、VS Code、Windsurf、Claude Code、Codex、Gemini CLI、Cline、Roo/Kilo、Trae、OpenCode、Antigravity、OpenClaw、Rider…… 以及本仓库新增的 **ZCode**、**DeepSeek Harness**
- **双传输**：本地 HTTP（默认 `http://127.0.0.1:8080`）或 stdio，可在窗口内切换
- **工具覆盖广**：GameObject / 组件 / 资源 / 材质 / 场景 / 脚本 / Prefab / UI Toolkit / 物理 / 相机 / VFX / 动画 / 构建 / 性能分析 / 测试等
- **脚本编辑与校验**：分级校验（Basic / Standard / Comprehensive / Strict），支持 Roslyn 语义检查
- **自带安全开关**：API Key 校验（`X-API-Key`）、LAN 绑定开关、远程不安全 HTTP 拦截

### 本仓库相对上游的改动

在 v10.3.0 基础上新增了两个上游没有的客户端集成：

| 客户端 | 配置落点 | 说明 |
| --- | --- | --- |
| **ZCode** | `~/.zcode/cli/config.json` → `mcp.servers` | 只写规范字段（`type`/`url`/`command`/`args`/`env`/`headers`/`enabled`/`timeoutMs`），因为 ZCode 遇到未知键会丢弃该服务器；HTTP 与 stdio 均支持；可把 `unity-mcp-skill` 安装到 `~/.zcode/skills`；默认 `timeoutMs=60000`，避免冷启动 uvx 时工具枚举超时 |
| **DeepSeek Harness** | `~/.dsh/profiles/<profile>/cordis.patch.yml` | DSH 没有 MCP 设置页，服务器以插件行的形式存在于 profile 的 YAML 补丁层，需用 `insert:` 条目引入（而非裸覆盖），写入后由 harness 热重载 |

相应地扩展了 `ConfigJsonBuilder` 与 `McpClient` 模型。新增配置器会被 `McpClientRegistry` 通过 `TypeCache` 自动发现，无需改注册表。

---

## 环境要求

| 依赖 | 说明 |
| --- | --- |
| Unity | 2021.3 或更高（含 Editor 与构建目标模块） |
| Python | 3.10+（MCP 服务器运行环境） |
| uv / uvx | 推荐的服务器运行器；未安装时窗口会给出安装引导 |
| MCP 客户端 | Claude Code / Cursor / VS Code / Windsurf / OpenClaw 任选 |

---

## 安装

### 方式一：Package Manager 添加 Git URL（推荐）

`Window > Package Manager > + > Add package from git URL...`，填入：

```
https://github.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#master
```

### 方式二：直接写 `Packages/manifest.json`

```json
{
  "dependencies": {
    "com.coplaydev.unity-mcp": "https://github.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#master"
  }
}
```

### 方式三：本地嵌入

把 `com.coplaydev.unity-mcp` 整个文件夹复制到项目的 `Packages/` 目录下即可（Unity 会以 Embedded 方式加载，便于改源码调试）。

---

## 快速开始

1. 打开 `Window > MCP for Unity`。
2. 点击 **Auto-Setup**：自动检测 Python / uv，并按当前选择的客户端写入配置。
   - 若提示选择服务器目录，指向自带的 `Server` 目录（使用内置实现时）。
   - 缺失 Python 或 uv 时，按窗口提示安装。
3. `Unity Bridge` 显示 `Stopped` 时点击 **Start Bridge**。
4. 在 MCP 客户端中连接，即可调用 `unityMCP__*` 系列工具。

客户端配置细节（Cursor / VS Code / Windsurf / Claude Code / OpenClaw 各自的配置文件与手动 JSON 片段）见包内指南：[com.coplaydev.unity-mcp/README.md](com.coplaydev.unity-mcp/README.md)。

---

## 目录结构

```
UnityMCP/
├── com.coplaydev.unity-mcp/          # UPM 包根目录
│   ├── package.json                  # 包清单（名称、版本、Unity 版本、依赖）
│   ├── README.md                     # Editor 窗口使用指南
│   ├── Editor/                       # 编辑器侧实现
│   │   ├── Tools/                    # 暴露给 MCP 的工具实现
│   │   ├── Resources/                # MCP 资源（场景/对象/组件读取等）
│   │   ├── Services/                 # 服务层（服务器管理、桥接、客户端配置）
│   │   ├── Setup/                    # 安装向导、Skill 同步
│   │   ├── Clients/                  # 各 MCP 客户端配置器
│   │   ├── Security/                 # API Key / 传输安全
│   │   ├── Migrations/               # 版本迁移
│   │   ├── Dependencies/             # Python / uv 探测与安装
│   │   └── Windows/                  # 编辑器窗口 UI
│   └── Runtime/                      # 运行时辅助与序列化
├── LICENSE                           # Apache-2.0
└── README.md
```

---

## 常见问题

| 现象 | 处理 |
| --- | --- |
| 窗口提示 Python / uv 未找到 | 安装 Python 3.10+ 与 `uv`，或手动指定 uvx 路径 |
| Claude CLI 未找到 | 安装 Claude Code CLI，或在窗口中选择安装位置 |
| 客户端显示已配置但连不上 | 确认 Unity Bridge 为 Running、端口未被占用，然后在客户端重连会话 |
| 改了包源码不生效 | 用"本地嵌入"方式安装（放入 `Packages/`），或用窗口中的 Rebuild MCP Server |

上游排错文档：
[Fix Unity MCP and Cursor/VSCode/Windsurf](https://github.com/CoplayDev/unity-mcp/wiki/1.-Fix-Unity-MCP-and-Cursor,-VSCode-&-Windsurf) ·
[Fix Unity MCP and Claude Code](https://github.com/CoplayDev/unity-mcp/wiki/2.-Fix-Unity-MCP-and-Claude-Code)

---

## 版本与来源

- 上游项目：[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
- 本仓库内容对应上游包 `com.coplaydev.unity-mcp` **v10.3.0**
- 服务器实现（Python）由 `uvx --from <git-url> mcp-for-unity` 提供

## 许可证

[Apache License 2.0](LICENSE)。原始版权归 Coplay 及上游贡献者所有，详见 `LICENSE` 文件。

---

## English

A packaged copy of [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (**MCP for Unity** v10.3.0) — a bridge that connects MCP-capable AI clients to the Unity Editor so they can create objects, edit components, write and validate scripts, run tests, take screenshots and manage assets.

This copy adds two client integrations that upstream does not ship: **ZCode** (writes `~/.zcode/cli/config.json` → `mcp.servers`, optional skill install, 60 s timeout for cold `uvx` starts) and **DeepSeek Harness** (inserts a plugin row into the profile YAML patch layer `~/.dsh/profiles/desktop/cordis.patch.yml`). All other supported clients — Cursor, VS Code, Windsurf, Claude Code, Codex, Gemini CLI, Cline, Trae, OpenCode, OpenClaw and more — come from upstream.

Install in Unity 2021.3+ via Package Manager → Add package from git URL:

```
https://github.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#master
```

Then open `Window > MCP for Unity`, click **Auto-Setup**, start the bridge, and connect your MCP client. Requires Python 3.10+ and uv/uvx.

Licensed under the Apache License 2.0.
