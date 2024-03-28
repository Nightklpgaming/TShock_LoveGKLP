using ClientApi.Networking;

//microsoft
using Microsoft.Xna.Framework;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json;

//system
using MySqlX.XDevAPI;
using System;
using System.Data;
using System.Reflection.Metadata.Ecma335;
//terraria
using Terraria;
using TerrariaApi.Server;
using IL.Terraria.GameContent.UI;
using On.Terraria.GameContent.UI;
using Terraria.GameContent.UI;
//tshock
using TShockAPI;
using TShockAPI.Hooks;
using TShockAPI.Net;



namespace LoveGKLP
{
    [ApiVersion(2, 1)]
    public class LoveGKLP : TerrariaPlugin
    {
        #region Plugin Info
        public override string Name => "LoveGKLP";

        public override string Author => "NightKLP";

        public override string Description => "A simple plugin for players to have love ( best use for valentines )";

        public override Version Version => new Version(1, 0, 1);
        #endregion

        private IDbConnection GetDatabase;
        public static Database.DBManager DB;

        //actions
        public static Dictionary<string, string> PendingPersonalActions = new();
        public static Dictionary<string, string> Actionawait = new();
        //finding love
        public static Dictionary<string, string> PendingActions = new();
        public static Dictionary<string, string> OnRejectLocked = new();

        //others
        public static Dictionary<string, string> ActivePartners = new();//optimize database gathering
        public Dictionary<string, DateTime> OnCoolDown = new();
        public static int emoteid = 0;
        public LoveGKLP(Main game) : base(game)
        {
            //amogus
        }

        public override void Initialize()
        {
            GetDatabase = new SqliteConnection(("Data Source=" + Path.Combine(TShock.SavePath, "LoveGKLP.sqlite")));
            DB = new Database.DBManager(GetDatabase);

            //=====================Player===================
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnServerLeave);
            //=====================Server===================
            ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);

            #region commands
            //main
            Commands.ChatCommands.Add(new Command("lovegklp.love", MainCommand, "love"));
            Commands.ChatCommands.Add(new Command("lovegklp.love", PlayerInfo, "loveplayerinfo"));
            Commands.ChatCommands.Add(new Command("lovegklp.default.findlove", GetLove, "findlove"));

            //admin
            Commands.ChatCommands.Add(new Command("lovegklp.admin", AdminCommand, "adminlove"));

            //pending actions
            Commands.ChatCommands.Add(new Command("lovegklp.default.pendingaction", AcceptAction, "loveaccept"));
            Commands.ChatCommands.Add(new Command("lovegklp.default.pendingaction", RejectAction, "lovereject"));


