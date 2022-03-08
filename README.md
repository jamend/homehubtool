# homehubtool

Based on https://github.com/FransVDB/BBox3SagemTool

Command line utility for getting/setting configuration options on Bell Home Hub 3000, Belgacom Box 3 (BBox3), Sagemcom Fast 5655v2 AC and potentially other routers based on the same generation of Sagemcom platform.

Runs on .NET Core 3.1+ or .NET 5+.

```
Arguments:
--get [xpath]           Retrieve JSON dump of given xpath
--set [xpath] [value]   Set given xpath to given value
--url                   URL of router, default "http://192.168.2.1"
--username              Username used for authentication, default "admin"
--password              Password used as password, default "admin"
--reboot                Reboots the router
--help                  Show this help
```

Example:

Dump the device configuration into the `configdump.json` file:
`dotnet run --get Device > configdump.json`

Reboot the router after authentication
`dotnet run --url http://192.168.1.1 --username admin --password kbx5xa72 --reboot`