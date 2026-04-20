# TestFrameworkConfig
A Utility to manage configurations and services with per Run separation.

## Features
- Per Run configuration management
- Simple way of Overriding Configs
- Service management with per Run separation

## Usage
You should declare a ConfigInstance at the Top of your Test Class witch loads the JSON Config.

The Flow should be like this:
```
[Init Global ConfigInstance]
-> Configure Global Services / Configs
->	[Create SubConfigInstance]
	-> Configure Local Services / Configs
```

## InnerWorkings
Init ConfigInstance should get the BaseConfiguration and the BaseServices.
When creating a SubConfigInstance, the BaseConfiguration and BaseServices are inherited from the Parent ConfigInstance.

```
Init new ConfigInstance (BaseConfig, BaseServices) 
                         |           \- BaseServices can be from another ConfigInstance or Empty
                         \- BaseConfig can be from JSON or another ConfigInstance or Empty

Config the ConfigInstance \
                          |- Override Configs
                          \- Override Services

Create SubConfigInstance (BaseConfig, BaseServices) 
                          |           \- BaseServices can be from another ConfigInstance or Empty
                          \- BaseConfig can be from JSON or another ConfigInstance or Empty
```