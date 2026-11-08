# UnityMCP — MCP for Unity

> `com.coplaydev.unity-mcp` **10.3.0** · Unity **2021.3+** · 协议 **MCP** · 许可 **Apache-2.0**

把 AI 助手（Claude Code、Cursor、VS Code、Windsurf、ZCode、DeepSeek Harness 等）通过 **MCP（Model Context Protocol）** 接入 Unity 编辑器：让 AI 客户端直接创建对象、改组件、写脚本、跑测试、截屏、管理资源，而不只是"把代码贴给你"。

本仓库是 Unity 包 [`com.coplaydev.unity-mcp`](https://github.com/CoplayDev/unity-mcp) v10.3.0 的镜像/打包副本，方便以 **UPM Git 依赖** 的方式安装和自用定制；相对上游额外内置了 **ZCode** 与 **DeepSeek Harness** 两个客户端配置器。

| 项目 | 值 |
| --- | --- |
| 包名 | `com.coplaydev.unity-mcp` |
| 版本 | `10.3.0` |
| 最低 Unity | `2021.3` |
| 服务器 | Python 包 `mcpforunityserver`（由 `uvx` 拉起） |
| 默认端点 | `http://127.0.0.1:8080/mcp`（本机 HTTP） |
| 许可证 | Apache-2.0 |

编辑器入口：`Window > MCP for Unity`（Windows/Linux `Ctrl+Shift+M`，macOS `Cmd+Shift+M`）。

---

## 目录

- [工作原理](#工作原理)
- [特性](#特性)
- [本仓库相对上游的改动](#本仓库相对上游的改动)
- [环境要求](#环境要求)
- [安装](#安装)
- [快速开始](#快速开始)
- [支持的客户端](#支持的客户端)
- [ZCode 配置详解](#zcode-配置详解)
- [DeepSeek Harness 配置详解](#deepseek-harness-配置详解)
- [传输模式与安全](#传输模式与安全)
- [工具清单](#工具清单)
- [Skill 安装](#skill-安装)
- [目录结构](#目录结构)
- [常见问题](#常见问题)
- [升级与同步上游](#升级与同步上游)
- [版本与来源](#版本与来源)
- [English](#english)

---

## 工作原理

```
┌──────────────┐   MCP (JSON-RPC)   ┌──────────────┐   本地 TCP    ┌──────────────┐
│  AI 客户端    │ ─────────────────▶ │  MCP 服务器   │ ────────────▶ │ Unity Editor │
│ Claude/Cursor │  HTTP 127.0.0.1   │  uvx / stdio  │  桥接端口 6400  │  包内 C# 工具 │
│ ZCode / DSH … │ ◀───────────────── │  (Python)     │ ◀──────────── │  Editor 侧   │
└──────────────┘   工具结果/资源     └──────────────┘               └──────────────┘
```

- **服务器**由 `uvx` 从 PyPI 拉起（`mcpforunityserver==10.3.0`），无需在本仓库中携带 Python 源码。
- **桥接**运行在 Editor 进程内，默认从 `6400` 起探测可用端口，被占用时自动回退。
- 两种传输任选：**本机 HTTP**（默认，`http://127.0.0.1:8080/mcp`）或 **stdio**（由客户端拉起 `uvx`）。

---

## 特性

- **一键接入**：窗口内 Auto-Setup 自动探测 Python/uv 环境、写入客户端配置并拉起服务器。
- **28 个内置客户端配置器**：覆盖 Claude Code/Desktop、Cursor、VS Code（含 Insiders）、Windsurf、Codex、Gemini CLI、Qwen Code、Cline、Kilo Code、Kimi Code、Kiro、Trae/Trae CN、OpenCode、OpenClaw、Copilot CLI、CodeBuddy CLI、Cherry Studio、Pi、Rider、Antigravity（含 IDE）、MiniMax Code、WorkBuddy、ZCode、DeepSeek Harness 等。
- **双传输**：本机 HTTP 或 stdio，在窗口内切换后自动改写到已配置的客户端。
- **工具覆盖广**：36 个工具 + 19 个资源，见[工具清单](#工具清单)。
- **脚本编辑与校验**：Basic / Standard / Comprehensive / Strict 四级校验，Strict 走 Roslyn 语义分析。
- **自带安全开关**：本机/远程作用域隔离、远程模式 API Key（`X-API-Key`）、LAN 绑定开关、远程不安全 HTTP 拦截。
- **Skill 同步**：可把 `unity-mcp-skill` 安装到客户端 skills 目录，让 AI 掌握正确调用姿势。

---

## 本仓库相对上游的改动

在 v10.3.0 基础上新增了两个上游没有的客户端集成，其余客户端均来自上游：

| 客户端 | 配置落点 | 关键实现细节 |
| --- | --- | --- |
| **ZCode** | `~/.zcode/cli/config.json` → `mcp.servers` | 只写规范字段（`type`/`url`/`command`/`args`/`env`/`headers`/`enabled`/`timeoutMs`），因为 ZCode 遇到未知键会**整条丢弃**该服务器；HTTP 与 stdio 均支持；可安装 `unity-mcp-skill` 到 `~/.zcode/skills`；默认 `timeoutMs=60000`，避免冷启动 `uvx` 时工具枚举超时 |
| **DeepSeek Harness** | `~/.dsh/profiles/<profile>/cordis.patch.yml` | DSH 没有 MCP 设置页，服务器以插件行的形式存在于 profile 的 YAML 补丁层，必须用 `insert:` 条目引入（而非裸覆盖）；按文本行增删，保留注释与其他条目；写入后由 harness 热重载 |
| **MiniMax Code** | `~/.minimax/mcp.json` | 标准 `mcpServers` 布局，复用 `JsonFileMcpConfigurator`，无需专用 Helper；HTTP 条目写 `streamable-http` 类型（官方文档示例取值，插件分发的 `*.mcp.json` 不接受裸 `http`）；默认写 `enabled=true` 与 `timeout=60000`，避免冷启动 `uvx` 时工具枚举超时 |
| **WorkBuddy** | `~/.workbuddy/mcp.json` | CodeBuddy 家族的桌面版换皮，标准 `mcpServers` 布局（`WORKBUDDY_CONFIG_DIR` 可覆盖目录）；HTTP 条目写标准 `http` 类型（应用导入时会把 `streamable-http` 归一化为 `http`）；启用语义是 `disabled`（缺省即启用），故不写 `enabled`；默认写 `timeout=60000` 避免冷启动 `uvx` 超时 |

相应地扩展了 `ConfigJsonBuilder`（未知字段裁剪、客户端专属 `type` 取值）与 `McpClient` 模型，并新增 `DeepSeekHarnessConfigHelper`。新增配置器由 `McpClientRegistry` 通过 `TypeCache` 自动发现，无需改注册表。

---

## 环境要求

| 依赖 | 说明 |
| --- | --- |
| Unity | 2021.3 或更高（含 Editor 与构建目标模块） |
| Python | 3.10+（MCP 服务器运行环境） |
| uv / uvx | 推荐的服务器运行器；未安装时窗口会给出安装引导 |
| MCP 客户端 | 见[支持的客户端](#支持的客户端)，任选其一 |

---

## 安装

### 方式一：Package Manager 添加 Git URL（推荐）

`Window > Package Manager > + > Add package from git URL...`，填入（**GitCode 主源**）：

```
https://gitcode.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#main
```

GitHub 镜像：

```
https://github.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#master
```

### 方式二：直接写 `Packages/manifest.json`

```json
{
  "dependencies": {
    "com.coplaydev.unity-mcp": "https://gitcode.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#main"
  }
}
```

GitHub 镜像把上面的 URL 换成 `https://github.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#master` 即可（两者不要同时写进同一份 manifest）。

### 方式三：本地嵌入

把 `com.coplaydev.unity-mcp` 整个文件夹复制到项目的 `Packages/` 目录下即可（Unity 以 **Embedded** 方式加载，便于改源码调试）。改动源码后建议选择这种方式，否则 UPM 会按 Git 版本重新拉取。

---

## 快速开始

1. 打开 `Window > MCP for Unity`。
2. 在 **MCP Client Configuration** 里选择客户端，点击 **Auto Configure**：
   - 未检测到 Python / uv 时按提示安装；
   - uv/uvx 不在 PATH 时用 **Choose UV Install Location** 指定路径。
3. 确认 **Server Status** 为 `Installed`，需要时点 **Auto-Setup** 完成环境注册。
4. `Unity Bridge` 显示 `Stopped` 时点击 **Start Bridge**（Auto 模式下通常自动启动）。
5. 重启（或重连）MCP 客户端，确认服务器出现在客户端的 MCP 列表中。
6. 让 AI 调用工具，例如"列出场景里所有 Camera"、"给 Player 加一个 Rigidbody"。

客户端配置细节与手动 JSON 片段见包内指南：[com.coplaydev.unity-mcp/README.md](com.coplaydev.unity-mcp/README.md)。

---

## 支持的客户端

28 个配置器，均由 `McpClientRegistry` 自动发现。路径以 Windows 为例，macOS/Linux 落在各自用户主目录下。

| 客户端 | 配置文件 / 方式 |
| --- | --- |
| Claude Code | `claude mcp add` CLI（`~/.claude`） |
| Claude Desktop | `%APPDATA%\Claude\claude_desktop_config.json` |
| Cursor | `~/.cursor/mcp.json` |
| VS Code / Insiders | `%APPDATA%\Code\User\mcp.json` |
| Windsurf | `~/.codeium/windsurf/mcp_config.json` |
| Codex | `~/.codex/config.toml` |
| Gemini CLI | `~/.gemini/settings.json` |
| Antigravity / Antigravity IDE | `~/.gemini/config/mcp_config.json` / `~/.gemini/antigravity-ide/mcp_config.json` |
| Qwen Code | `~/.qwen/settings.json` |
| Cline | `%APPDATA%\Code\User\globalStorage\saoudrizwan.claude-dev\settings\cline_mcp_settings.json` |
| Kilo Code | `~/.config/kilo/kilo.jsonc` |
| Kimi Code | `~/.kimi/mcp.json` |
| Kiro | `~/.kiro/settings/mcp.json` |
| Trae / Trae CN | `%APPDATA%\Trae\User\mcp.json` / `%APPDATA%\Trae CN\User\mcp.json` |
| OpenCode | `~/.config/opencode/opencode.json` |
| OpenClaw | `~/.openclaw/openclaw.json`（经 `openclaw-mcp-bridge`，暴露 `unityMCP__call` 代理工具） |
| Copilot CLI | `~/.copilot/mcp-config.json` |
| CodeBuddy CLI | `~/.codebuddy.json` |
| Cherry Studio | `%APPDATA%\Cherry Studio\config` |
| Pi | `~/.config/mcp/mcp.json` |
| Rider | `%LOCALAPPDATA%\github-copilot\intellij\mcp.json` |
| **MiniMax Code** | `~/.minimax/mcp.json` |
| **WorkBuddy** | `~/.workbuddy/mcp.json` |
| **ZCode** | `~/.zcode/cli/config.json` → `mcp.servers` |
| **DeepSeek Harness** | `~/.dsh/profiles/desktop/cordis.patch.yml` |

---

## ZCode 配置详解

ZCode 的 MCP 配置写在 `~/.zcode/cli/config.json` 的 **`mcp.servers`** 容器内。该 schema 是严格模式：**出现未知键会导致整条服务器被丢弃**，因此本仓库只写规范字段 `type` / `url` / `command` / `args` / `env` / `headers` / `enabled` / `timeoutMs`。

### 本机 HTTP（默认，推荐）

Unity 打开并已启动 HTTP 服务器时使用：

```json
{
  "mcp": {
    "servers": {
      "UnityMCP": {
        "type": "http",
        "url": "http://127.0.0.1:8080/mcp",
        "enabled": true,
        "timeoutMs": 60000
      }
    }
  }
}
```

### stdio（无需保持编辑器内 HTTP 服务器）

客户端自己拉起 `uvx`：

```json
{
  "mcp": {
    "servers": {
      "UnityMCP": {
        "type": "stdio",
        "command": "uvx",
        "args": [
          "--from",
          "mcpforunityserver==10.3.0",
          "mcp-for-unity",
          "--transport",
          "stdio"
        ],
        "enabled": true,
        "timeoutMs": 60000
      }
    }
  }
}
```

说明：

- 两个键都可省略：`enabled` 默认 `true`，`timeoutMs` 由配置器写为 `60000`。冷启动 `uvx` 首次下载依赖可能较慢，30 秒默认超时容易在工具枚举阶段失败。
- `args` 里的版本号跟随包版本；也可把 `--from` 换成 git 源或本地路径（见窗口 **Advanced Settings** 的服务器源覆盖）。
- 写完后重启 ZCode，再在 `Settings > MCP` 中确认服务器已出现。

---

## DeepSeek Harness 配置详解

DSH 没有 MCP 设置页：服务器是 profile 插件树里的一行，来自补丁层 `~/.dsh/profiles/<profile>/cordis.patch.yml`（Electron 应用使用 `desktop` profile；配置器在 `desktop` 缺失时会回退到任意带补丁文件的 profile）。

`@deepseek-ai/dsh-mcp-client` 虽然随应用安装，但没有 bundle 挂载它，因此必须用 **`insert:`** 补丁条目引入，而不是裸覆盖。手工添加的最小片段如下。

### 本机 HTTP

```yaml
- insert:
    - id: mcp-unityMCP
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: unityMCP
        transport: streamable-http
        url: http://127.0.0.1:8080/mcp
```

### stdio

```yaml
- insert:
    - id: mcp-unityMCP
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: unityMCP
        transport: stdio
        command: 'C:\Users\<you>\.local\bin\uvx.exe'
        args:
          - '--from'
          - 'mcpforunityserver==10.3.0'
          - 'mcp-for-unity'
          - '--transport'
          - 'stdio'
        env:
          SystemRoot: 'C:\Windows'
```

说明：

- 配置器以**文本行**方式增删该条目，注释、`!!js` 表达式和其他条目都会被原样保留。
- Windows 上 DSH 会从子进程环境中清除形似凭据的变量，`SystemRoot` 不在清除之列，且服务器需要它，因此 stdio 模式会显式写入 `env.SystemRoot`。
- 写入后 harness 热重载，无需重启应用；工具以 `unityMCP__<tool>` 形式暴露。

---

## 传输模式与安全

| 模式 | 服务器由谁拉起 | 端点 | 适用场景 |
| --- | --- | --- | --- |
| **HTTP Local**（默认） | Unity Editor 内 | `http://127.0.0.1:8080/mcp` | 本机开发；编辑器关闭时工具不可用 |
| **HTTP Remote** | 独立主机 | `http://<host>:8080/mcp` | 局域网/远程，需 API Key |
| **stdio** | MCP 客户端 | 无网络监听 | 客户端自行拉起服务器；仍需 Unity 打开 |

安全相关：

- **作用域隔离**：本机模式强制 loopback（`localhost` / `127.0.0.1` / `::1`）；远程模式才写入凭据头。
- **API Key**：远程模式自动注入 `X-API-Key` 头（`AuthConstants.ApiKeyHeader`），值取自 Editor 设置。
- **远程不安全 HTTP 拦截**：非 loopback 且未启用安全传输时会被拒绝。
- **LAN 绑定开关**：需要让局域网其他机器连入时才打开，默认关闭。

---

## 工具清单

36 个工具（括号内为命令名），客户端中通常带服务器名前缀，如 `UnityMCP__manage_gameobject`：

| 分类 | 工具 |
| --- | --- |
| 场景 / 对象 | `manage_scene`、`manage_gameobject`、`find_gameobjects`、`manage_components`、`manage_prefabs`、`manage_scriptable_object` |
| 编辑器 / 执行 | `manage_editor`、`execute_menu_item`、`execute_code`、`batch_execute`、`refresh_unity`、`read_console`、`unity_reflect` |
| 资源 | `manage_asset`、`manage_material`、`manage_shader`、`manage_texture`、`generate_image`、`generate_audio`、`generate_model`、`import_model`、`import_model_file` |
| 脚本 / 构建 / 测试 | `manage_script`、`manage_build`、`manage_packages`、`run_tests`、`get_test_job` |
| 图形 / 渲染 | `manage_graphics`、`manage_camera`、`manage_animation`、`manage_vfx`、`manage_profiler`、`manage_ui`、`manage_physics`、`manage_probuilder`、`blender_bridge` |

19 个只读资源（`get_*`）：`get_editor_state`、`get_gameobject`、`get_gameobject_components`、`get_gameobject_component`、`get_selection`、`get_project_info`、`get_tests`、`get_tests_for_mode`、`get_active_tool`、`get_tool_states`、`get_cameras`、`get_layers`、`get_tags`、`get_menu_items`、`get_windows`、`get_prefab_stage`、`get_renderer_features`、`get_rendering_stats`、`get_volumes`。

> 工具集随服务器版本演进；以客户端实际枚举结果为准。

---

## Skill 安装

`unity-mcp-skill` 是一份驱动 AI 正确调用本插件的技能包。可通过 `Window > MCP for Unity` 中的 Skill 安装入口同步（`Unity MCP Skill Install(Sync)` 窗口），源默认取上游仓库 `.claude/skills/unity-mcp-skill`。

| 客户端 | 安装位置 |
| --- | --- |
| Claude Code | `~/.claude/skills/unity-mcp-skill` |
| ZCode | `~/.zcode/skills/unity-mcp-skill` |
| DeepSeek Harness | `~/.dsh/skills/unity-mcp-skill` |

同步服务会写入所有权标记（`.unity-mcp-skill-sync`），避免覆盖用户自己维护的技能目录。

---

## 目录结构

```
UnityMCP/
├── com.coplaydev.unity-mcp/          # UPM 包根目录
│   ├── package.json                  # 包清单（名称、版本、Unity 版本、依赖）
│   ├── README.md                     # Editor 窗口使用指南
│   ├── Editor/                       # 编辑器侧实现
│   │   ├── Tools/                    # 36 个工具 + 19 个资源（按域分目录）
│   │   ├── Clients/                  # 各 MCP 客户端配置器（28 个，TypeCache 自动发现）
│   │   ├── Services/                 # 服务器管理、桥接、传输、工具发现
│   │   ├── Setup/                    # 安装向导、Skill 同步
│   │   ├── Helpers/                  # 配置 JSON 构建、端点/路径工具
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
| 窗口提示 Python / uv 未找到 | 安装 Python 3.10+ 与 `uv`，或在窗口中选择 uv/uvx 可执行文件路径 |
| `Auto Configure` 报 "uvx not found" | 同上；也可在 Advanced Settings 中设置覆盖路径 |
| Claude CLI 未找到 | 安装 Claude Code CLI，或在窗口中选择安装位置 |
| 客户端显示已配置但连不上 | 确认 Unity Bridge 为 Running、`8080` 未被占用，然后在客户端重连会话 |
| 工具列表为空 / 超时 | 多为冷启动 `uvx` 首次解析依赖；调大客户端超时（ZCode 默认已给 60 s），或先手动跑一次 `uvx --from mcpforunityserver==10.3.0 mcp-for-unity --help` 预热缓存 |
| 客户端里服务器"消失" | ZCode 严格 schema：检查 `mcp.servers.unityMCP` 下是否混入了未知键 |
| DSH 里看不到插件 | 确认补丁条目用了 `insert:` 且 id 为 `mcp-unityMCP`；检查 profile 是否为 `desktop` |
| 改了包源码不生效 | 用"本地嵌入"方式安装（放入 `Packages/`），或使用窗口中的 Rebuild MCP Server |
| 换传输后客户端仍按旧方式连 | 切换传输后重新点一次 `Auto Configure`，让配置被改写 |

上游排错文档：
[Fix Unity MCP and Cursor/VSCode/Windsurf](https://github.com/CoplayDev/unity-mcp/wiki/1.-Fix-Unity-MCP-and-Cursor,-VSCode-&-Windsurf) ·
[Fix Unity MCP and Claude Code](https://github.com/CoplayDev/unity-mcp/wiki/2.-Fix-Unity-MCP-and-Claude-Code)

---

## 升级与同步上游

- **UPM 安装**：Package Manager 中选择本包 → `Update`，或改动 manifest 里的 `#main` 指向的版本/分支后刷新。
- **本地嵌入**：手动用新版 `com.coplaydev.unity-mcp` 覆盖 `Packages/` 下的目录，保留你改过的文件。
- **同步上游**：上游为 [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)。本仓库只在 `Clients/Configurators`、`ConfigJsonBuilder`、`McpClient` 模型与 `Helpers/DeepSeekHarnessConfigHelper.cs` 上做了增量改动，升级时把这些增量重放到新版本即可。
- **本地仓库推送到两个远端**：

```bash
git remote add gitcode git@gitcode.com:zzr767299/UnityMCP.git   # 已配置过则跳过
git push gitcode master:main    # GitCode 主源（main）
git push origin master          # GitHub 镜像（master）
```

---

## 版本与来源

- 上游项目：[CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
- 本仓库内容对应上游包 `com.coplaydev.unity-mcp` **v10.3.0**
- 服务器实现（Python）由 PyPI 包 `mcpforunityserver==10.3.0` 提供，可执行入口 `mcp-for-unity`

## 许可证

[Apache License 2.0](LICENSE)。原始版权归 Coplay 及上游贡献者所有，详见 `LICENSE` 文件。

---

## English

A packaged copy of [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (**MCP for Unity** v10.3.0) — a bridge that connects MCP-capable AI clients to the Unity Editor so they can create objects, edit components, write and validate scripts, run tests, take screenshots and manage assets.

The package exposes **36 tools** and **19 resources** over either local HTTP (`http://127.0.0.1:8080/mcp`) or stdio, and ships **28 client configurators** that are auto-discovered at runtime.

This copy adds four client integrations that upstream does not ship:

- **ZCode** — writes `~/.zcode/cli/config.json` → `mcp.servers` using only canonical keys (ZCode drops any server that carries an unknown key), supports HTTP and stdio, installs `unity-mcp-skill` into `~/.zcode/skills`, and defaults `timeoutMs` to 60 s so a cold `uvx` start does not trip tool listing.
- **DeepSeek Harness** — inserts a plugin row into the profile YAML patch layer `~/.dsh/profiles/desktop/cordis.patch.yml` using an `insert:` entry (required because no bundle mounts `@deepseek-ai/dsh-mcp-client`); the file is edited line-wise so comments and other entries survive.
- **MiniMax Code** — writes the standard `mcpServers` layout in `~/.minimax/mcp.json`; HTTP entries use the `streamable-http` type token from the official docs (plugin-distributed `*.mcp.json` files reject the generic `http` type), and default to `enabled=true` with `timeout=60000` so a cold `uvx` start does not trip tool listing.
- **WorkBuddy** — a CodeBuddy-family desktop rebrand reading the standard `mcpServers` layout from `~/.workbuddy/mcp.json` (`WORKBUDDY_CONFIG_DIR` override respected); HTTP entries use the standard `http` type (the app normalizes `streamable-http` to `http` on import), and since enablement is a `disabled` flag (absent = enabled) no `enabled` key is written; defaults to `timeout=60000` for cold `uvx` starts.

Every other client — Claude Code/Desktop, Cursor, VS Code, Windsurf, Codex, Gemini CLI, Qwen Code, Cline, Kilo Code, Kimi Code, Kiro, Trae, OpenCode, OpenClaw, Copilot CLI, CodeBuddy CLI, Cherry Studio, Pi, Rider, Antigravity and more — comes from upstream.

### Install

Unity 2021.3+ → Package Manager → Add package from git URL:

```
https://gitcode.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#main
```

GitHub mirror:

```
https://github.com/zzr767299/UnityMCP.git?path=/com.coplaydev.unity-mcp#master
```

Then open `Window > MCP for Unity`, pick your client, click **Auto Configure**, start the bridge, and reconnect your MCP client. Requires Python 3.10+ and uv/uvx.

### Manual config (HTTP)

ZCode (`~/.zcode/cli/config.json`):

```json
{ "mcp": { "servers": { "UnityMCP": { "type": "http", "url": "http://127.0.0.1:8080/mcp", "enabled": true, "timeoutMs": 60000 } } } }
```

DeepSeek Harness (`cordis.patch.yml`):

```yaml
- insert:
    - id: mcp-unityMCP
      name: '@deepseek-ai/dsh-mcp-client'
      config:
        serverName: unityMCP
        transport: streamable-http
        url: http://127.0.0.1:8080/mcp
```

Licensed under the Apache License 2.0.
