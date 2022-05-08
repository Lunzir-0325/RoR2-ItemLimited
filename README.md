[![Support Lunzir](https://img.shields.io/badge/Support-Lunzir-ff6482)](https://ko-fi.com/lunzir0325)
[![支持我](https://img.shields.io/badge/Support-支持我-ff6482)](http://note.youdao.com/noteshare?id=0f269e09eb25d7f00285e815a48f835d&sub=28F42AE4219D453FB3E383B0A4ECA9FB)
*QQ：409790315*
*E-mail: 409790315@qq.com*
# RoR2-ItemLimited
## Information 介绍
- [√] Server - side
- [√] Client - side
- [√] Items
- [√] Equipments
- [√] Unmodded
- [√] Support Language API

Limit item and convert excess items to scraps. You can write the number you want to limit in the configuration file. No need unmoded , client and server are available.

The configuration file will reload before a new run, so you can edit it and start again without restarting the application.

You can get the item code [here.](https://gist.github.com/Lunzir-0325/8f375c6504a64f6c88f35259470659ee) You can also type the command ```show_limit_list``` in the game.

限制物品上限，多出部分转换成碎片，可在配置文件写上你要限制的数量，用法：物品代码-数量，详情可以看配置的例子。适用专用服务器和本地主机，房主有mod就可，不需要unmoded。[物品代码](https://gist.github.com/Lunzir-0325/8f375c6504a64f6c88f35259470659ee)我有分享在我的github。也可以在游戏里面输入指令```show_limit_list```查看数据。

配置文件会在每开局前加载，所以你可以编辑好再开局，不用重启游戏。

麻了，写mod针累，一睁一闭眼又一天，求求疫情快过去吧，我要搬砖买吃的，家里都快没小鱼干喂了。。

2022年3月23日

暂时解封了，下雨了，重新开始...

2022年3月28日

## Config Settings 默认配置
| Setting| Default Value| 
|---|---:|
[Setting设置]|
EnableMod|true
ItemCode|LunarDagger-3,ExplodeOnDeath-3,ExecuteLowHealthElite-2,StrengthenBurn-3,Firework-0,Dagger-0,MoreMissile-0...
EquipmentCode|Blackhole...
EnableAutoScrap|true
~~EnableTier1Limit~~|~~true~~
~~EnableTier2Limit~~|~~true~~
~~EnableTier3Limit~~|~~true~~
~~EnableBossLimit~~|~~true~~
~~EnableLunarLimit~~|~~true~~
~~EnableVoidLimit~~|~~true~~

English not my first language, hope you can understand what I am mean. : }

## What's Next 以后的想法
- [√] Add equipment limit.
- [ ] Developing ban items

## Known issues 已知问题
- None.

## Changelog 更新日志
#### 1.1.6 2022年4月28日 v1.2.3.1
- Update to lasted version of the game. 更新至游戏新版本
#### 1.1.52 2022年4月2日 v1.2.2.0
- Test upload language pack. 测试上传语言包 
#### 1.1.51 2022年3月28日 v1.2.2.0
- Forgot to add the language pack. No one reminded me... 忘记添加语言包。。没搞提醒我
#### 1.1.5 2022年3月28日 v1.2.2.0
- Released as stable. 更稳定的版本。
#### 1.1.4 2022年3月28日 v1.2.2.0
- Fixed command show_limit_list information display error. 修复指令show_limit_list信息显示错误。
#### 1.1.3 2022年3月28日 v1.2.2.0
- Fixed code and now added multiple item codes such as pearls. 修复代码，现在增加珍珠等多个物品代码。
- Added console command to view current item restrictions. 新增控制台指令，可查看当前物品限制情况。
- Remove multiple configuration options. 去除多个配置选项
#### 1.1.1 2022年3月27日 v1.2.2.0
- Fix serious bugs. 修复严重bug.
#### 1.1.0 2022年3月26日 v1.2.2.0
- Added equipment limit. 增加主动装备限制。
- Support for multiple languages. I'm using Google Translate, please forgive me if the translation is not accurate. 支持多国语言。
#### 1.0.0 2022年3月23日
- First Release 首发


