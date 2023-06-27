using TerrariaApi.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TShockAPI;

namespace ClanPlugin
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Name => "Clan Plugin";
        public override string Author => "Zekevious";
        public override string Description => "Allows players to create clans and manage members.";
        public override Version Version => new Version(1, 0, 0);

        private Dictionary<string, Clan> clans;

        public Plugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            LoadClanData();

            Commands.ChatCommands.Add(new Command("clan.create", CreateClan, "clancreate", "cc"));
            Commands.ChatCommands.Add(new Command("clan.leave", LeaveClan, "clanleave", "cl"));
            Commands.ChatCommands.Add(new Command("clan.rank", ClanRankCommand, "clanrank", "cr"));
            Commands.ChatCommands.Add(new Command("clan.accept", AcceptClanInvite, "clanaccept", "ca"));
            Commands.ChatCommands.Add(new Command("clan.invite", ClanInvite, "claninvite", "ci"));
            Commands.ChatCommands.Add(new Command("clan.kick", ClanKick, "clankick", "ck"));
            Commands.ChatCommands.Add(new Command("clan.help", DisplayHelp, "clanhelp", "ch"));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SaveClanData();
            }
            base.Dispose(disposing);
        }

        private void CreateClan(CommandArgs args)
        {
            var player = args.Player;
            var clanName = args.Parameters.Count > 0 ? args.Parameters[0] : string.Empty;

            if (string.IsNullOrEmpty(clanName))
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan create <clanName>");
                return;
            }

            var account = player.Account;

            if (IsInClan(player))
            {
                player.SendErrorMessage("You are already in a clan.");
                return;
            }

            if (clans.ContainsKey(clanName))
            {
                player.SendErrorMessage($"Clan '{clanName}' already exists.");
                return;
            }

            var clan = new Clan(clanName, account.Name);
            clan.AddMember(account.Name, ClanRank.Owner);
            clans.Add(clanName, clan);

            player.SendSuccessMessage($"Clan '{clanName}' created.");
        }

        private void LeaveClan(CommandArgs args)
        {
            var player = args.Player;
            var account = player.Account;

            if (!IsInClan(player))
            {
                player.SendErrorMessage("You are not in a clan.");
                return;
            }

            var clan = GetPlayerClan(player);
            var rank = GetPlayerRank(player);

            if (rank == ClanRank.Owner)
            {
                DisbandClan(clan);
                player.SendErrorMessage("Your clan has been disbanded.");
                return;
            }

            clan.RemoveMember(account.Name);
            player.SendSuccessMessage("You have left the clan.");
        }

        private void ClanRankCommand(CommandArgs args)
        {
            var player = args.Player;
            var account = player.Account;

            if (!IsInClan(player))
            {
                player.SendErrorMessage("You are not in a clan.");
                return;
            }

            var clan = GetPlayerClan(player);
            var rank = GetPlayerRank(player);

            if (rank != ClanRank.Owner)
            {
                player.SendErrorMessage("You cannot rank other players in the clan.");
                return;
            }

            if (args.Parameters.Count < 2)
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan rank <playerName> <rank>");
                return;
            }

            var playerName = args.Parameters[0];
            var targetRank = ParseClanRank(args.Parameters[1]);

            if (targetRank == ClanRank.None)
            {
                player.SendErrorMessage("Invalid rank specified.");
                return;
            }

            if (!clan.IsMember(playerName))
            {
                player.SendErrorMessage("The specified player is not a member of your clan.");
                return;
            }

            if (playerName.Equals(account.Name, StringComparison.OrdinalIgnoreCase))
            {
                player.SendErrorMessage("You cannot unrank yourself!");
                return;
            }

            clan.AddMember(playerName, targetRank);
            player.SendSuccessMessage($"Player '{playerName}' has been ranked as '{targetRank}'.");
        }

        private void AcceptClanInvite(CommandArgs args)
        {
            var player = args.Player;
            var account = player.Account;

            if (IsInClan(player))
            {
                player.SendErrorMessage("You are already in a clan.");
                return;
            }

            if (args.Parameters.Count == 0)
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan accept <clanName>");
                return;
            }

            var clanName = args.Parameters[0];

            if (!clans.ContainsKey(clanName))
            {
                player.SendErrorMessage($"Clan '{clanName}' does not exist.");
                return;
            }

            var clan = clans[clanName];

            if (!clan.CanInvite(account.Name))
            {
                player.SendErrorMessage($"You have not been invited to join clan '{clanName}'.");
                return;
            }

            clan.AddMember(account.Name, ClanRank.Member);
            player.SendSuccessMessage($"You have joined clan '{clanName}'.");
        }

        private void ClanInvite(CommandArgs args)
        {
            var player = args.Player;
            var account = player.Account;

            if (!IsInClan(player))
            {
                player.SendErrorMessage("You are not in a clan.");
                return;
            }

            var clan = GetPlayerClan(player);
            var rank = GetPlayerRank(player);

            if (rank != ClanRank.Admin && rank != ClanRank.Owner)
            {
                player.SendErrorMessage("You don't have permission to invite players to the clan.");
                return;
            }

            if (args.Parameters.Count == 0)
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan invite <playerName>");
                return;
            }

            var playerName = args.Parameters[0];

            if (clan.IsMember(playerName))
            {
                player.SendErrorMessage("This user is already in a clan!");
                return;
            }

            var inviteMessage = $"You have been invited to join '{clan.Name}'. Type '/clan accept {clan.Name}' to join.";
            var invitePlayer = TShock.Utils.FindPlayer(playerName);

            if (invitePlayer != null)
            {
                invitePlayer.SendInfoMessage(inviteMessage);
                player.SendSuccessMessage($"Invitation sent to player '{playerName}'.");
            }
            else
            {
                player.SendErrorMessage($"Player '{playerName}' is not online.");
            }
        }

        private void ClanKick(CommandArgs args)
        {
            var player = args.Player;
            var account = player.Account;

            if (!IsInClan(player))
            {
                player.SendErrorMessage("You are not in a clan.");
                return;
            }

            var clan = GetPlayerClan(player);
            var rank = GetPlayerRank(player);

            if (rank != ClanRank.Admin && rank != ClanRank.Owner)
            {
                player.SendErrorMessage("You don't have permission to kick players from the clan.");
                return;
            }

            if (args.Parameters.Count == 0)
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan kick <playerName>");
                return;
            }

            var playerName = args.Parameters[0];

            if (!clan.IsMember(playerName))
            {
                player.SendErrorMessage("The specified player is not a member of your clan.");
                return;
            }

            clan.RemoveMember(playerName);
            player.SendSuccessMessage($"Player '{playerName}' has been kicked from the clan.");
        }

        private void DisplayHelp(CommandArgs args)
        {
            var player = args.Player;

            player.SendInfoMessage("==== Clan Plugin ====");
            player.SendInfoMessage("/clan create <clanName> - Create a new clan.");
            player.SendInfoMessage("/clan leave - Leave your current clan.");
            player.SendInfoMessage("/clan rank <playerName> <rank> - Change a player's rank in your clan.");
            player.SendInfoMessage("/clan accept <clanName> - Accept an invitation to join a clan.");
            player.SendInfoMessage("/clan invite <playerName> - Invite a player to join your clan.");
            player.SendInfoMessage("/clan kick <playerName> - Kick a player from your clan.");
        }

        private bool IsInClan(TSPlayer player)
        {
            return clans.Values.Any(clan => clan.IsMember(player.Account.Name));
        }

        private Clan GetPlayerClan(TSPlayer player)
        {
            return clans.Values.FirstOrDefault(clan => clan.IsMember(player.Account.Name));
        }

        private ClanRank GetPlayerRank(TSPlayer player)
        {
            var clan = GetPlayerClan(player);
            return clan?.GetMemberRank(player.Account.Name) ?? ClanRank.None;
        }

        private ClanRank ParseClanRank(string rank)
        {
            switch (rank.ToLowerInvariant())
            {
                case "member":
                    return ClanRank.Member;
                case "admin":
                    return ClanRank.Admin;
                case "owner":
                    return ClanRank.Owner;
                default:
                    return ClanRank.None;
            }
        }

        private void LoadClanData()
        {
            var path = Path.Combine(TShock.SavePath, "ClanData.txt");

            if (!File.Exists(path))
            {
                clans = new Dictionary<string, Clan>();
                return;
            }

            using (var reader = new StreamReader(path))
            {
                clans = new Dictionary<string, Clan>();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    var data = line.Split(',');

                    if (data.Length >= 3)
                    {
                        var clanName = data[0];
                        var ownerName = data[1];
                        var memberData = data.Skip(2).ToArray();

                        var clan = new Clan(clanName, ownerName);
                        clans.Add(clanName, clan);

                        foreach (var member in memberData)
                        {
                            var memberInfo = member.Split(':');

                            if (memberInfo.Length == 2)
                            {
                                var playerName = memberInfo[0];
                                var memberRank = (ClanRank)Enum.Parse(typeof(ClanRank), memberInfo[1]);
                                clan.AddMember(playerName, memberRank);
                            }
                        }
                    }
                }
            }
        }

        private void SaveClanData()
        {
            var path = Path.Combine(TShock.SavePath, "ClanData.txt");

            using (var writer = new StreamWriter(path))
            {
                foreach (var clan in clans.Values)
                {
                    var clanData = $"{clan.Name},{clan.Owner}";

                    foreach (var member in clan.Members)
                    {
                        var memberData = $"{member.Key}:{member.Value}";
                        clanData += $",{memberData}";
                    }

                    writer.WriteLine(clanData);
                }
            }
        }

        private void DisbandClan(Clan clan)
        {
            clans.Remove(clan.Name);
        }

        private class Clan
        {
            public string Name { get; }
            public string Owner { get; }
            public Dictionary<string, ClanRank> Members { get; }

            public Clan(string name, string owner)
            {
                Name = name;
                Owner = owner;
                Members = new Dictionary<string, ClanRank>();
            }

            public void AddMember(string playerName, ClanRank rank)
            {
                Members[playerName] = rank;
            }

            public void RemoveMember(string playerName)
            {
                Members.Remove(playerName);
            }

            public bool IsMember(string playerName)
            {
                return Members.ContainsKey(playerName);
            }

            public ClanRank GetMemberRank(string playerName)
            {
                return Members.TryGetValue(playerName, out var rank) ? rank : ClanRank.None;
            }

            public bool CanInvite(string playerName)
            {
                var rank = GetMemberRank(playerName);
                return rank == ClanRank.Admin || rank == ClanRank.Owner;
            }
        }
    }
}
