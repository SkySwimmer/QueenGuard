using System;
using Discord;
using Discord.WebSocket;
using CMDR;
using System.Threading.Tasks;
using System.Threading;
using static CMDR.Server;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Ferever
{
    public class Module : BotModule
    {
        internal Dictionary<Server, ConfigDictionary<string, object>> serverMemory = new Dictionary<Server, ConfigDictionary<string, object>>();
        
        public override string id => "QueenGuard";

        public override string moduledesctiption => "The Queen Guard Bot Module";

        public override void Init(Bot bot)
        {
        }

        public override void PostInit(Bot bot)
        {
            bot.client.JoinedGuild += (guild) => {
                Server server = bot.GetServerFromSocketGuild(guild);
                while (server == null)
                {
                    server = bot.GetServerFromSocketGuild(guild);
                    Thread.Sleep(100);
                }

                serverMemory[server] = new ConfigDictionary<string, object>();
                LoadServer(bot, guild, server);
                return Task.CompletedTask;
            };

            bot.client.LeftGuild += (guild) => {
                foreach (Server srv in new List<Server>(serverMemory.Keys)) {
                    if (srv.id == guild.Id) {
                        serverMemory.Remove(srv);
                        break;
                    }
                }

                return Task.CompletedTask;
            };

            bot.client.UserLeft += (guild, user) => {
                Server server = GetBot().GetServerFromSocketGuild(guild);
                if (serverMemory[server].ContainsKey("submitted-" + user.Id))
                    serverMemory[server].Remove("submitted-" + user.Id);
                return Task.CompletedTask;
            };

            bot.client.ModalSubmitted += (interaction) => {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser) {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(this);

                    if (interaction.Data.CustomId == "verification") {
                        interaction.DeferAsync().GetAwaiter().GetResult();

                        ulong reviewerRole = (ulong)conf.GetOrDefault("reviewerRole", (ulong)0);
                        ulong reviewChannel = (ulong)conf.GetOrDefault("reviewChannel", (ulong)0);
                        SocketTextChannel reviewCh = guild.GetTextChannel(reviewChannel);

                        string msg = "";
                        foreach (var comp in interaction.Data.Components) {
                            msg += comp.CustomId + "\n";
                            msg += "------------------------------------------------------------------------------------------------------------------------------------\n";
                            msg += comp.Value + "\n";
                            msg += "------------------------------------------------------------------------------------------------------------------------------------\n";
                            msg += "\n";
                            msg += "\n";
                        }
                        msg += "\n";
                        MemoryStream strm = new MemoryStream(Encoding.UTF8.GetBytes(msg));
                        reviewCh.SendFileAsync(strm, "form.txt", "<@&" + reviewerRole + ">, received a verification submission from " + user.Mention + ".",  false, null, null, false, null, null, new ComponentBuilder()
                            .WithButton("Accept", "acceptUser/" + user.Id, ButtonStyle.Success)
                            .WithButton("Reject", "rejectUser/" + user.Id, ButtonStyle.Danger)
                        .Build()).GetAwaiter().GetResult();
                        strm.Close();
                        
                        serverMemory[server]["submitted-" + user.Id] = true;
                    }
                }

                return Task.CompletedTask;
            };

            bot.client.ButtonExecuted += (interaction) => {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser) {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(this);

                    if (interaction.Data.CustomId == "beginVerification" && conf.GetOrDefault("verificationQuestion1", null) != null) {
                        try {
                            ulong sparkRole = (ulong)conf.GetOrDefault("sparkRole", (ulong)0);
                            if (user.Roles.FirstOrDefault(t => t.Id == sparkRole, null) != null) {
                                interaction.RespondAsync("You have already verified your account, you can visit the other channels of this server.", null, false, true).GetAwaiter().GetResult();
                                return Task.CompletedTask;
                            }

                            if (serverMemory[server].ContainsKey("submitted-" + user.Id)) {
                                interaction.RespondAsync("Your verification form has already been submitted, please wait for it to be reviewed.", null, false, true).GetAwaiter().GetResult();
                                return Task.CompletedTask;
                            }

                            ModalBuilder builder = new ModalBuilder()
                                .WithCustomId("verification")
                                .WithTitle("Verification challenge");
                            
                            string q1 = conf.Get("verificationQuestion1").ToString();
                            if (q1 == "<skipped>")
                                q1 = "";                            
                            string q2 = conf.Get("verificationQuestion2").ToString();
                            if (q2 == "<skipped>")
                                q2 = "";
                            string q3 = conf.Get("verificationQuestion3").ToString();
                            if (q3 == "<skipped>")
                                q3 = "";                            
                            string q4 = conf.Get("verificationQuestion4").ToString();
                            if (q4 == "<skipped>")
                                q4 = "";
                            string q5 = conf.Get("verificationQuestion5").ToString();
                            if (q5 == "<skipped>")
                                q5 = "";
                            
                            if (q1 != "") {
                                builder.AddTextInput(q1, q1, TextInputStyle.Paragraph);
                            }
                            if (q2 != "") {
                                builder.AddTextInput(q2, q2, TextInputStyle.Paragraph);
                            }
                            if (q3 != "") {
                                builder.AddTextInput(q3, q3, TextInputStyle.Paragraph);
                            }
                            if (q4 != "") {
                                builder.AddTextInput(q4, q4, TextInputStyle.Paragraph);
                            }
                            if (q5 != "") {
                                builder.AddTextInput(q5, q5, TextInputStyle.Paragraph);
                            }
                            
                            interaction.RespondWithModalAsync(builder.Build()).GetAwaiter().GetResult();
                        } catch {}
                    } else if (interaction.Data.CustomId.StartsWith("acceptUser/")) {
                        try {
                            ulong sparkRole = (ulong)conf.GetOrDefault("sparkRole", (ulong)0);
                            if (sparkRole != 0) {
                                if (serverMemory[server].ContainsKey("submitted-" + user.Id)) {
                                    serverMemory[server].Remove("submitted-" + user.Id);
                                }
                                
                                SocketRole sparks = (SocketRole)guild.GetRole(sparkRole);
                                ulong uid = ulong.Parse(interaction.Data.CustomId.Substring("acceptUser/".Length));
                                guild.GetUser(uid).AddRoleAsync(sparks.Id);
                                interaction.RespondAsync("Accepted user membership, granted the `" + sparks.Name + "` role to <@!" + uid + ">.").GetAwaiter().GetResult();
                                guild.DefaultChannel.SendMessageAsync("Welcome <@!" + uid + ">!").GetAwaiter().GetResult();
                            }
                        } catch {}
                    } else if (interaction.Data.CustomId.StartsWith("rejectUser/")) {
                        try {
                            ulong uid = ulong.Parse(interaction.Data.CustomId.Substring("rejectUser/".Length));
                            guild.GetUser(uid).KickAsync().GetAwaiter().GetResult();
                            interaction.RespondAsync("Rejected user membership, kicked <@!" + uid + ">.").GetAwaiter().GetResult();
                        } catch {}
                    }
                }

                return Task.CompletedTask;
            };

            foreach (Server server in bot.servers) {
                SocketGuild guild = bot.client.GetGuild(server.id);
                serverMemory[server] = new ConfigDictionary<string, object>();
                LoadServer(bot, guild, server);
            }
        }

        public override void PreInit(Bot bot)
        {
        }

        public void LoadServer(Bot bot, SocketGuild guild, Server server) {            
            ModuleConfig conf = server.GetModuleConfig(this);
            ulong sparkRole = (ulong)conf.GetOrDefault("sparkRole", (ulong)0);
            if (sparkRole != 0) {
                SocketRole sparks = (SocketRole)guild.GetRole(sparkRole);
                ulong reviewerRole = (ulong)conf.GetOrDefault("reviewerRole", (ulong)0);
                SocketRole reviewers = (SocketRole)guild.GetRole(reviewerRole);
                if (sparks != null && reviewers != null) {
                    ulong reviewChannel = (ulong)conf.GetOrDefault("reviewChannel", (ulong)0);
                    ulong verificationChannel = (ulong)conf.GetOrDefault("verificationChannel", (ulong)0);

                    SocketTextChannel verifyCh = guild.GetTextChannel(verificationChannel);
                    SocketTextChannel reviewCh = guild.GetTextChannel(reviewChannel);
                    if (verifyCh != null && reviewCh != null) {
                        ulong verificationMessageID = (ulong)conf.GetOrDefault("verificationMessageID", (ulong)0);
                        IMessage msg = null;
                        
                        if (verificationMessageID != 0) 
                            msg = verifyCh.GetMessageAsync(verificationMessageID).GetAwaiter().GetResult();
                        
                        if (msg == null) {
                            msg = verifyCh.SendMessageAsync("", false, new EmbedBuilder()
                                .WithTitle("Welcome to " + guild.Name + "!")
                                .WithColor(Color.Blue)
                                .WithDescription(conf.Get("messageContent").ToString())
                            .Build(), null, null, null, new ComponentBuilder()
                                .WithButton("Verify", "beginVerification", ButtonStyle.Success)
                            .Build()).GetAwaiter().GetResult();
                            conf.Set("verificationMessageID", msg.Id);
                            server.SaveAll();
                        }
                    }
                }
            }
        }

        public override void RegisterCommands(Bot bot)
        {
            RegisterCommand(new ConfigureCommand(this));
        }
    }
}
