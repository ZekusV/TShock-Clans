using TerrariaApi.Server;
using System;
using System.Collections.Generic;
using System.IO;
using TShockAPI;

namespace ClanPlugin
{
    [ApiVersion(2, 1)]
    public class ClanPlugin : TerrariaPlugin
    {
        private Dictionary<string, Clan> clans;

        public override string Author => "YourName";
        public override string Description => "Clan Plugin";
        public override string Name => "Clan Plugin";
        public override Version Version => new Version(1, 0, 0);

        public ClanPlugin(Main game) : base(game)
        {
        }

        public override void Initialize()
        {
            LoadClanData();

            Commands.ChatCommands.Add(new Command("clan.create", ClanCreate, "clancreate")
            {
                HelpText = "Create a new clan."
            });

            Commands.ChatCommands.Add(new Command("clan.leave", ClanLeave, "clanleave")
            {
                HelpText = "Leave your current clan."
            });

            Commands.ChatCommands.Add(new Command("clan.rank", ClanRankCommand, "clanrank")
            {
                HelpText = "Change a player's rank in your clan."
            });

            Commands.ChatCommands.Add(new Command("clan.accept", AcceptClanInvite, "clanaccept")
            {
                HelpText = "Accept an invitation to join a clan."
            });

            Commands.ChatCommands.Add(new Command("clan.invite", ClanInvite, "claninvite")
            {
                HelpText = "Invite a player to join your clan."
            });

            Commands.ChatCommands.Add(new Command("clan.kick", ClanKick, "clankick")
            {
                HelpText = "Kick a player from your clan."
            });

            Commands.ChatCommands.Add(new Command("clan.help", DisplayHelp, "clan")
            {
                HelpText = "Clan plugin commands."
            });

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }

            base.Dispose(disposing);
        }

        private void OnInitialize(EventArgs args)
        {
            TShockAPI.Hooks.GeneralHooks.ReloadEvent += OnReload;
        }

        private void OnLeave(LeaveEventArgs args)
        {
            var player = TShock.Players[args.Who];
            if (player != null)
            {
                if (IsInClan(player))
                {
                    SaveClanData();
                }
            }
        }

        private void OnReload(ReloadEventArgs args)
        {
            LoadClanData();
        }

