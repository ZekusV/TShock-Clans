using System;
using Terraria;
using TShockAPI;

namespace ClanPlugin
{
    public static class PluginInformation
    {
        public static readonly string Author = "Zekevious";
        public static readonly string Description = "Clan Plugin";
        public static readonly string Name = "ClanPlugin";
        public static readonly Version Version = new Version(1, 0, 0);

        public static void Initialize()
        {
            TShock.Log.ConsoleInfo($"Loading {Name} v{Version} by {Author}");
        }
    }
}
