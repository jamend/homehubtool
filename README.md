# homehubtool

Based on https://github.com/FransVDB/BBox3SagemTool

Command line utility for getting/setting configuration options on Bell Home Hub 3000/2000, Belgacom Box 3 (BBox3), and potentially other routers based on the same generation of Sagemcom platform.

Runs on .net core 2.2.

```
Arguments:
--get [xpath]           Retrieve JSON dump of given xpath
--set [xpath] [value]   Set given xpath to given value
--url                   URL of router, default http://192.168.2.1
--username              Default admin
--password              Default admin
--help                  Show this help
```

Example:

`dotnet run --get Device > configdump.json`
