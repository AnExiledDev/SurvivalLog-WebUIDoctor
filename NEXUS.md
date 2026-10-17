Name: WebUI Doctor
Summary: Gets the UI back when Survival Log shows its "interface display error". Restarts the game's browser until the UI returns, then closes the error dialog, gives clicks back and releases a stuck pause. 当 Survival Log 显示「界面显示异常」时把界面找回来。不断重启游戏的浏览器直到界面恢复，然后关闭错误对话框、恢复点击，并解除卡住的暂停。

Description (BBCode):
Survival Log draws its menus and HUD with a built-in browser. When that browser crashes and the game's own recovery gives up, you are left with an error dialog and a dead UI. WebUI Doctor restarts the browser, keeps trying until the UI is back, and cleans up after the crash.

[size=5]Main features[/size]
[list]
[*][b]Hard restart[/b]: when the game gives up, the mod restarts its browser and skips the "automatic recovery has failed" dialog.[/*]
[*][b]Never gives up[/b]: retries after 3 seconds, then 10 seconds, 30 seconds, 1 minute, then every 2 minutes until the UI is back.[/*]
[*][b]UI repair[/b]: after every recovery it closes the error dialog and gives mouse clicks back to the UI.[/*]
[*][b]Stuck pause release[/b]: a pause left over from the crash is resumed. A pause you set with the speed control is never touched.[/*]
[*][b]Recovery while paused[/b]: the game's recovery waits on game time, which barely moves while paused. The mod runs time at normal speed until the UI is back, then pauses the game again.[/*]
[*][b]Clicks on the world come back[/b]: if clicking an object while the UI was gone left every object unclickable (no menu to explore or sleep), the repair gives those clicks back.[/*]
[*][b]Other UI mods keep working[/b]: the recovered browser gets back the settings those mods need, and if they are still missing, the browser is restarted once more.[/*]
[*][b]UI clock check[/b]: once a minute it makes sure the clock on screen matches the game's time.[/*]
[*][b]Log file[/b]: everything it does goes to BepInEx\webui-doctor.log.[/*]
[/list]

[size=5]Hotkeys[/size]
[list]
[*][b]Ctrl+F12[/b]: repair the UI by hand (gives clicks back on the UI and the world, closes the error dialog, releases a stuck pause).[/*]
[*][b]Ctrl+F11[/b]: checks whether other UI mods can reach the game's page and writes the result to the log. Changes nothing.[/*]
[/list]
These keys are fixed and cannot be changed.

The mod writes nothing into your save. It only restarts the game's own browser and resumes a pause it judges stuck. You can remove it at any time; the game then shows its error dialog again when its recovery gives up.

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*]It cannot stop the browser from crashing. The UI is gone for a few seconds (longer after repeated crashes) while it comes back.[/*]
[*]Ctrl+F12 releases a pause even if the game thinks a window is open. Only a pause you set with the speed control is left alone.[/*]
[*]The leftover error panel is found by its English or Chinese text. In another language you may have to close it yourself.[/*]
[*]The log file is never trimmed. Delete it while the game is closed if it gets large.[/*]
[/list]
If you run into anything strange, please tell me (see Feedback below).

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.18902. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*]Windows.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Put [font=Courier New]WebUIDoctor.dll[/font] in the BepInEx\plugins folder inside the game folder. Full path example:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\WebUIDoctor.dll[/code][/*]
[/list]

[size=5]Uninstall[/size]
Delete [font=Courier New]WebUIDoctor.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder.

[size=5]Configuration[/size]
To turn the hard restart or the browser log off, edit [font=Courier New]BepInEx\config\com.dev.survivallog.webuidoctor.cfg[/font] while the game is closed (see [url=https://github.com/AnExiledDev/SurvivalLog-WebUIDoctor/blob/main/CONFIG.md]CONFIG.md[/url]; the cfg file explains each setting in English and Chinese). Changes apply at the next game start.

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or leave a comment here. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\webui-doctor.log[/font].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-WebUIDoctor]github.com/AnExiledDev/SurvivalLog-WebUIDoctor[/url]

[line]
[size=5]简体中文[/size]

Survival Log 用内置浏览器绘制菜单和 HUD。当这个浏览器崩溃、而游戏自己的恢复也放弃时，你只会看到一个错误对话框和一个无法操作的界面。WebUI Doctor 会重启浏览器，一直重试直到界面恢复，并在崩溃后清理残留问题。

[size=5]主要功能[/size]
[list]
[*][b]强制重启[/b]：游戏放弃时，模组重启它的浏览器，并跳过「自动恢复失败」的对话框。[/*]
[*][b]永不放弃[/b]：3 秒后重试，然后是 10 秒、30 秒、1 分钟，之后每 2 分钟一次，直到界面恢复。[/*]
[*][b]界面修复[/b]：每次恢复后关闭错误对话框，并把鼠标点击还给界面。[/*]
[*][b]解除卡住的暂停[/b]：崩溃遗留的暂停会被恢复。你用速度控制设置的暂停不会被改动。[/*]
[*][b]暂停时也能恢复[/b]：游戏的恢复要等游戏时间，而暂停时游戏时间几乎不动。模组会在界面恢复前让时间以正常速度运行，之后再次暂停游戏。[/*]
[*][b]恢复对场景物体的点击[/b]：如果界面消失时点击了物体，导致所有物体都无法点击（没有探索或睡觉菜单），修复会恢复这些点击。[/*]
[*][b]其他界面模组继续工作[/b]：恢复后的浏览器会重新获得这些模组需要的设置；如果仍然缺失，会再重启一次浏览器。[/*]
[*][b]界面时钟检查[/b]：每分钟确认一次屏幕上的时钟与游戏时间一致。[/*]
[*][b]日志文件[/b]：它做的每件事都会写入 BepInEx\webui-doctor.log。[/*]
[/list]

[size=5]快捷键[/size]
[list]
[*][b]Ctrl+F12[/b]：手动修复界面（恢复界面和场景物体的点击，关闭错误对话框，解除卡住的暂停）。[/*]
[*][b]Ctrl+F11[/b]：检查其他界面模组能否访问游戏页面，并把结果写入日志。不做任何更改。[/*]
[/list]
这些按键是固定的，不能修改。

模组不会往存档里写入任何内容。它只会重启游戏自己的浏览器，并恢复它判断为卡住的暂停。你可以随时移除它；移除后，游戏在恢复放弃时会再次显示错误对话框。

支持英文和中文。

[size=5]已知问题[/size]
[list]
[*]它无法阻止浏览器崩溃。界面恢复期间会消失几秒钟（多次崩溃后会更久）。[/*]
[*]即使游戏认为有窗口打开，Ctrl+F12 也会解除暂停。只有你用速度控制设置的暂停不受影响。[/*]
[*]残留的错误面板是通过英文或中文文字识别的。使用其他语言时，你可能需要自己关闭它。[/*]
[*]日志文件不会自动清理。如果它变得很大，请在游戏关闭时删除。[/*]
[/list]
如果遇到任何奇怪的情况，请告诉我（见下方反馈）。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.18902。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]把 [font=Courier New]WebUIDoctor.dll[/font] 放进游戏目录下的 BepInEx\plugins 文件夹。完整路径示例：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\WebUIDoctor.dll[/code][/*]
[/list]

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]WebUIDoctor.dll[/font]。

[size=5]配置[/size]
如需关闭强制重启或浏览器日志，请在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.webuidoctor.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-WebUIDoctor/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。修改会在下次启动游戏时生效。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在这里留言。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\webui-doctor.log[/font]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-WebUIDoctor]github.com/AnExiledDev/SurvivalLog-WebUIDoctor[/url]
