# Run!ZombieFoods! 高刷新率移动与镜头修复（1.1.0）

当前游戏目录已经安装完成，直接启动 `Run!ZombieFoods!/Run!ZombieFoods!.exe` 即可。
不需要将显示器改成 60Hz，也没有通过锁定渲染帧率来修复。

## 1.1.0：镜头跟随卡顿修复

1.0.0 修复了人物速度，但遗漏了镜头的更新方式：`Camera2DFollow.Update`
每个渲染帧调用 `Rigidbody2D.MovePosition`，位置要等下一次物理更新才会生效。
高刷新率下，多次镜头计算会被覆盖，镜头仍然按物理频率分段移动。

1.1.0 包含以下修复：

- 在 `LateUpdate` 中运行原镜头逻辑，等 `CameraTarget.Update` 更新跟随目标后再计算镜头。
- 镜头位置在当前渲染帧立即生效，不再排队等待物理更新；保留相机 Z 深度及共享位置数据。
- 给使用 `PolyNavAgent` 的人物刚体开启 `Interpolate`，平滑物理帧之间的显示位置。
  导航计算读取真实的 `Rigidbody2D.position`，避免把插值后的显示位置用于物理计算。
- 镜头前视阈值、缩放步长按 `Time.deltaTime` 换算，以原版 60fps 参数作为参考。
- 沿用原有平滑跟随、室内外目标选择和震屏逻辑。目标为空、镜头组件停用时跳过更新。

显示插值会带来约一个物理步长的视觉延迟（本游戏约 20ms），用于换取平滑显示；
不改变物理步长、不锁帧、不提高全局物理频率。

