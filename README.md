# Run!ZombieFoods! 高刷新率适配

不需要将显示器改成 60Hz 来换取正常的移动速度，本mod修复了这个bug。

# 更新日志

## 1.0.0 
1. 修复了人物速度


## 1.1.0 
1. 修复了镜头跟随速度

## 1.1.1
1. 插件运行信息统一写入 BepInEx 日志系统。

## Steam 安装

关闭游戏。先将官方 `BepInEx_win_x86_5.4.23.2.zip` 的内容解压到游戏根目录，
再将 `HighRefreshFix-1.1.1.zip` 的内容解压到同一目录，合并文件夹。
不要多套一层压缩包名称文件夹。正确结构：

```text
Run!ZombieFoods!/
  Run!ZombieFoods!.exe
  winhttp.dll
  doorstop_config.ini
  BepInEx/
    core/BepInEx.dll
    core/BepInEx.Preloader.dll
    plugins/HighRefreshFix.dll
```

解压完成后从 Steam 启动游戏。