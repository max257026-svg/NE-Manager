# Changelog

## v2.1.1 · 2026-08-31

**类型**：Hotfix

### 严重修复

- [InjectorPage] 移除 Process.GetProcesses() 直接绑定 DataGrid 的做法，改用 ProcessManager.Enumerate() 获取预填充的 ProcessItem。原实现每次 DataGrid 绑定 MainWindowTitle 都会从 UI 线程跨 RPC 访问每个目标进程的主窗口句柄，进程数量大时直接导致界面卡死；同时 Process 对象没有 Dispose，每次刷新都会累积 GDI 句柄和内核对象泄漏
- [InjectorPage] 进程列表改为异步加载并接入 StatusBar 状态提示，进入页面不再阻塞主线程；列绑定从 MainWindowTitle 切换为 ProcessItem 提供的 Name / ParentId / UserName / Path 四个预计算字段
- [DllInjector] 修复早期失败路径资源泄漏：当 VirtualAllocEx 成功但 WriteProcessMemory / CreateRemoteThread / WaitForSingleObject 后续步骤中任意一步失败时，远程内存不再泄漏。整个流程统一放入 try-finally，保证 VirtualFreeEx(remoteMem) 和 CloseHandle(hProc) 在所有分支上都会被调用

### 中等修复

- [MemoryEditorPage] 切换到 ProcessManager 后遗漏的字段名修正：.ProcessName → .Name，消除 CS1061 编译错误
- [ProcessManager.ProcessItem] 清理意外残留的 public string Name 重复声明
- [ProcessManager.ModuleItem] 在修复重复声明过程中被误删的 Name 属性恢复，EnumerateModules 逻辑重新编译通过

### Build

- Version: 2.1.1.0
- 0 警告 / 0 错误
- Self-contained single-file EXE · win-x64 · 64.44 MB

### 下载

https://github.com/max257026-svg/NE-Manager/releases/tag/v2.1.1

---

## v2.1 · 2026-08-31

**类型**：功能发布

### 新增功能

- 实时仪表盘（Dashboard）—— CPU 总使用率 + 物理内存使用率 实时折线图，PerformanceCounter 采样，环形缓冲 60 帧回看，自绘 Polyline + 半透明填充 + 网格线，零第三方依赖
- DLL 注入器（Injector）—— 对接 NE.Core.Injection.DllInjector（CreateRemoteThread + LoadLibraryW 原生封装），进程列表一键选中 + 手动 PID 输入，注入前确认弹窗 + 异步执行结果回显，需 SeDebug 特权或管理员权限
- UI 升级：标题栏版本号升级为 v2.1（Accent 蓝加粗），侧边栏新增 "v2.1 新功能" 分组顶置

### Bug Fixes（顺带修了 3 个）

- [SystemMonitorService] 物理内存指标改用真实 PerformanceCounter（Committed Bytes + Available MBytes），不再用 GC.GetTotalMemory 滥竽充数
- [DashboardPage] 控件生命周期从 OnVisualChildrenChanged 改为 Loaded/Unloaded，修复页面切走时 Timer 无法停止导致的多开问题
- [InjectorPage] 注入按钮改为 async/await + try/catch，消除 .ContinueWith + .Result 死锁风险

### Build

- Version: 2.1.0.0
- Self-contained single-file EXE · win-x64 · 64.3 MB

### 下载

https://github.com/max257026-svg/NE-Manager/releases/tag/v2.1

---

## v2.0 · 2026-08-31

**类型**：功能发布

### 全量功能（继承自 v1.0）

- 文件与系统：文件管理 / 权限与提权 / 注册表编辑器 / 启动项管理 / 一键清理
- 系统工具：进程管理器 / 服务管理器 / 磁盘与卷 / WMI 控制台
- 逆向与安全：PE 文件分析 / HEX 编辑器 / 文本编辑器 / 内存修改 / APK 解析 / 安全审计 / Diff 对比
- 开发工具：归档浏览 / 网络文件 / 脚本引擎 / 批量重命名 / 数据格式化 / Linux FS / macOS FS / 日志与回滚

### Build

- Version: 2.0.0.0
- Self-contained single-file EXE · win-x64 · 69.65 MB

### 下载

https://github.com/max257026-svg/NE-Manager/releases/tag/v2.0