相关 Unity 5.6 官方文档：
- [MovePosition 在下一次物理更新生效](https://docs.unity3d.com/560/Documentation/ScriptReference/Rigidbody2D.MovePosition.html)
- [Rigidbody2D 显示插值](https://docs.unity3d.com/560/Documentation/ScriptReference/Rigidbody2D-interpolation.html)
- [LateUpdate 与镜头跟随](https://docs.unity3d.com/560/Documentation/ScriptReference/MonoBehaviour.LateUpdate.html)

## 版本与安装

- 游戏：Unity 5.6.2f1，Windows x86，Mono / CLR 2.0。
- 加载器：BepInEx 5.4.23.2 **x86**。
- 插件：`Run!ZombieFoods!/BepInEx/plugins/HighRefreshFix.dll`。
- 原有 BepInEx 3.1 实测出现 `TypeLoadException: Could not load type 'NullCheck'`，不能正常加载。
- 已将旧加载器与旧测试插件移入 `backups/bepinex3`，并用原有 `UnityEngine.dll.bak` 恢复引擎 DLL，取消旧版硬注入。新版加载器在内存中注入，未修改 `Assembly-CSharp.dll`。

官方来源：
- https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.2
- https://docs.bepinex.dev/articles/user_guide/installation/index.html

下载包：`tools/BepInEx_win_x86_5.4.23.2.zip`
SHA256：`7DD51193587A86C11A58530AEEB4789B15763CC6CF2C55E71785CEF4EF214253`

## 原因与修复

`PolyNavAgent.LateUpdate` 每个渲染帧计算 `position + velocity * Time.deltaTime`，
但 `position` setter 只是覆盖 `tagPostion`。真正的 `Rigidbody2D.MovePosition`
在 `FixedUpdate` 执行。因此同一个物理帧之间的多次渲染计算不能累计位移。

补丁跳过渲染阶段的导航计算，在原来的 `FixedUpdate` 之前调用原导航逻辑。
这时 `Time.deltaTime` 由 Unity 提供物理步长（实测 0.02 秒），随后原代码提交移动。
保留原有寻路、日夜速度、避障和到达判定，不复制整套导航算法。
使用同一 `PolyNavAgent` 的其他单位也会应用该修复。

修复后的速度遵循游戏配置的每秒速度，而不是按 60Hz 时已经丢失的位移重新标定。
在物理频率 50Hz、渲染频率 60fps 的简化恒速场景中，原版只有约 5/6 的设计速度；
因此修复后可能比原版 60fps 略快。加速和转向现在也按固定物理频率更新。

## 验证

- 使用 .NET 3.5 编译器构建成功，匹配游戏旧运行时。
- 游戏后台启动实测：BepInEx 5.4.23.2 加载成功，插件输出 `[HighRefreshFix] READY`。
- 在游戏真实 Mono/Unity 运行时执行 `tests/SchedulingProbe.cs`：每次物理调用前插入
  0、1、2、3、4、8、16 次渲染回调，验证渲染阶段不执行原导航、物理阶段执行原导航、
  调用后开关正确复位，并检查人物插值设置。测试 DLL 在交付前移出插件目录。
- 镜头测试 `tests/CameraProbe.cs` 使用游戏真实运行时及独立测试相机，
  通过 `Time.captureFramerate` 模拟 30/60/144/180/240fps，检查镜头每帧立即移动、
  Z 深度不变、共享坐标同步、每秒缩放量一致，以及停用、目标缺失、室内目标切换。
  这是模拟帧时间测试，并非显示器刷新率或实际画面观感测量。
- 1.0.0 日志：`analysis/verified-runtime.log`；1.1.0 日志：`analysis/verified-camera-runtime.log`。
- 加载器会有一条找不到 `Application.isBatchMode` 的兼容性警告；本次没有阻止加载或测试。
- 尚未完成实际关卡中 60/144/180/240Hz 下的交互移动距离、碰撞和到达目标对比。
  自动测试验证调度机制，不等同于完整实机游玩验收。

建议在同一关卡、同一路线分别以 60Hz 和高刷新率移动，比较到达时间及镜头平滑度，
同时检查停止、拐弯、碰撞、进出室内、震屏及日夜切换。
查看游戏 `_Data/output_log.txt` 中的 `[HighRefreshFix] READY 1.1.0` 标记确认生效。

## 构建与卸载

### 一键编译发布

在 VS Code 的 PowerShell 终端中运行 `./release.cmd`，或运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1
```

脚本自动从 `src/HighRefreshFix.cs` 的 `BepInPlugin` 声明读取版本号（如 `1.1.0`），
编译并生成以下文件，不覆盖游戏里正在使用的插件：

```text
releases/
  README.md                       # 按版本号从新到旧排列的下载索引
  1.1.0/
    HighRefreshFix-1.1.0.zip       # 可分发插件包，不含游戏或加载器
    manifest.json                 # 版本、UTC 构建时间、源码及产物 SHA256
    package/
      README.md
      BepInEx/plugins/HighRefreshFix.dll
```

脚本支持反复构建同一版本。新包编译、打包成功后，旧目录会自动备份到
`releases/.history/<版本号>-<UTC时间>-<唯一标识>/`，再将新包放入当前版本目录。
编译或打包失败不会替换旧包；替换过程中出错会尝试恢复原目录。
失败的 `.staging-*` 临时目录会保留用于诊断。
发布新版本时修改插件声明中的版本号，并更新本说明。
`release.cmd` 默认不暂停，适合 VS Code 终端；需要完成后等待按键时运行 `./release.cmd --pause`。
`releases/README.md` 按数字版本排序，例如 `1.10.0` 排在 `1.9.0` 前面。

构建依赖本机 .NET 3.5 编译器，以及当前游戏目录中的 Unity、游戏和 BepInEx DLL。
脚本会检查所需文件，不自动下载依赖。从其他工作目录启动脚本也可以正常工作。

### 本地安装、测试与卸载

在项目根目录运行 `powershell -ExecutionPolicy Bypass -File .\build.ps1`，
会直接生成游戏插件目录中的 DLL。源码在 `src/HighRefreshFix.cs` 和 `src/CameraFix.cs`。

自动探针的构建命令为 `powershell -ExecutionPolicy Bypass -File .\tests\build-probes.ps1`。
该命令会把两个测试插件放入游戏插件目录；它们会临时调整模拟帧时间，仅供后台测试。
测试完成后必须移走 `SchedulingProbe.dll` 与 `CameraProbe.dll`，再正常游玩。

关闭游戏后，将 `BepInEx/plugins/HighRefreshFix.dll` 移到游戏目录之外，即可停用全部修复。
若只想回退本次镜头改动，用 `backups/1.0.0/HighRefreshFix.dll` 覆盖插件目录中的同名文件。
如需停用整个加载器，将游戏根目录的 `winhttp.dll` 移走；引擎 DLL 已恢复原版，
不需要再次运行旧 `BepInEx.Patcher.exe`。

今后的发布包位于 `releases/<版本号>/HighRefreshFix-<版本号>.zip`，
用于已经安装 BepInEx 5 的同版本游戏；`dist/` 中的包保留为早期手动构建产物。
先安装上述官方 x86 加载器，再将插件包中的 `BepInEx` 文件夹合并到游戏目录。
从旧 BepInEx 3 升级时应先恢复原引擎 DLL，并移走 Managed 中旧 BepInEx.dll / 0Harmony.dll，
避免两代加载器混用。本项目已完成这些步骤。

## Git 忽略规则

`.gitignore` 忽略游戏本体、第三方下载 `tools/`、分析日志 `analysis/`、备份 `backups/`、
旧包 `dist/`、新包 `releases/`、测试 DLL/PDB、`bin/obj` 和常见编辑器临时文件。
源码、README、编译/发布脚本和测试源码保持可提交，不使用全局 `*.dll` 或 `*.zip`
规则，以免未来有意加入的依赖或测试资料被意外隐藏。发布包可单独上传到 GitHub Releases。