            //execution
            Commands.ChatCommands.Add(new Command("lovegklp.default.divorce", Divorce, "divorce"));
            Commands.ChatCommands.Add(new Command("lovegklp.tp", TPCouple, "tplove"));
            Commands.ChatCommands.Add(new Command("lovegklp.whisper", WhisperCouple, "whisperlove", "wlove"));
            Commands.ChatCommands.Add(new Command("lovegklp.aid", AIDCommand, "aidlove"));//suggestion cmd
            #endregion
        }

        protected override  void Dispose(bool disposing)
        {
            if (disposing)
            {

                // Deregister hooks here

                //=====================Player===================
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnServerLeave);

                //=====================Server===================
                ServerApi.Hooks.GameUpdate.Deregister(this, OnUpdate);
            }
            base.Dispose(disposing);
        }

        private void OnUpdate(EventArgs e)
        {
            #region code
            foreach (var actions in  Actionawait)
            {
                if (actions.Key.Split("_")[1] == "kiss")
                {
                    KissAction(actions.Key.Split("_")[0], actions.Value);
                }
            }
            foreach (var partner in ActivePartners)
            {
                CoupleBuff(partner.Key.Split("_")[0], partner.Key.Split("_")[1], partner.Value);
            }
            #endregion
        }

        private void OnServerJoin(JoinEventArgs args)
        {
            #region code
            try
            {
                Partner partner = DB.GetPartnerByName(Main.player[args.Who].name);
                foreach (var check in ActivePartners)
                {
                    if (check.Key.Split("_")[0] == partner.Player1) return;
                }
                ActivePartners.Add(partner.Player1 + "_" + partner.Player2, partner.Type);
            } catch (NullReferenceException) { }
            #endregion
        }

        private void OnServerLeave(LeaveEventArgs args)
        {
            #region code
            try
            {
                Partner partner = DB.GetPartnerByName(Main.player[args.Who].name);
                foreach (var check in ActivePartners)
                {
                    if (check.Key.Split("_")[0] == partner.Player1)
                    {
                        var find1 = TSPlayer.FindByNameOrID(partner.Player1);
                        var find2 = TSPlayer.FindByNameOrID(partner.Player2);
                        if (find1.Count == 0 && find2.Count == 0)
                        {
                            ActivePartners.Remove(check.Key);
                        }
                    }
                    if (check.Key.Split("_")[1] == partner.Player2)
                    {
                        var find1 = TSPlayer.FindByNameOrID(partner.Player1);
                        var find2 = TSPlayer.FindByNameOrID(partner.Player2);
                        if (find1.Count == 0 && find2.Count == 0)
                        {
                            ActivePartners.Remove(check.Key);
                        }
                    }
                }
                
            }
            catch (NullReferenceException) { }
            #endregion
        }

        #region commands

        //main
        private void MainCommand(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }

            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Usage: /love <sub-command> <args...>" +
                    "\ndo ( /love help ) for more info");
                return;
            }
            string helptext = "[i:58] [c/e210c2:Love Concept Info] [i:58]" +
                "\n> hello there in order to register your gender do [c/ffffff:( /love register <your gender> )]" +
                "\nif you do not permission to register that means you need a permission to a staff to approve your gender!" +
                "\n" +
                "\n\n[c/e210c2:[ sub commands ][c/e210c2:]]" +
                "\n+yes: [c/ffffff:accepts someone's pending request ]" +
                "\n+no: [c/ffffff:rejects someone's pending request ]" +
                "\n+kiss: [c/ffffff: sent a request to a player you want to kiss ( if you have a partner you can only kiss him/her )]" +
                "\n" +
                "\n\n[c/e210c2:[ finding your love ][c/e210c2:]]" +
                "\n'/loveplayerinfo <playername>' [c/ffffff:get information of a player who already register]" +
                "\n'/findlove <playername>' [c/ffffff:sent's a request to someone you want to be your partner...]" +
                "\n'/loveaccept' [c/ffffff:accepts someone pending request to be your partner...]" +
                "\n'/lovereject' [c/ffffff:rejects someone pending request to be your partner...]" +
                "\n" +
                "\n\n[c/e210c2:[ partner commands ][c/e210c2:]]" +
                "\n'/tplove' [c/ffffff:teleport to your partner even they have /tpallow on]" +
                "\n'/whisperlove <message>' or '/wlove <message>' [c/ffffff:Send a private message to your partner without any problems!]" +
                "\n'/aidlove' [c/ffffff:heals you and your partner with 200hp... you only use it 6 minutes each use it wisely" +
                "\n[c/ff2e09:'/divorce'] [c/ff5537:break up with your partner anytime...]";
            switch (args.Parameters[0])
            {
                case "help":
                    executer.SendMessage(helptext, Color.Pink);
                    return;
                case "yes":
                    #region code

                    foreach (var get in Actionawait)
                    {
                        if (get.Key.Split("_")[0] == executer.Name && get.Key.Split("_")[1] == "request")
                        {
                            var getplayer1 = TSPlayer.FindByNameOrID(get.Value);
                            if (getplayer1.Count == 0)
                            {
                                executer.SendErrorMessage("seems he's not online...");
                                return;
                            }
                            switch (get.Key.Split("_")[2])
                            {
                                case "kiss":
                                    Actionawait.Remove(get.Key);
                                    Actionawait.Add($"{get.Key.Split("_")[0]}_kiss", get.Value);
                                    executer.SendMessage($"[i:58] now you can kiss {getplayer1[0].Name} < get closer >", Color.Pink);
                                    getplayer1[0].SendMessage($"[i:58] [c/ffffff:{executer.Name}] Accepted!", Color.Lime);
                                    return;
                            }
                        }
                    }
                    executer.SendErrorMessage("You have no pending request!");
                    return;
                    #endregion

                case "no":
                    #region code

                    foreach (var get in Actionawait)
                    {
                        if (get.Key.Split("_")[0] == executer.Name && get.Key.Split("_")[1] == "request")
                        {
                            var getplayer1 = TSPlayer.FindByNameOrID(get.Value);
                            if (getplayer1.Count == 0)
                            {
                                executer.SendErrorMessage("seems he's not online...");
                                return;
                            }
                            switch (get.Key.Split("_")[1])
                            {
                                case "kiss":
                                    Actionawait.Remove(get.Key);
                                    executer.SendMessage($"[i:58] you said no to {getplayer1[0].Name}", Color.Pink);
                                    getplayer1[0].SendMessage($"[i:58] [c/ffffff:{executer.Name}] doesn't want to kiss you", Color.Red);
                                    return;
                            }
                        }
                    }
                    executer.SendErrorMessage("You have no pending request!");

                    return;
                    #endregion

                case "register":
                    #region code
                    if (!executer.HasPermission("lovegklp.register"))
                    {
                        executer.SendErrorMessage("you cannot register your gender yet" +
                            "\nincase you want too you need a staff to approved your gender!");
                        return;
                    }
                    try
                    {
                        usergklp check = DB.GetPlayerByName(executer.Name);
                        executer.SendErrorMessage("you already set your Gender!");
                        return;
                    } catch(NullReferenceException)
                    {
                        if (args.Parameters.Count == 1)
                        {
                            args.Player.SendErrorMessage("please specify your gender!" +
                                "\nproper syntax: /love register <your gender>" +
                                "\ngender: male, female, boy, girl");
                            return;
                        }
                        string getgender;
                        switch (args.Parameters[1].ToLowerInvariant())
                        {
                            case "male":
                            case "boy":
                                getgender = "male";
                                break;
                            case "female":
                            case "girl":
                                getgender = "female";
                                break;
                            default:
                                executer.SendErrorMessage("please pick your gender properly!");
                                return;
                        }
                        DB.NewPlayer(executer.Name, getgender);

                        executer.SendMessage("Successfully registered!" +
                            $"\nyour name is [c/ffffff:{executer.Name}]" +
                            $"\nyour gender is [c/ffffff:{getgender}]", Color.Green);
                    }
                    return;
                #endregion

                //actions
                case "kiss":
                    #region code
                    try
                    {
                        Partner partner = DB.GetPartnerByName(executer.Name);

                        string couplename = "";
                        if (partner.Player1 == executer.Name) couplename = partner.Player2;
                        if (partner.Player2 == executer.Name) couplename = partner.Player1;
                        var getcouple = TSPlayer.FindByNameOrID(couplename);
                        if (getcouple.Count == 0)
                        {
                            executer.SendErrorMessage("Your partner is not online!");
                            return;
                        }

                        Actionawait.Add($"{couplename}_kiss", executer.Name);

                        executer.SendMessage("[i:58] get closer to kiss your partner!", Color.Pink);
                        getcouple[0].SendMessage($"[i:58] {executer.Name} wants to give you a kiss" +
                            $"\ngo closer to your partner!", Color.Pink);
                    } catch (NullReferenceException)
                    {

                        if (args.Parameters.Count == 1)
                        {
                            executer.SendErrorMessage("please specify a player!");
                            return;
                        }

                        var getplayer4 = TSPlayer.FindByNameOrID(args.Parameters[1]);
                        if (getplayer4.Count == 0)
                        {
                            executer.SendErrorMessage("Invalid Player!");
                            return;
                        }

                        try
                        {
                            Partner check = DB.GetPartnerByName(getplayer4[0].Name);
                            executer.SendErrorMessage($"{getplayer4[0].Name} already has a partner!");
                            return;
                        } catch (NullReferenceException) { }

                        foreach (var check in Actionawait)
                        {
                            if (check.Key.Split("_")[0] == getplayer4[0].Name && check.Value == "request")
                            {
                                executer.SendErrorMessage($"{getplayer4[0].Name} is on pending request...");
                                return;
                            }
                        }

                        Actionawait.Add($"{getplayer4[0].Name}_request_kiss", executer.Name);
                        executer.SendMessage("[i:58] request sent!", Color.Pink);
                        getplayer4[0].SendMessage($"[i:58] {executer.Name} wants give you a kiss" +
                            $"\n[c/3cdb02:( /love yes ) if yes] [c/db6402:( /love no ) if no]", Color.Pink);
                    }
                    return;
                    #endregion
                    
                default:
                    executer.SendErrorMessage("please specify a sub-command!");
                    return;
            }
            #endregion
        }

        private void AdminCommand(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }

            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("Usage: /adminlove <sub-command> <args...>" +
                    "\ndo ( /adminlove help ) for more info");
                return;
            }
            string helptext = "[i:58] Admin love Sub-Commands [i:58]" +
                "\napprovegender: [c/ffffff:register someones gender ( player your picking must be online )]" +
                "\nsetgender: [c/ffffff:change someones gender]" +
                "\nsetasmarried: [c/ffffff:approve those couples if they are really married in-game ( you can set it as not married )]" +
                "\nforcedivorce: [c/ffffff: force to divorce someone!]";
            switch (args.Parameters[0])
            {
                case "help":
                    executer.SendMessage(helptext, Color.Pink);
                    return;
                case "approvegender":
                    #region code
                    if (args.Parameters.Count == 1)
                    {
                        executer.SendErrorMessage("please specify a player\n" +
                            "syntax: /adminlove setgender <playername> <girl or boy>");
                        return;
                    }

                    try
                    {
                        string targetname1 = args.Parameters[1];
                        var gettarget1 = TSPlayer.FindByNameOrID(args.Parameters[1]);
                        if (gettarget1.Count > 0)
                        {
                            targetname1 = gettarget1[0].Name;
                        }
                        usergklp gettarget = DB.GetPlayerByName(targetname1);

                        executer.SendErrorMessage($"Player Named {targetname1} has already registered their gender!" +
                            "\n if u want to change their gender do ( /adminlove setgender ) instead!");
                    }
                    catch (NullReferenceException)
                    {
                        var gettarget1 = TSPlayer.FindByNameOrID(args.Parameters[1]);
                        if (gettarget1.Count == 0)
                        {
                            executer.SendErrorMessage("Invalid player!");
                            return;
                        }

                        if (args.Parameters.Count == 2)
                        {
                            executer.SendErrorMessage("please specify if its a girl or a boy!");
                            return;
                        }

                        switch (args.Parameters[2])
                        {
                            case "male":
                            case "boy":
                                if (DB.NewPlayer(gettarget1[0].Name, "male"))
                                {
                                    executer.SendMessage($"[i:29] {gettarget1[0].Name} is registered as male...", Color.DeepPink);
                                    gettarget1[0].SendMessage($"[i:29] {executer.Name} Set your gender as a boy!", Color.DeepPink);
                                }
                                else
                                {
                                    executer.SendErrorMessage("something went wrong...");
                                }
                                return;
                            case "female":
                            case "girl":
                                if (DB.NewPlayer(gettarget1[0].Name, "female"))
                                {
                                    executer.SendMessage($"[i:29] {gettarget1[0].Name} is register as girl...", Color.DeepPink);
                                    gettarget1[0].SendMessage($"[i:29] {executer.Name} Set your gender as a girl!", Color.DeepPink);
                                }
                                else
                                {
                                    executer.SendErrorMessage("something went wrong...");
                                }
                                return;
                            default:
                                executer.SendErrorMessage("please specify if its a girl or a boy!");
                                return;
                        }
                    }
                    return;
                    #endregion

                case "setgender":
                    #region code
                    if (args.Parameters.Count == 1)
                    {
                        executer.SendErrorMessage("please specify a player\n" +
                            "syntax: /adminlove setgender <playername> <girl or boy>");
                        return;
                    }

                    try
                    {
                        string targetname1 = args.Parameters[1];
                        var gettarget1 = TSPlayer.FindByNameOrID(args.Parameters[1]);
                        if (gettarget1.Count > 0)
                        {
                            targetname1 = gettarget1[0].Name;
                        }
                        usergklp gettarget = DB.GetPlayerByName(targetname1);

                        if (args.Parameters.Count == 2)
                        {
                            executer.SendErrorMessage("please specify if its a girl or a boy!");
                            return;
                        }

                        switch (args.Parameters[2])
                        {
                            case "male":
                            case "boy":
                                if (gettarget.Gender == "male")
                                {
                                    executer.SendErrorMessage("This player is already a boy!");
                                    return;
                                }
                                if (DB.SetPlayerGender(gettarget.Name, "male"))
                                {
                                    executer.SendMessage($"[i:29] Set {gettarget.Name} as male...", Color.DeepPink);
                                } else
                                {
                                    executer.SendErrorMessage("something went wrong...");
                                }
                                return;
                            case "female":
                            case "girl":
                                if (gettarget.Gender == "female")
                                {
                                    executer.SendErrorMessage("This player is already a girl!");
                                    return;
                                }
                                if (DB.SetPlayerGender(gettarget.Name, "female"))
                                {
                                    executer.SendMessage($"[i:29] Set {gettarget.Name} as girl...", Color.DeepPink);
                                }
                                else
                                {
                                    executer.SendErrorMessage("something went wrong...");
                                }
                                return;
                            default:
                                executer.SendErrorMessage("please specify if its a girl or a boy!");
                                return;
                        }
                    }
                    catch (NullReferenceException)
                    {
                        executer.SendErrorMessage("Invalid Player!");
                        return;
                    }
                    return;
                    #endregion

                case "setasmarried":
                    #region code
                    if (args.Parameters.Count == 1)
                    {
                        executer.SendErrorMessage("please specify a player\n" +
                            "syntax: /adminlove setasmarried <playername> <yes or no>");
                        return;
                    }
                    
                    try
                    {
                        string targetname1 = args.Parameters[1];
                        var gettarget1 = TSPlayer.FindByNameOrID(args.Parameters[1]);
                        if (gettarget1.Count > 0)
                        {
                            targetname1 = gettarget1[0].Name;
                        }
                        try
                        {
                            Partner getpartner = DB.GetPartnerByName(targetname1);


                            if (args.Parameters.Count == 2)
                            {
                                executer.SendErrorMessage("please specify if its yes or no");
                                return;
                            }

                            switch (args.Parameters[2])
                            {
                                case "true":
                                case "yes":
                                    if (getpartner.Type == "married")
                                    {
                                        executer.SendErrorMessage("They are already Married!");
                                        return;
                                    }

                                    if (DB.SetPartnerType(getpartner.Player1, "married"))
                                    {
                                        TSPlayer.All.SendMessage($"[i:29] [c/ffffff: {getpartner.Player1} and {getpartner.Player2}] are offically married!", Color.DeepPink);
                                        executer.SendMessage($"[i:29] Set as ( {getpartner.Player1} X {getpartner.Player2} ) as Married!", Color.DeepPink);
                                    }
                                    else
                                    {
                                        executer.SendErrorMessage("something went wrong...");
                                    }

                                    return;
                                case "false":
                                case "no":
                                    if (getpartner.Type != "married")
                                    {
                                        executer.SendErrorMessage("They are not Married!");
                                        return;
                                    }

                                    if (DB.SetPartnerType(getpartner.Player1, "couple"))
                                    {
                                        TSPlayer.All.SendMessage($"[i:29] Seems [c/ffffff: {getpartner.Player1} and {getpartner.Player2}] are not married!", Color.DeepPink);
                                        executer.SendMessage($"[i:29] Set as ( {getpartner.Player1} X {getpartner.Player2} ) as couples", Color.DeepPink);
                                    } else
                                    {
                                        executer.SendErrorMessage("something went wrong...");
                                    }
                                    return;
                                default:
                                    executer.SendErrorMessage("please specify if its yes or no");
                                    return;
                            }
                        } catch (NullReferenceException)
                        {
                            executer.SendErrorMessage("Seems ths Player doesn't have a Partner!");
                            return;
                        }
                    } catch (NullReferenceException)
                    {
                        executer.SendErrorMessage("Invalid Player!");
                        return;
                    }
                    return;
                #endregion

                case "forcedivorce":
                    #region code
                    if (args.Parameters.Count == 1)
                    {
                        executer.SendErrorMessage("please specify a player\n" +
                            "syntax: /adminlove setasmarried <playername> <yes or no>");
                        return;
                    }

                    try
                    {
                        string targetname1 = args.Parameters[1];
                        var gettarget1 = TSPlayer.FindByNameOrID(args.Parameters[1]);
                        if (gettarget1.Count > 0)
                        {
                            targetname1 = gettarget1[0].Name;
                        }
                        try
                        {
                            Partner getpartner = DB.GetPartnerByName(targetname1);

                            DB.DeletePartner(getpartner.Player1);
                            ActivePartners.Remove(getpartner.Player1);
                            ActivePartners.Remove(getpartner.Player2);

                            TSPlayer.All.SendMessage($"[i:29] {executer.Name} forced [c/ffffff:{getpartner.Player1} and {getpartner.Player2}] to break up!!!", Color.Orange);
                            executer.SendMessage($"[i:29] {getpartner.Player1} and {getpartner.Player2} are force to break up", Color.DeepPink);
                        }
                        catch (NullReferenceException)
                        {
                            executer.SendErrorMessage("Seems ths Player doesn't have a Partner!");
                            return;
                        }
                    }
                    catch (NullReferenceException)
                    {
                        executer.SendErrorMessage("Invalid Player!");
                        return;
                    }
                    return;
                    #endregion

                default:
                    executer.SendErrorMessage("please specify a sub-command!");
                    return;
            }
                #endregion
        }

        private void PlayerInfo(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("please specify a player!" +
                    "\nproper syntax: /loveplayerinfo <playername>");
                return;
            }

            string targetname = string.Join(" ", args.Parameters.ToArray(), 0, args.Parameters.Count);

            var gettarget = TSPlayer.FindByNameOrID(targetname);
            if (gettarget.Count > 0)
            {
                targetname = gettarget[0].Name;
            }


            try
            {
                usergklp playergklp = DB.GetPlayerByName(targetname);

                string[] text = { "n/a", $"[c/8cff1f:{playergklp.Name} is Single]" };
                if (playergklp.Gender == "male") text[0] = "[c/001ee6:Male]";
                if (playergklp.Gender == "female") text[0] = "[c/f813ac:Female]";

                try
                {
                    Partner partner = DB.GetPartnerByName(playergklp.Name);
                    string[] genders = { "n/a", "n/a" };

                    usergklp user1 = DB.GetPlayerByName(partner.Player1);
                    if (user1.Gender == "male") genders[0] = $"[c/304af8:{partner.Player1} (B)]";
                    if (user1.Gender == "female") genders[0] = $"[c/fa40bc:{partner.Player1} (G)]";
                    usergklp user2 = DB.GetPlayerByName(partner.Player2);
                    if (user2.Gender == "male") genders[1] = $"[c/304af8:{partner.Player2} (B)]";
                    if (user2.Gender == "female") genders[1] = $"[c/fa40bc:{partner.Player2} (G)]";


                    text[1] = $"{genders[0]} and {genders[1]} [c/b613f8:are {partner.Type}]";

                }
                catch (NullReferenceException) { }

                executer.SendMessage("[i:58] Player Info [i:58]" +
                    $"\nName: [c/ffffff:{playergklp.Name}]" +
                    $"\nGender: {text[0]}" +
                    $"\n--[Status]--" +
                    $"\n{text[1]}", Color.Yellow);

            }
            catch (NullReferenceException)
            {
                if (gettarget.Count > 0)
                {
                    executer.SendErrorMessage("This player hasn't been registered yet!");
                } else
                {
                    executer.SendErrorMessage("Invalid Player!");
                }
                
                return;
            }

            return;
            #endregion
        }

        private void GetLove(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }
            if (args.Parameters.Count == 0)
            {
                args.Player.SendErrorMessage("please specify a player if you wanna find love" +
                    "\nproper syntax: /findlove <player>" +
                    "\na player your choosing must already register their gender specialy you!" +
                    "\nYou can do ( /love playerinfo <playername> ) to see if they are boy/girl or not registered...");
                return;
            }
            
            try
            {
                usergklp executeruser = DB.GetPlayerByName(executer.Name);
                try
                {
                    Partner check = DB.GetPartnerByName(executer.Name);
                    executer.SendErrorMessage("You already have a Partner with you!" +
                        "\nif you don't like with your partner do /divorce");
                    return;
                }
                catch (NullReferenceException)
                {
                    var players = TSPlayer.FindByNameOrID(args.Parameters[0]);
                    if (players.Count == 0)
                    {
                        executer.SendErrorMessage("Invalid Player...");
                        return;
                    }
                    try
                    {
                        usergklp target = DB.GetPlayerByName(players[0].Name);
                        if (players[0].Name == executer.Name)
                        {
                            executer.SendErrorMessage("you can't request yourself to be partner!?!");
                            return;
                        }
                        try
                        {
                            DB.GetPartnerByName(players[0].Name);
                            executer.SendErrorMessage("this player has already have a partner!");
                            return;
                        } catch (NullReferenceException) { }
                        foreach (var check in OnRejectLocked)
                        {
                            if (check.Key == players[0].Name && check.Value == executer.Name)
                            {
                                executer.SendMessage($"[c/db0202:[X][c/db0202:]] Sorry bro {players[0].Name} doesn't wanna be with you :(", Color.Orange);
                                return;
                            }
                        }

                        if (executeruser.Gender == target.Gender)
                        {
                            executer.SendErrorMessage("please don't be gay");
                            return;
                        }

                        foreach (var check in PendingActions)
                        {
                            if (check.Key == players[0].Name)
                            {
                                executer.SendErrorMessage($"Player named {players[0].Name} is on Pending request by someone or you...");
                                return;
                            }
                        }

                        executer.SendMessage("Your Request has been sent!" +
                            $"\nplayername: {target.Name} Gender: {target.Gender}", Color.Pink);

                        string text = "n/a";

                        if (executeruser.Gender == "male") text = "[c/304af8:boyfriend]";
                        if (executeruser.Gender == "female") text = "[c/fa40bc:girlfriend]";

                        players[0].SendMessage($"[i:58] {executer.Name} Wants to be your {text}!" +
                            $"\nwould you accept? [c/3cdb02:/loveaccept if yes] [c/db6402:/lovereject if no] or! [c/db0202:( /lovereject force ) if you don't want to get request by him/her again]", Color.Pink);
                        PendingActions.Add(players[0].Name, executer.Name);
                    }
                    catch (NullReferenceException)
                    {
                        executer.SendErrorMessage("this player hasn't register hes/her gender...");
                        return;
                    }
                }
            } catch (NullReferenceException)
            {
                executer.SendErrorMessage("Please Register your Gender for you to find love" +
                    "\nyou can register your gender by executing ( /love register <your gender> )");
                return;
            }



            #endregion
        }

        private void Divorce(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }
            try
            {
                //get partner from the executer
                Partner partners = DB.GetPartnerByName(executer.Name);

                string getpartnername = "";

                //getname
                if (partners.Player1 == executer.Name) { getpartnername = partners.Player2; }
                else
                if (partners.Player2 == executer.Name) { getpartnername = partners.Player1; }

                DB.DeletePartner(partners.Player1);
                ActivePartners.Remove(partners.Player1);
                ActivePartners.Remove(partners.Player2);

                var getpartner = TSPlayer.FindByNameOrID(getpartnername);
                if (getpartner.Count > 0)
                {
                    getpartner[0].SendMessage($"{executer.Name} just broke up with you! :(", Color.Red);
                }
                executer.SendMessage($"you just broke up with {getpartner[0].Name}", Color.Red);

            }
            catch (NullReferenceException)
            {
                executer.SendErrorMessage("you don't have a partner!", Color.Pink);
                return;
            }
            #endregion
        }


        //pending actions
        private void AcceptAction(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }
            try
            {
                DB.GetPartnerByName(executer.Name);
                executer.SendErrorMessage("error: seems you already have a partner!");
                return;
            } catch (NullReferenceException) { }
            foreach (var getvalue in PendingActions)
            {
                if (getvalue.Key == executer.Name)
                {
                    DB.NewPartner(executer.Name, getvalue.Value);
                    ActivePartners.Add(executer.Name + "_" + getvalue.Value, "couples");
                    TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, $"{getvalue.Value} is now my love <3", (int)Color.Pink.packedValue, executer.X, executer.Y);
                    executer.SendMessage($"[i:58] [c/ffffff:{getvalue.Value}] is your partner congratulations!", Color.Pink);
                    var partnerget = TSPlayer.FindByNameOrID(getvalue.Value);
                    if (partnerget.Count == 0)
                    {
                        return;
                    }
                    TSPlayer.All.SendData(PacketTypes.CreateCombatTextExtended, $"{executer.Name} is now my love <3", (int)Color.Pink.packedValue, partnerget[0].X, partnerget[0].Y);
                    partnerget[0].SendMessage($"[i:58] [c/ffffff:{executer.Name}] Accepted you congratulations!", Color.Pink);
                    return;
                }
            }
            executer.SendErrorMessage("you do not have Pending request by someone!");
            #endregion
        }
        private void RejectAction(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }
            foreach (var getvalue in PendingActions)
            {
                if (getvalue.Key == executer.Name)
                {
                    if (args.Parameters.Count == 1)
                    {
                        if (args.Parameters[0] == "force")
                        {
                            PendingActions.Remove(getvalue.Key);
                            OnRejectLocked.Add(executer.Name, getvalue.Value);
                            executer.SendMessage($"[c/db0202:[X][c/db0202:]] you've rejected {getvalue.Value} in Force!", Color.Orange);
                            var partnerget = TSPlayer.FindByNameOrID(getvalue.Value);
                            if (partnerget.Count == 0)
                            {
                                return;
                            }
                            partnerget[0].SendMessage($"[c/db0202:[X][c/db0202:]] {executer.Name} Rejected you!!!", Color.Orange);
                        }
                    } else
                    {
                        PendingActions.Remove(getvalue.Key);
                        executer.SendMessage($"[c/db0202:[X][c/db0202:]] you've rejected {getvalue.Value}!", Color.Orange);
                        var partnerget = TSPlayer.FindByNameOrID(getvalue.Value);
                        if (partnerget.Count == 0)
                        {
                            return;
                        }
                        partnerget[0].SendMessage($"[c/db0202:[X][c/db0202:]] {executer.Name} Rejected you!!!", Color.Orange);
                        return;
                    }
                }
            }
            executer.SendErrorMessage("you do not have Pending request by someone!");
            #endregion
        }


        //execution
        private void TPCouple(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }
            try
            {
                //get partner from the executer
                Partner partners = DB.GetPartnerByName(executer.Name);

                string getpartnername = "";

                //getname
                if (partners.Player1 == executer.Name) { getpartnername = partners.Player2; }
                else
                if (partners.Player2 == executer.Name) { getpartnername = partners.Player1; }


                var getpartner = TSPlayer.FindByNameOrID(getpartnername);
                if (getpartner.Count == 0)
                {
                    args.Player.SendErrorMessage("Your Partner is not playing on this server...");
                    return;
                }
                TSPlayer partner = getpartner[0];

                if (!partner.IsLoggedIn)
                {
                    args.Player.SendErrorMessage("Your Partner is not logged in...");
                    return;
                }

                Projectile.NewProjectile(Projectile.GetNoneSource(), executer.TPlayer.position.X, executer.TPlayer.position.Y - 0f, 0f, -0f, 950, 0, 0);
                Projectile.NewProjectile(Projectile.GetNoneSource(), partner.TPlayer.position.X, partner.TPlayer.position.Y - 0f, 0f, -0f, 950, 0, 0);

                executer.Teleport(partner.TPlayer.position.X, partner.TPlayer.position.Y);
                executer.SendMessage("[i:58] Successfully teleported to your Partner", Color.Pink);
                partner.SendMessage("[i:58] Your Partner has teleported to you!", Color.Pink);


            } 
            catch (NullReferenceException)
            {
                executer.SendErrorMessage("you don't have a partner!", Color.Pink);
                return;
            }
            #endregion
        }

        private void WhisperCouple(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }
            if (args.Parameters.Count == 0)
            {
                executer.SendErrorMessage("Your message for your partner is empty!" +
                    "\nproper syntax: /whisperlove <message>");
                return;
            }
            try
            {
                //get partner from the executer
                Partner partners = DB.GetPartnerByName(executer.Name);

                string getpartnername = "";

                //getname
                if (partners.Player1 == executer.Name) { getpartnername = partners.Player2; }
                else
                if (partners.Player2 == executer.Name) { getpartnername = partners.Player1; }


                var getpartner = TSPlayer.FindByNameOrID(getpartnername);
                if (getpartner.Count == 0)
                {
                    args.Player.SendErrorMessage("Your Partner is not playing on this server...");
                    return;
                }
                TSPlayer partner = getpartner[0];

                if (!partner.IsLoggedIn)
                {
                    args.Player.SendErrorMessage("Your Partner is not logged in...");
                    return;
                }

                //action
                var message = string.Join(" ", args.Parameters.ToArray(), 0, args.Parameters.Count);
                executer.SendMessage($"[i:29] sent: {message}", Color.DeepPink);
                partner.SendMessage($"[i:29] <from {executer.Name}> {message}", Color.DeepPink);


            }
            catch (NullReferenceException)
            {
                executer.SendErrorMessage("you don't have a partner!", Color.Pink);
                return;
            } catch (Exception e)
            {
                Console.WriteLine(e);
            }
            #endregion
        }

        private void AIDCommand(CommandArgs args)
        {
            #region code
            TSPlayer executer = args.Player;
            if (!executer.RealPlayer)
            {
                executer.SendErrorMessage("you can only use that command in-game!");
                return;
            }
            try
            {
                //get partner from the executer
                Partner partners = DB.GetPartnerByName(executer.Name);

                string getpartnername = "";

                //getname
                if (partners.Player1 == executer.Name) { getpartnername = partners.Player2; }
                else
                if (partners.Player2 == executer.Name) { getpartnername = partners.Player1; }


                var getpartner = TSPlayer.FindByNameOrID(getpartnername);
                if (getpartner.Count == 0)
                {
                    args.Player.SendErrorMessage("Your Partner is not playing on this server...");
                    return;
                }
                TSPlayer partner = getpartner[0];

                if (!partner.IsLoggedIn)
                {
                    args.Player.SendErrorMessage("Your Partner is not logged in...");
                    return;
                }

                //action
                string[] partnernames = { executer.Name, getpartner[0].Name };
                foreach (var check in OnCoolDown)
                {
                    if (partnernames.Contains(check.Key.Split("_")[0]) || partnernames.Contains(check.Key.Split("_")[1]) && check.Key.Split("_")[2] == "heal")
                    {
                        if ((int)(DateTime.UtcNow - check.Value).TotalMinutes < 6)
                        {
                            TotalTime get = GetTotalTime(DateTime.UtcNow, check.Value.AddMinutes(6));
                            executer.SendErrorMessage($"you can use it again on {get.Minute * -1} Minutes and {get.Second * -1} Seconds...");
                            return;
                        } else
                        {
                            executer.SendMessage("[i:58] You've used Heal with your partner... ( you can use it again after 6 mins )", Color.Pink);
                            getpartner[0].SendMessage("[i:58] Your Partner Used Heal! ( so your heal is on cooldown )", Color.Pink);
                            executer.Heal(200);
                            getpartner[0].Heal(200);
                            OnCoolDown[check.Key] = DateTime.UtcNow;
                            return;
                        }
                    }
                }

                executer.SendMessage("[i:58] You've used Heal with your partner... ( you can use it again after 6 mins )", Color.Pink);
                getpartner[0].SendMessage("[i:58] Your Partner Used Heal! ( so your heal is on cooldown )", Color.Pink);
                executer.Heal(200);
                getpartner[0].Heal(200);
                OnCoolDown.Add($"{executer.Name}_{getpartner[0].Name}_heal", DateTime.UtcNow);
            }
            catch (NullReferenceException)
            {
                executer.SendErrorMessage("you don't have a partner!", Color.Pink);
                return;
            }
            #endregion
        }

        #endregion

        #region Functions
        public void CoupleBuff(string Player1, string Player2, string Type)
        {
            var foundP1 = TSPlayer.FindByNameOrID(Player1);
            if (foundP1.Count == 0)
            {
                return;
            }
            TSPlayer P1 = foundP1[0];

            var foundP2 = TSPlayer.FindByNameOrID(Player2);
            if (foundP2.Count == 0)
            {
                return;
            }
            TSPlayer P2 = foundP2[0];

            if (!P1.IsLoggedIn || !P2.IsLoggedIn)
            {
                return;
            }

            float num1 = ((int)P1.TPlayer.position.X/16) - ((int)P2.TPlayer.position.X/16);
            float num2 = ((int)P1.TPlayer.position.Y/16) - ((int)P2.TPlayer.position.Y/16);
            if ((float)Math.Sqrt(num1 * num1 + num2 * num2) <= 30 )// if those couples are near 30 blocks to each other
            {
                P1.SetBuff(119, 180);//love struck [ does not nothing < its a design > ]
                P2.SetBuff(119, 180);
                P1.SetBuff(2, 180);//regeneration
                P2.SetBuff(2, 180);
                /*
                Actionawait.Remove(P1.Name);
                Actionawait.Remove(P2.Name);
                */
            } else if (Type == "married" && (float)Math.Sqrt(num1 * num1 + num2 * num2) <= 40)// if those married people are near 50 blocks to each other
            {
                P1.SetBuff(119, 180);//love struck [ does not nothing < its a design > ]
                P2.SetBuff(119, 180);
                P1.SetBuff(2, 180);//regeneration
                P2.SetBuff(2, 180);
            }
        }

        public void KissAction(string Player1, string Player2)
        {
            var foundP1 = TSPlayer.FindByNameOrID(Player1);
            if (foundP1.Count == 0)
            {
                return;
            }
            TSPlayer P1 = foundP1[0];

            var foundP2 = TSPlayer.FindByNameOrID(Player2);
            if (foundP2.Count == 0)
            {
                return;
            }
            TSPlayer P2 = foundP2[0];

            if (!P1.IsLoggedIn || !P2.IsLoggedIn)
            {
                return;
            }

            float num1 = ((int)P1.TPlayer.position.X / 16) - ((int)P2.TPlayer.position.X / 16);
            float num2 = ((int)P1.TPlayer.position.Y / 16) - ((int)P2.TPlayer.position.Y / 16);
            if ((float)Math.Sqrt(num1 * num1 + num2 * num2) <= 3)// if those players are near 3 blocks to each other
            {
                Actionawait.Remove($"{P1.Name}_kiss");
                P1.SetBuff(119, 300);//love struck [ heart particles on player ]
                P2.SetBuff(119, 300);
                Thread.Sleep(1000);
                P1.SetBuff(22, 180);//darkness [ to close their eyes ]
                P2.SetBuff(22, 180);
                P1.SetBuff(160, 180);//dazed [ reduce their movement speed significantly ]
                P2.SetBuff(160, 180);
                TSPlayer.All.SendData(PacketTypes.EmoteBubble, null, 88+emoteid, 1, P1.TPlayer.whoAmI, 180, 88);
                emoteid++;
                TSPlayer.All.SendData(PacketTypes.EmoteBubble, null, 88+emoteid, 1, P2.TPlayer.whoAmI, 180, 88);
                emoteid++;
                Thread.Sleep(3000);
                TSPlayer.All.SendData(PacketTypes.EmoteBubble, null, 0+emoteid, 1, P1.TPlayer.whoAmI, 300, 0);
                emoteid++;
                TSPlayer.All.SendData(PacketTypes.EmoteBubble, null, 0+emoteid, 1, P2.TPlayer.whoAmI, 300, 0);
                emoteid++;
                if (emoteid >= 50)
                {
                    emoteid = 0;
                }
            }
        }

        public TotalTime GetTotalTime(DateTime prev, DateTime now)
        {
            int Days = (int)(prev - now).TotalDays;
            int Hours = (int)(prev - now).TotalHours - (24 * Days);
            int Minutes = (int)(prev - now).TotalMinutes - (((24 * Days) + Hours) * 60);
            int Seconds = ((int)(prev - now).TotalSeconds - (((((24 * Days) + Hours) * 60) * Minutes) * 60)) - (60*Minutes);


            return new TotalTime(Days, Hours, Minutes, Seconds);
        }

        #endregion


        /*
        public static float Distance(Vector2 value1, Vector2 value2)
        {
            float num = value1.X - value2.X;
            float num2 = value1.Y - value2.Y;
            return (float)Math.Sqrt(num * num + num2 * num2);
        }
        */
    }
    public class TotalTime
    {
        public int Day;
        public int Hour;
        public int Minute;
        public int Second;

        public TotalTime(int Day, int Hour, int Minute, int Second)
        {
            this.Day = Day;
            this.Hour = Hour;
            this.Minute = Minute;
            this.Second = Second;
        }
    }
}
