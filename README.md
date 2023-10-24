# AudioLink
AudioLink is a [Touch Portal](https://www.touch-portal.com) plugin that allows you to control each audio device separately with sliders or buttons
(May not work on Linux or Mac)


- [AudioLink](#AudioLink)
  - [Installation](#installation) 
  - [Plugin Capabilities](#plugin-capabilities)
    - [Actions](#actions)
    - [Sliders](#sliders)
    - [States](#states)
  - [Settings](#settings)
  - [ChangeLog](#changelog)
  - [Dependencies](#dependencies)
  - [Authors](#authors)

## Installation
1. Download the [latest version](https://github.com/DataNext27/TouchPortal_AudioLink/releases/tag/1.1.0) of the plugin
2. Open Touch Portal
   - Click the settings button
   - Click import plugin
   - Find the plugin file you've just downloaded and open it
3. Wait a bit till it finish loading
4. Now start setting up buttons or sliders


## Plugin Capabilities
### Actions
 - Increase / Decrease volume
 - Mute device

### Sliders
 - Increase / Decrease volume
   
### States
 - Device Mute
   - Valid Values: Muted, Unmuted
   - Note: Can be changed in settings
 - Device Name
   - Value: Chosen device name 
 - Device Volume
   - Values: Chosen device volume
   - Note: Volume is from 0 to 100

## Settings
 - Update Interval
   - Value: in ms
   - Default: 2000
   - Min : 2000
   - Max: 20000
   - Note: Time interval in ms between values updates from PC to Touch Portal (Not from Touch Portal to PC, in that case this is instant), required to prevent processor overload
 - Muted States Names
   - Values: text
   - Default: Muted,Unmuted
   - How To Use: {Muted Text},{Unmuted text} (the "," is required)
   - Note: Just for customize state in button text

## ChangeLog
```
v0.2
  - Increase / Decrease volume
  - Mute device
v1.0.0
  Additions:
    - Added notifications system for updates
v1.1.0
  Additions:
    - Added Device name, volume and muted states
    - Added "update interval" and "muted states names" settings
v1.1.1
  Additions:
    - Added an icon
v1.1.2
  Fixes:
    - Fixed a bug where some states would not appear
```

## Dependencies
 - [TouchPortal-CS-API](https://github.com/mpaperno/TouchPortal-CS-API)
 - [AudioSwitcher](https://github.com/xenolightning/AudioSwitcher)

## Authors
 - Made by DataNext

Thanks to:
 - Touch Portal Creators for Touch Portal App
 - [mpaperno](https://github.com/mpaperno) for the Touch Portal C# API
 - [xenolightning](https://github.com/xenolightning) for AudioSwitcher Library
