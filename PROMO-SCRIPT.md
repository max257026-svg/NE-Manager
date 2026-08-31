# NE Manager v2.1.1 宣传片文案

**时长**：~45 秒
**BGM**：Tech Launch Cinematic（搜网易云/QQ 音乐）
**节奏**：1.2 秒一帧

---

## 第一幕 · 开篇（0-7s）

`
[0.0]  黑场，心跳一样的低频嗡鸣
[0.8]  霓虹蓝斜切穿过屏幕，Zzzzt 电子音
[1.2]  图标从中心弹出，弹性动画：NE Manager
[2.2]  大标题淡入：终极 Windows 系统工具
[3.6]  闪白，切下一幕
[3.8]  副标题：v2.1.1 Hotfix · 4 Bugs · 2 Critical · 1 Fix
[6.0]  镜头微推：这是我们两周内的第三个补丁
       每一个数字背后，都是真实用户反馈回来的崩溃
`

---

## 第二幕 · InjectorPage 卡死修复（7-17s）

`
[7.0]  分屏：左 = v2.0 画面，右 = v2.1.1 画面
[7.4]  左边：注入器进程列表滚动 3 fps，每一行卡 300ms，鼠标指针转圈
       右边：丝滑 60fps 流畅滚动，选中后瞬间高亮
[8.8]  旁白：我们把 Process.GetProcesses() 直接绑进了 DataGrid
       然后天真地让 XAML 去访问 MainWindowTitle
[10.2] 画面切：WPF PropertyChangedCallback 跨 RPC 箭头动画
       每个进程的主窗口句柄，都要跨进程去取一次
       200 个进程 = 200 次 RPC = UI 线程直接卡死
[11.8] 修复动画：ProcessManager.Enumerate() 预打包 ProcessItem
       每列数据提前算好，进页面直接绑 — 0 次跨进程调用
[13.4] 第二个画面：Process Explorer 句柄数柱状图
       修复前：1243 个 GDI 句柄（红色，持续上涨）
       修复后：124 个（绿色，稳定不动）
[15.0] 旁白：我们还漏 Dispose 了 Process 对象
       每次刷新，句柄泄漏，一天能攒一千多个
[16.6] 绿色对勾弹层 · InjectorPage
`

---

## 第三幕 · DllInjector 内存泄漏修复（17-26s）

`
[17.0] 画面：虚拟地址条，Remote Memory 段高亮红框
[17.4] 旁白：VirtualAllocEx 成功了
       但 WriteProcessMemory 失败了
       然后 CreateRemoteThread 也失败了
[18.6] 箭头动画：三个 return 路径，每条都绕开了 VirtualFreeEx
       目标进程地址空间里永远挂着那几 KB 脏数据
[20.0] 修复动画：整个 Inject 函数包进 try-finally
       不管哪一步失败，finally 都兜底：
       VirtualFreeEx(remoteMem)
       CloseHandle(hProc)
       CloseHandle(hThread)
[22.0] 画面：地址条红框变绿框 · 实时内存监控折线图
       修复前：每次失败，远程内存向上跳一个台阶（阶梯状）
       修复后：注入失败后地址条自动清零（平稳）
[24.5] 旁白：现在失败也不再是无声的资源泄漏
       所有句柄、所有远程内存、所有内核对象
       全路径清理，一个不留
[25.8] 绿色对勾弹层 · DllInjector
`

---

## 第四幕 · MemoryEditor + ProcessManager（26-32s）

`
[26.2] VS Code 报错画面：error CS1061
       "ProcessItem" 未包含 ProcessName
[27.4] 旁白：换了数据源忘了改字段名
       一个 .ProcessName，编译直接挂掉
[28.2] 画面切：把 .ProcessName 替换成 .Name
       编译从 1 error → 0 error · 0 warning
[29.6] 第二个画面：ProcessManager.cs 对比
       左边：ProcessItem 里两份 public string Name（重复）
       右边：净化后只剩一份
[31.0] 绿色对勾弹层 × 2
`

---

## 第五幕 · 收尾（32-45s）

`
[32.0] 所有对勾弹层同时聚拢到屏幕中央 · 4 个对勾排成一行
[33.0] 旁白：4 个 bug，2 个高危，3 个 Build 之间
       我们把这些坑一个个填上
[34.4] 大镜头：NE Manager 启动动画 + 侧边栏 26 个页面快闪
       文件 · 进程 · 服务 · 磁盘 · WMI · PE · HEX · 内存 · APK · 注册表 ...
[36.2] 旁白：v2.1.1 不是一个"修点小问题"的版本
       它让进程列表不再卡死
       让 DllInjector 不再偷偷泄漏
       让内存编辑器 0 error 编译通过
[38.0] 画面：Build 输出 → 0 警告 · 0 错误 · 64.44 MB
[39.4] 旁白：每一个补丁，都是对承诺的兑现
       我们写的每一行代码
       都要对启动它的用户负责
[41.0] 画面：GitHub Release 页 · v2.1.1 已发布
       3 个下载链接 · GitHub · 123 · 夸克
[42.6] 旁白：下载试试，看看是不是更稳了
       有任何问题，我们会继续打补丁
[44.0] 画面：NE Manager · v2.1.1 · NewEra Studio
       蓝下划线 + 渐暗
[45.0] 结束
`

---

## 镜头清单（剪的时候照着来）

| 画面 | 怎么来 |
|------|--------|
| 开篇霓虹斜切 | AE / Premiere 里画蓝色 neon line + 斜向位移 |
| 左 v2.0 / 右 v2.1.1 分屏 | 录屏：启动注入器 → 滚进程列表，录两次（一次卡死，一次修完） |
| RPC 箭头动画 | PPT 画 3 行进程框，加 200 个箭头从 UI 线程穿到内核 |
| 句柄柱状图 | Process Explorer 抓图（View → Show Process Tree，列 Handler Count） |
| DllInjector 地址条 | 自己画一排小格子（0x00400000 ~ 0x7FFE0000），红块变绿块 |
| VS Code 报错 → 修复 | 真实录屏：打开 MemoryEditorPage.xaml.cs → 改字段名 → 看 Output 窗口 error count 变 0 |
| 4 个对勾聚拢 | Figma 画 4 个圆角白底 + 绿勾，AE 做 spring 弹性动画 |
| GitHub Release 页 | 浏览器录屏，打开 release 页，鼠标悬停 ZIP 大小 |