        private void ClanCreate(CommandArgs args)
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
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan create <clanName>");
                return;
            }

            var clanName = args.Parameters[0];

            if (clans.ContainsKey(clanName))
            {
                player.SendErrorMessage($"Clan '{clanName}' already exists.");
                return;
            }

            var clan = new Clan(clanName, account.Name);
            clan.AddMember(account.Name, ClanRank.Owner);
            clans.Add(clanName, clan);

            player.SendSuccessMessage($"Clan '{clanName}' has been created. You are the owner.");
            SaveClanData();
        }

        private void ClanLeave(CommandArgs args)
        {
            var player = args.Player;
            var account = player.Account;

            if (!IsInClan(player))
            {
                player.SendErrorMessage("You are not in a clan.");
                return;
            }

            var clan = GetPlayerClan(player);
            if (clan.Owner == account.Name)
            {
                DisbandClan(clan);
                player.SendSuccessMessage($"You have disbanded the clan '{clan.Name}'.");
            }
            else
            {
                clan.RemoveMember(account.Name);
                player.SendSuccessMessage($"You have left the clan '{clan.Name}'.");
            }

            SaveClanData();
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
            var targetPlayerName = args.Parameters.Count > 0 ? args.Parameters[0] : "";

            if (targetPlayerName == "")
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan rank <playerName> <rank>");
                return;
            }

            var targetPlayer = TShock.Users.GetUsers().Find(u => u.Name.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase));
            if (targetPlayer == null)
            {
                player.SendErrorMessage("Player not found.");
                return;
            }

            if (!clan.IsMember(targetPlayer.Name))
            {
                player.SendErrorMessage($"Player '{targetPlayer.Name}' is not a member of your clan.");
                return;
            }

            if (clan.Owner != account.Name && clan.GetMemberRank(account.Name) != ClanRank.Admin)
            {
                player.SendErrorMessage("You do not have permission to change ranks in your clan.");
                return;
            }

            var targetRank = args.Parameters.Count > 1 ? ParseClanRank(args.Parameters[1]) : ClanRank.None;
            if (targetRank == ClanRank.None)
            {
                player.SendErrorMessage("Invalid rank. Available ranks: Member, Admin");
                return;
            }

            if (clan.Owner == targetPlayer.Name && targetRank != ClanRank.Owner)
            {
                player.SendErrorMessage("You cannot change the rank of the clan owner.");
                return;
            }

            clan.AddMember(targetPlayer.Name, targetRank);
            player.SendSuccessMessage($"Player '{targetPlayer.Name}' has been ranked as '{targetRank}'.");
            SaveClanData();
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

            if (clan.IsMember(account.Name))
            {
                player.SendErrorMessage($"You are already a member of the clan '{clanName}'.");
                return;
            }

            clan.AddMember(account.Name, ClanRank.Member);
            player.SendSuccessMessage($"You have joined the clan '{clanName}'.");
            SaveClanData();
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
            if (clan.Owner != account.Name && clan.GetMemberRank(account.Name) != ClanRank.Admin)
            {
                player.SendErrorMessage("You do not have permission to invite players to your clan.");
                return;
            }

            if (args.Parameters.Count == 0)
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan invite <playerName>");
                return;
            }

            var targetPlayerName = args.Parameters[0];
            var targetPlayer = TShock.Users.GetUsers().Find(u => u.Name.Equals(targetPlayerName, StringComparison.OrdinalIgnoreCase));
            if (targetPlayer == null)
            {
                player.SendErrorMessage("Player not found.");
                return;
            }

            if (clan.IsMember(targetPlayer.Name))
            {
                player.SendErrorMessage($"Player '{targetPlayer.Name}' is already a member of your clan.");
                return;
            }

            if (clan.IsInvited(targetPlayer.Name))
            {
                player.SendErrorMessage($"Player '{targetPlayer.Name}' has already been invited to your clan.");
                return;
            }

            clan.InviteMember(targetPlayer.Name);
            player.SendSuccessMessage($"Player '{targetPlayer.Name}' has been invited to your clan.");
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
            if (clan.Owner != account.Name && clan.GetMemberRank(account.Name) != ClanRank.Admin)
            {
                player.SendErrorMessage("You do not have permission to kick players from your clan.");
                return;
            }

            if (args.Parameters.Count == 0)
            {
                player.SendErrorMessage("Invalid syntax! Proper syntax: /clan kick <playerName>");
                return;
            }

            var targetPlayerName = args.Parameters[0];
            if (!clan.IsMember(targetPlayerName))
            {
                player.SendErrorMessage($"Player '{targetPlayerName}' is not a member of your clan.");
                return;
            }

            clan.RemoveMember(targetPlayerName);
            player.SendSuccessMessage($"Player '{targetPlayerName}' has been kicked from your clan.");
            SaveClanData();
        }

        private void DisplayHelp(CommandArgs args)
        {
            var player = args.Player;

            player.SendInfoMessage("--- Clan Plugin Commands ---");
            player.SendInfoMessage("/clan create <clanName> - Create a new clan.");
            player.SendInfoMessage("/clan leave - Leave your current clan.");
            player.SendInfoMessage("/clan rank <playerName> <rank> - Change a player's rank in your clan.");
            player.SendInfoMessage("/clan accept <clanName> - Accept an invitation to join a clan.");
            player.SendInfoMessage("/clan invite <playerName> - Invite a player to join your clan.");
            player.SendInfoMessage("/clan kick <playerName> - Kick a player from your clan.");
            player.SendInfoMessage("/clan - Clan plugin commands.");
        }

        private void LoadClanData()
        {
            if (!File.Exists("ClanData.txt"))
            {
                clans = new Dictionary<string, Clan>();
                return;
            }

            using (var reader = new StreamReader("ClanData.txt"))
            {
                var json = reader.ReadToEnd();
                clans = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Clan>>(json);
            }
        }

        private void SaveClanData()
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(clans);
            File.WriteAllText("ClanData.txt", json);
        }

        private bool IsInClan(TSPlayer player)
        {
            return GetPlayerClan(player) != null;
        }

        private Clan GetPlayerClan(TSPlayer player)
        {
            return clans.Values.FirstOrDefault(c => c.IsMember(player.Account.Name));
        }

        private void DisbandClan(Clan clan)
        {
            clans.Remove(clan.Name);
        }

        private ClanRank ParseClanRank(string rankString)
        {
            switch (rankString.ToLower())
            {
                case "admin":
                    return ClanRank.Admin;
                case "member":
                    return ClanRank.Member;
                default:
                    return ClanRank.None;
            }
        }
    }
}
