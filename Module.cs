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
            bot.client.JoinedGuild += (guild) =>
            {
                Server server = bot.GetServerFromSocketGuild(guild);
                while (server == null)
                {
                    server = bot.GetServerFromSocketGuild(guild);
                    Thread.Sleep(100);
                }

                // Setup memory
                serverMemory[server] = new ConfigDictionary<string, object>();

                // Load server
                LoadServer(bot, guild, server);
                return Task.CompletedTask;
            };

            bot.client.LeftGuild += (guild) =>
            {
                foreach (Server srv in new List<Server>(serverMemory.Keys))
                {
                    if (srv.id == guild.Id)
                    {
                        // Remove memory
                        serverMemory.Remove(srv);
                        break;
                    }
                }

                return Task.CompletedTask;
            };

            bot.client.UserLeft += (guild, user) =>
            {
                Server server = GetBot().GetServerFromSocketGuild(guild);
                if (serverMemory[server].ContainsKey("verification-" + user.Id))
                {
                    // Get review message
                    string msg = serverMemory[server].GetValue("verification-" + user.Id).ToString();
                    serverMemory[server].Remove("verification-" + user.Id);

                    // Parse info
                    string chID = msg.Split("/")[0];
                    string msgID = msg.Split("/")[1];

                    // Get message object
                    IUserMessage message = (IUserMessage)guild.GetTextChannel(ulong.Parse(chID)).GetMessageAsync(ulong.Parse(msgID)).GetAwaiter().GetResult();

                    // Update embed
                    message.ModifyAsync(t =>
                    {
                        t.Embed = new Optional<Embed>(message.Embeds.ToArray()[0].ToEmbedBuilder()
                            .WithColor(Color.DarkRed)
                            .WithFooter("Status: member left")
                            .Build());
                        t.Components = new Optional<MessageComponent>(new ComponentBuilder().Build());
                    }).GetAwaiter().GetResult();
                }
                return Task.CompletedTask;
            };

            bot.client.ModalSubmitted += (interaction) =>
            {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser)
                {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(this);

                    if (interaction.Data.CustomId == "verification")
                    {
                        interaction.DeferAsync().GetAwaiter().GetResult();

                        ulong reviewerRole = (ulong)conf.GetOrDefault("reviewerRole", (ulong)0);
                        ulong reviewChannel = (ulong)conf.GetOrDefault("reviewChannel", (ulong)0);
                        SocketTextChannel reviewCh = guild.GetTextChannel(reviewChannel);

                        // Build embed
                        EmbedBuilder builder = new EmbedBuilder();
                        builder.WithFooter("Status: pending review");
                        builder.WithTitle(user.Username + "#" + user.Discriminator);
                        builder.WithColor(Color.Orange);
                        builder.WithDescription(user.Mention + " submitted their verification form.");

                        // Add fields
                        foreach (var comp in interaction.Data.Components)
                        {
                            // Add form entry
                            builder.AddField(comp.CustomId, comp.Value);
                        }

                        // Send review message
                        IUserMessage msg = reviewCh.SendMessageAsync("<@&" + reviewerRole + ">, received a verification submission from " + user.Mention + ".", components: new ComponentBuilder()
                            .WithButton("Accept", "acceptUser/" + user.Id, ButtonStyle.Success)
                            .WithButton("Reject", "rejectUser/" + user.Id, ButtonStyle.Danger)
                        .Build(), embed: builder.Build()).GetAwaiter().GetResult();

                        // Create thread
                        guild.GetTextChannel(msg.Channel.Id).CreateThreadAsync(user.Username + "#" + user.Discriminator + " review discussion", message: msg).GetAwaiter().GetResult();

                        // Set memory entry
                        serverMemory[server]["verification-" + user.Id] = msg.Channel.Id + "/" + msg.Id;
                    }
                    else if (interaction.Data.CustomId == "verificationFormEditor/setup")
                    {
                        // Check permission
                        if (!bot.CheckPermissions("commands.administration.configure.queenguard", user, guild))
                        {
                            // Error
                            return interaction.RespondAsync(embed: new EmbedBuilder()
                                    .WithColor(Color.Red)
                                    .WithDescription("You do not have the `commands.administration.configure.queenguard` permission.")
                                .Build());
                        }

                        // Respond
                        interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                    .WithDescription("Saving configuration...")
                                    .WithColor(Color.DarkGreen)
                                .Build() }).GetAwaiter().GetResult();

                        // Save questions
                        foreach (var comp in interaction.Data.Components)
                            conf.Set("verification" + comp.CustomId, comp.Value);

                        // Pull data from memory
                        Dictionary<string, object> data = (Dictionary<string, object>)serverMemory[server]["config-" + user.Id];

                        // Save channels
                        conf.Set("reviewChannel", ((SocketChannel)data["ReviewChannel"]).Id);
                        conf.Set("verificationChannel", ((SocketChannel)data["PanelChannel"]).Id);

                        // Save messages
                        conf.Set("messageContent", data["PanelMessage"].ToString());
                        conf.Set("buttonMessage", data["PanelButton"].ToString());

                        // Save roles
                        conf.Set("reviewerRole", ((SocketRole)data["ReviewerRole"]).Id);
                        conf.Set("sparkRole", ((SocketRole)data["SparkRole"]).Id);
                        if (data["VerifiedSparkRole"] == null)
                            conf.Set("verifiedSparkRole", (ulong)0);
                        else
                            conf.Set("verifiedSparkRole", ((SocketRole)data["VerifiedSparkRole"]).Id);

                        // Save and reload
                        conf.Set("setup_completed", true);
                        server.SaveAll();
                        LoadServer(GetBot(), guild, server);

                        // Clean
                        serverMemory[server].Remove("config-" + user.Id);

                        // Send success
                        channel.SendMessageAsync("", false, new EmbedBuilder()
                            .WithDescription("Saved and reloaded the server configuration!")
                            .WithColor(Color.Green)
                        .Build()).GetAwaiter().GetResult();
                    }
                    else if (interaction.Data.CustomId == "verificationFormEditor/edit")
                    {
                        // Check permission
                        if (!bot.CheckPermissions("commands.administration.configure.queenguard", user, guild))
                        {
                            // Error
                            return interaction.RespondAsync(embed: new EmbedBuilder()
                                    .WithColor(Color.Red)
                                    .WithDescription("You do not have the `commands.administration.configure.queenguard` permission.")
                                .Build());
                        }

                        // Respond
                        interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                    .WithDescription("Saving configuration...")
                                    .WithColor(Color.DarkGreen)
                                .Build() }).GetAwaiter().GetResult();

                        // Save questions
                        foreach (var comp in interaction.Data.Components)
                            conf.Set("verification" + comp.CustomId, comp.Value);

                        // Save and reload
                        server.SaveAll();
                        LoadServer(GetBot(), guild, server);

                        // Clean
                        serverMemory[server].Remove("config-" + user.Id);

                        // Send success
                        channel.SendMessageAsync("", false, new EmbedBuilder()
                            .WithDescription("Saved and reloaded the server configuration!")
                            .WithColor(Color.Green)
                        .Build()).GetAwaiter().GetResult();
                    }
                }

                return Task.CompletedTask;
            };

            bot.client.SlashCommandExecuted += (interaction) =>
            {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser)
                {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(this);
                    if (interaction.CommandName != "queenguard")
                        return Task.CompletedTask;

                    // Check permission
                    if (!bot.CheckPermissions("commands.administration.configure.queenguard", user, guild))
                    {
                        // Error
                        return interaction.RespondAsync(embed: new EmbedBuilder()
                                .WithColor(Color.Red)
                                .WithDescription("You do not have the `commands.administration.configure.queenguard` permission.")
                            .Build());
                    }

                    // Handle commands
                    switch (interaction.Data.Options.ToArray()[0].Name)
                    {
                        case "setup":
                            {
                                // Setup
                                SocketSlashCommandDataOption[] options = interaction.Data.Options.ToArray()[0].Options.ToArray();
                                SocketRole sparkRole = options.First(t => t.Name == "verification-role").Value as SocketRole;
                                SocketRole reviewerRole = options.First(t => t.Name == "reviewer-role").Value as SocketRole;
                                SocketChannel reviewChannel = options.First(t => t.Name == "review-channel").Value as SocketChannel;
                                SocketChannel panelChannel = options.First(t => t.Name == "panel-channel").Value as SocketChannel;
                                if (!(reviewChannel is SocketTextChannel))
                                {
                                    // Error
                                    return interaction.RespondAsync(embed: new EmbedBuilder()
                                            .WithColor(Color.Red)
                                            .WithDescription("Cannot use non-text channels as review channel.")
                                        .Build());
                                }
                                if (!(panelChannel is SocketTextChannel))
                                {
                                    // Error
                                    return interaction.RespondAsync(embed: new EmbedBuilder()
                                            .WithColor(Color.Red)
                                            .WithDescription("Cannot use non-text channels for the verification panel.")
                                        .Build());
                                }

                                // Optional arguments
                                string message = "**Welcome to " + guild.Name + "! **\n"
                                  + "\n"
                                  + "Please verify that you are a feralian to gain access to this server.\n"
                                  + "To begin the verification process, press 'Verify' below.";
                                string button = "Verify";
                                if (options.Any(t => t.Name == "panel-message"))
                                    message = options.First(t => t.Name == "panel-message").Value as string;
                                if (options.Any(t => t.Name == "panel-button"))
                                    button = options.First(t => t.Name == "panel-button").Value as string;
                                SocketRole verifSpark = null;
                                if (options.Any(t => t.Name == "secondary-verification-role"))
                                    verifSpark = options.First(t => t.Name == "secondary-verification-role").Value as SocketRole;

                                // Save in memory
                                serverMemory[server]["config-" + user.Id] = new Dictionary<string, object>()
                                {
                                    ["SparkRole"] = sparkRole,
                                    ["VerifiedSparkRole"] = verifSpark,
                                    ["ReviewerRole"] = reviewerRole,
                                    ["ReviewChannel"] = reviewChannel,
                                    ["PanelChannel"] = panelChannel,
                                    ["PanelMessage"] = message,
                                    ["PanelButton"] = button
                                };

                                // Show dialog
                                string oldQ1 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion1", conf.GetOrDefault("verificationQuestion1", "<skipped>").ToString()).ToString();
                                if (oldQ1 == "")
                                    oldQ1 = "<skipped>";
                                string oldQ2 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion2", conf.GetOrDefault("verificationQuestion2", "<skipped>").ToString()).ToString();
                                if (oldQ2 == "")
                                    oldQ2 = "<skipped>";
                                string oldQ3 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion3", conf.GetOrDefault("verificationQuestion3", "<skipped>").ToString()).ToString();
                                if (oldQ3 == "")
                                    oldQ3 = "<skipped>";
                                string oldQ4 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion4", conf.GetOrDefault("verificationQuestion4", "<skipped>").ToString()).ToString();
                                if (oldQ4 == "")
                                    oldQ4 = "<skipped>";
                                string oldQ5 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion5", conf.GetOrDefault("verificationQuestion5", "<skipped>").ToString()).ToString();
                                if (oldQ5 == "")
                                    oldQ5 = "<skipped>";
                                return interaction.RespondWithModalAsync(new ModalBuilder()
                                    .WithTitle("Form Editor")
                                    .WithCustomId("verificationFormEditor/setup")
                                    .AddTextInput(new TextInputBuilder()
                                        .WithLabel("Question 1")
                                        .WithCustomId("Question1")
                                        .WithMaxLength(45)
                                        .WithValue(oldQ1))
                                    .AddTextInput(new TextInputBuilder()
                                        .WithLabel("Question 2")
                                        .WithCustomId("Question2")
                                        .WithMaxLength(45)
                                        .WithRequired(false)
                                        .WithValue(oldQ2))
                                    .AddTextInput(new TextInputBuilder()
                                        .WithLabel("Question 3")
                                        .WithCustomId("Question3")
                                        .WithMaxLength(45)
                                        .WithRequired(false)
                                        .WithValue(oldQ3))
                                    .AddTextInput(new TextInputBuilder()
                                        .WithLabel("Question 4")
                                        .WithCustomId("Question4")
                                        .WithMaxLength(45)
                                        .WithRequired(false)
                                        .WithValue(oldQ4))
                                    .AddTextInput(new TextInputBuilder()
                                        .WithLabel("Question 5")
                                        .WithCustomId("Question5")
                                        .WithMaxLength(45)
                                        .WithRequired(false)
                                        .WithValue(oldQ5))
                                .Build());
                            }
                        case "edit":
                            {
                                // Edit
                                string subCommand = interaction.Data.Options.ToArray()[0].Options.ToArray()[0].Name;

                                // Check setup
                                if (!(bool)conf.GetOrDefault("setup_completed", false))
                                {
                                    // Error
                                    return interaction.RespondAsync(embed: new EmbedBuilder()
                                            .WithColor(Color.Red)
                                            .WithDescription("Queen Guard has not yet been configured, please run `/queenguard setup` first.")
                                        .Build());
                                }

                                // Handle
                                switch (subCommand)
                                {
                                    case "form":
                                        {
                                            // Show dialog
                                            string oldQ1 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion1", conf.GetOrDefault("verificationQuestion1", "<skipped>").ToString()).ToString();
                                            if (oldQ1 == "")
                                                oldQ1 = "<skipped>";
                                            string oldQ2 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion2", conf.GetOrDefault("verificationQuestion2", "<skipped>").ToString()).ToString();
                                            if (oldQ2 == "")
                                                oldQ2 = "<skipped>";
                                            string oldQ3 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion3", conf.GetOrDefault("verificationQuestion3", "<skipped>").ToString()).ToString();
                                            if (oldQ3 == "")
                                                oldQ3 = "<skipped>";
                                            string oldQ4 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion4", conf.GetOrDefault("verificationQuestion4", "<skipped>").ToString()).ToString();
                                            if (oldQ4 == "")
                                                oldQ4 = "<skipped>";
                                            string oldQ5 = serverMemory[server].GetValueOrDefault("SetupVerificationQuestion5", conf.GetOrDefault("verificationQuestion5", "<skipped>").ToString()).ToString();
                                            if (oldQ5 == "")
                                                oldQ5 = "<skipped>";
                                            return interaction.RespondWithModalAsync(new ModalBuilder()
                                                .WithTitle("Form Editor")
                                                .WithCustomId("verificationFormEditor/edit")
                                                .AddTextInput(new TextInputBuilder()
                                                    .WithLabel("Question 1")
                                                    .WithCustomId("Question1")
                                                    .WithMaxLength(45)
                                                    .WithValue(oldQ1))
                                                .AddTextInput(new TextInputBuilder()
                                                    .WithLabel("Question 2")
                                                    .WithCustomId("Question2")
                                                    .WithMaxLength(45)
                                                    .WithRequired(false)
                                                    .WithValue(oldQ2))
                                                .AddTextInput(new TextInputBuilder()
                                                    .WithLabel("Question 3")
                                                    .WithCustomId("Question3")
                                                    .WithMaxLength(45)
                                                    .WithRequired(false)
                                                    .WithValue(oldQ3))
                                                .AddTextInput(new TextInputBuilder()
                                                    .WithLabel("Question 4")
                                                    .WithCustomId("Question4")
                                                    .WithMaxLength(45)
                                                    .WithRequired(false)
                                                    .WithValue(oldQ4))
                                                .AddTextInput(new TextInputBuilder()
                                                    .WithLabel("Question 5")
                                                    .WithCustomId("Question5")
                                                    .WithMaxLength(45)
                                                    .WithRequired(false)
                                                    .WithValue(oldQ5))
                                            .Build());
                                        }
                                    case "panel":
                                        {
                                            SocketSlashCommandDataOption[] options = interaction.Data.Options.ToArray()[0].Options.ToArray()[0].Options.ToArray();
                                            SocketChannel panelChannel = options.First(t => t.Name == "panel-channel").Value as SocketChannel;
                                            if (!(panelChannel is SocketTextChannel))
                                            {
                                                // Error
                                                return interaction.RespondAsync(embed: new EmbedBuilder()
                                                        .WithColor(Color.Red)
                                                        .WithDescription("Cannot use non-text channels for the verification panel.")
                                                    .Build());
                                            }

                                            // Optional arguments
                                            string message = conf.Get("messageContent").ToString();
                                            string button = conf.Get("buttonMessage").ToString();
                                            if (options.Any(t => t.Name == "panel-message"))
                                                message = options.First(t => t.Name == "panel-message").Value as string;
                                            if (options.Any(t => t.Name == "panel-button"))
                                                button = options.First(t => t.Name == "panel-button").Value as string;

                                            // Respond
                                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                                .WithDescription("Saving configuration...")
                                                .WithColor(Color.DarkGreen)
                                            .Build() }).GetAwaiter().GetResult();

                                            // Save channels
                                            conf.Set("verificationChannel", panelChannel.Id);

                                            // Save messages
                                            conf.Set("messageContent", message);
                                            conf.Set("buttonMessage", button);

                                            // Save and reload
                                            server.SaveAll();
                                            LoadServer(GetBot(), guild, server);

                                            // Clean
                                            serverMemory[server].Remove("config-" + user.Id);

                                            // Send success
                                            channel.SendMessageAsync("", false, new EmbedBuilder()
                                                .WithDescription("Saved and reloaded the server configuration!")
                                                .WithColor(Color.Green)
                                            .Build()).GetAwaiter().GetResult();
                                            break;
                                        }
                                    case "review":
                                        {
                                            SocketSlashCommandDataOption[] options = interaction.Data.Options.ToArray()[0].Options.ToArray()[0].Options.ToArray();
                                            SocketRole reviewerRole = options.First(t => t.Name == "reviewer-role").Value as SocketRole;
                                            SocketChannel reviewChannel = options.First(t => t.Name == "review-channel").Value as SocketChannel;
                                            if (!(reviewChannel is SocketTextChannel))
                                            {
                                                // Error
                                                return interaction.RespondAsync(embed: new EmbedBuilder()
                                                        .WithColor(Color.Red)
                                                        .WithDescription("Cannot use non-text channels as review channel.")
                                                    .Build());
                                            }

                                            // Respond
                                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                                .WithDescription("Saving configuration...")
                                                .WithColor(Color.DarkGreen)
                                            .Build() }).GetAwaiter().GetResult();

                                            // Save channels
                                            conf.Set("reviewChannel", reviewChannel.Id);
                                            
                                            // Save roles
                                            conf.Set("reviewerRole", reviewerRole.Id);

                                            // Save and reload
                                            server.SaveAll();
                                            LoadServer(GetBot(), guild, server);

                                            // Clean
                                            serverMemory[server].Remove("config-" + user.Id);

                                            // Send success
                                            channel.SendMessageAsync("", false, new EmbedBuilder()
                                                .WithDescription("Saved and reloaded the server configuration!")
                                                .WithColor(Color.Green)
                                            .Build()).GetAwaiter().GetResult();
                                            break;
                                        }
                                    case "verification-role":
                                        {
                                            SocketSlashCommandDataOption[] options = interaction.Data.Options.ToArray()[0].Options.ToArray()[0].Options.ToArray();
                                            SocketRole sparkRole = options.First(t => t.Name == "verification-role").Value as SocketRole;
                                            SocketRole verifSpark = null;
                                            if (options.Any(t => t.Name == "secondary-verification-role"))
                                                verifSpark = options.First(t => t.Name == "secondary-verification-role").Value as SocketRole;

                                            // Respond
                                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                                .WithDescription("Saving configuration...")
                                                .WithColor(Color.DarkGreen)
                                            .Build() }).GetAwaiter().GetResult();

                                            // Save roles
                                            conf.Set("sparkRole", sparkRole.Id);
                                            if (verifSpark == null)
                                                conf.Set("verifiedSparkRole", (ulong)0);
                                            else
                                                conf.Set("verifiedSparkRole", verifSpark.Id);

                                            // Save and reload
                                            server.SaveAll();
                                            LoadServer(GetBot(), guild, server);

                                            // Clean
                                            serverMemory[server].Remove("config-" + user.Id);

                                            // Send success
                                            channel.SendMessageAsync("", false, new EmbedBuilder()
                                                .WithDescription("Saved and reloaded the server configuration!")
                                                .WithColor(Color.Green)
                                            .Build()).GetAwaiter().GetResult();
                                            break;
                                        }
                                }
                                break;
                            }
                    }
                }

                return Task.CompletedTask;
            };

            bot.client.ButtonExecuted += (interaction) =>
            {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser)
                {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(this);

                    // Handle button press
                    if (interaction.Data.CustomId == "beginVerification" && conf.GetOrDefault("verificationQuestion1", null) != null)
                    {
                        try
                        {
                            // Check roles
                            ulong verifiedSparkRole = (ulong)conf.GetOrDefault("verifiedSparkRole", (ulong)0);
                            ulong sparkRole = (ulong)conf.GetOrDefault("sparkRole", (ulong)0);
                            if (verifiedSparkRole != 0)
                                sparkRole = verifiedSparkRole;
                            if (user.Roles.FirstOrDefault(t => t.Id == sparkRole, null) != null)
                            {
                                interaction.RespondAsync("You have already verified your account, you can visit the other channels of this server.", null, false, true).GetAwaiter().GetResult();
                                return Task.CompletedTask;
                            }

                            // Check pending verification
                            if (serverMemory[server].ContainsKey("verification-" + user.Id))
                            {
                                interaction.RespondAsync("Your verification form has already been submitted, please wait for it to be reviewed.", null, false, true).GetAwaiter().GetResult();
                                return Task.CompletedTask;
                            }

                            // Show modal
                            ModalBuilder builder = new ModalBuilder()
                                .WithCustomId("verification")
                                .WithTitle("Verification challenge");

                            // Load questions
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

                            // Add inputs
                            if (q1 != "")
                                builder.AddTextInput(q1, q1, TextInputStyle.Short, minLength: 1, maxLength: 1024);
                            if (q2 != "")
                                builder.AddTextInput(q2, q2, TextInputStyle.Short, minLength: 1, maxLength: 1024);
                            if (q3 != "")
                                builder.AddTextInput(q3, q3, TextInputStyle.Short, minLength: 1, maxLength: 1024);
                            if (q4 != "")
                                builder.AddTextInput(q4, q4, TextInputStyle.Short, minLength: 1, maxLength: 1024);
                            if (q5 != "")
                                builder.AddTextInput(q5, q5, TextInputStyle.Short, minLength: 1, maxLength: 1024);

                            // Show modal
                            interaction.RespondWithModalAsync(builder.Build()).GetAwaiter().GetResult();
                        }
                        catch { }
                    }
                    else if (interaction.Data.CustomId.StartsWith("acceptUser/"))
                    {
                        try
                        {
                            // Confirm
                            ulong uid = ulong.Parse(interaction.Data.CustomId.Substring("rejectUser/".Length));
                            interaction.RespondAsync(interaction.User.Mention + ", are you sure you want to accept this user?", components: new ComponentBuilder()
                                    .WithButton("Confirm", "acceptUserConfirm/" + uid, ButtonStyle.Success)
                                    .WithButton("Cancel", "dismiss", ButtonStyle.Primary)
                                .Build()).GetAwaiter().GetResult();
                        }
                        catch { }
                    }
                    else if (interaction.Data.CustomId.StartsWith("acceptUserConfirm/"))
                    {
                        try
                        {
                            // Handle accept
                            ulong sparkRole = (ulong)conf.GetOrDefault("sparkRole", (ulong)0);
                            if (sparkRole != 0)
                            {
                                // Retrieve roles
                                SocketRole rCheckRole = (SocketRole)guild.GetRole(sparkRole);
                                ulong verifiedSparkRole = (ulong)conf.GetOrDefault("verifiedSparkRole", (ulong)0);
                                if (verifiedSparkRole != 0)
                                    rCheckRole = (SocketRole)guild.GetRole(verifiedSparkRole);

                                // Get user ID
                                ulong uid = ulong.Parse(interaction.Data.CustomId.Substring("acceptUserConfirm/".Length));
                                if (guild.GetUser(uid).Roles.FirstOrDefault(t => t.Id == rCheckRole.Id) != null)
                                {
                                    interaction.RespondAsync("<@!" + uid + "> is already a verified member.", ephemeral: true).GetAwaiter().GetResult();
                                    return Task.CompletedTask;
                                }

                                // Defer
                                interaction.DeferAsync().GetAwaiter().GetResult();

                                // Delete
                                interaction.Message.DeleteAsync().GetAwaiter().GetResult();

                                // Update embed
                                if (serverMemory[server].ContainsKey("verification-" + user.Id))
                                {
                                    // Get review message
                                    string msg = serverMemory[server].GetValue("verification-" + user.Id).ToString();
                                    serverMemory[server].Remove("verification-" + user.Id);

                                    // Parse info
                                    string chID = msg.Split("/")[0];
                                    string msgID = msg.Split("/")[1];

                                    // Get message object
                                    IUserMessage message = (IUserMessage)guild.GetTextChannel(ulong.Parse(chID)).GetMessageAsync(ulong.Parse(msgID)).GetAwaiter().GetResult();

                                    // Update embed
                                    string ava = interaction.User.GetAvatarUrl();
                                    if (ava == null || ava == "")
                                        ava = interaction.User.GetDefaultAvatarUrl();
                                    message.ModifyAsync(t =>
                                    {
                                        t.Embed = new Optional<Embed>(message.Embeds.ToArray()[0].ToEmbedBuilder()
                                            .WithColor(Color.Green)
                                        .WithFooter("Status: accepted by " + interaction.User.Username + "#" + interaction.User.Discriminator, ava)
                                            .Build());
                                        t.Components = new Optional<MessageComponent>(new ComponentBuilder().Build());
                                    }).GetAwaiter().GetResult();
                                }

                                // Add roles
                                SocketRole sparks = (SocketRole)guild.GetRole(sparkRole);
                                guild.GetUser(uid).AddRoleAsync(sparks.Id).GetAwaiter().GetResult();
                                if (verifiedSparkRole != 0)
                                {
                                    SocketRole verifiedSparks = (SocketRole)guild.GetRole(verifiedSparkRole);
                                    if (verifiedSparks != null)
                                    {
                                        guild.GetUser(uid).AddRoleAsync(verifiedSparks.Id).GetAwaiter().GetResult();
                                    }
                                }

                                // Respond
                                guild.SystemChannel.SendMessageAsync("Welcome <@!" + uid + ">!").GetAwaiter().GetResult();
                            }
                        }
                        catch { }
                    }
                    else if (interaction.Data.CustomId.StartsWith("rejectUser/"))
                    {
                        try
                        {
                            // Confirm
                            ulong uid = ulong.Parse(interaction.Data.CustomId.Substring("rejectUser/".Length));
                            interaction.RespondAsync(interaction.User.Mention + ", are you sure you want to reject this user?", components: new ComponentBuilder()
                                    .WithButton("Confirm", "rejectUserConfirm/" + uid, ButtonStyle.Danger)
                                    .WithButton("Cancel", "dismiss", ButtonStyle.Primary)
                                .Build()).GetAwaiter().GetResult();
                        }
                        catch { }
                    }
                    else if (interaction.Data.CustomId == "dismiss")
                    {
                        // Defer
                        interaction.DeferAsync().GetAwaiter().GetResult();

                        // Delete
                        interaction.Message.DeleteAsync().GetAwaiter().GetResult();
                    }
                    else if (interaction.Data.CustomId.StartsWith("rejectUserConfirm/"))
                    {
                        try
                        {
                            ulong uid = ulong.Parse(interaction.Data.CustomId.Substring("rejectUserConfirm/".Length));

                            // Defer
                            interaction.DeferAsync().GetAwaiter().GetResult();
                            
                            // Delete
                            interaction.Message.DeleteAsync().GetAwaiter().GetResult();

                            // Update embed
                            if (serverMemory[server].ContainsKey("verification-" + user.Id))
                            {
                                // Get review message
                                string msg = serverMemory[server].GetValue("verification-" + user.Id).ToString();
                                serverMemory[server].Remove("verification-" + user.Id);

                                // Parse info
                                string chID = msg.Split("/")[0];
                                string msgID = msg.Split("/")[1];

                                // Get message object
                                IUserMessage message = (IUserMessage)guild.GetTextChannel(ulong.Parse(chID)).GetMessageAsync(ulong.Parse(msgID)).GetAwaiter().GetResult();

                                // Update embed
                                string ava = interaction.User.GetAvatarUrl();
                                if (ava == null || ava == "")
                                    ava = interaction.User.GetDefaultAvatarUrl();
                                message.ModifyAsync(t =>
                                {
                                    t.Embed = new Optional<Embed>(message.Embeds.ToArray()[0].ToEmbedBuilder()
                                        .WithColor(Color.DarkRed)
                                        .WithFooter("Status: rejected by " + interaction.User.Username + "#" + interaction.User.Discriminator, ava)
                                        .Build());
                                    t.Components = new Optional<MessageComponent>(new ComponentBuilder().Build());
                                }).GetAwaiter().GetResult();
                            }

                            // Kick
                            guild.GetUser(uid).KickAsync().GetAwaiter().GetResult();
                        }
                        catch { }
                    }
                }

                return Task.CompletedTask;
            };

            // Load servers
            foreach (Server server in bot.servers)
            {
                SocketGuild guild = bot.client.GetGuild(server.id);
                serverMemory[server] = new ConfigDictionary<string, object>();
                LoadServer(bot, guild, server);
            }
        }

        public override void PreInit(Bot bot)
        {
        }

        public void LoadServer(Bot bot, SocketGuild guild, Server server)
        {
            // Create message if needed
            ModuleConfig conf = server.GetModuleConfig(this);
            ulong sparkRole = (ulong)conf.GetOrDefault("sparkRole", (ulong)0);
            if (sparkRole != 0)
            {
                SocketRole sparks = (SocketRole)guild.GetRole(sparkRole);
                ulong reviewerRole = (ulong)conf.GetOrDefault("reviewerRole", (ulong)0);
                SocketRole reviewers = (SocketRole)guild.GetRole(reviewerRole);
                if (sparks != null && reviewers != null)
                {
                    ulong reviewChannel = (ulong)conf.GetOrDefault("reviewChannel", (ulong)0);
                    ulong verificationChannel = (ulong)conf.GetOrDefault("verificationChannel", (ulong)0);

                    SocketTextChannel verifyCh = guild.GetTextChannel(verificationChannel);
                    SocketTextChannel reviewCh = guild.GetTextChannel(reviewChannel);
                    if (verifyCh != null && reviewCh != null)
                    {
                        ulong verificationMessageID = (ulong)conf.GetOrDefault("verificationMessageID", (ulong)0);
                        IMessage msg = null;

                        try
                        {
                            if (verificationMessageID != 0)
                                msg = verifyCh.GetMessageAsync(verificationMessageID).GetAwaiter().GetResult();
                        }
                        catch
                        {}

                        if (msg == null)
                        {
                            try
                            {
                                msg = verifyCh.SendMessageAsync("", false, new EmbedBuilder()
                                    .WithTitle("Welcome to " + guild.Name + "!")
                                    .WithColor(Color.Blue)
                                    .WithDescription(conf.Get("messageContent").ToString())
                                .Build(), null, null, null, new ComponentBuilder()
                                    .WithButton(conf.Get("buttonMessage").ToString(), "beginVerification", ButtonStyle.Success)
                                .Build()).GetAwaiter().GetResult();
                                    conf.Set("verificationMessageID", msg.Id);
                                    server.SaveAll();
                            }
                            catch
                            {

                            }
                        }
                    }
                }
            }

            // Create commands
            if (!guild.GetApplicationCommandsAsync().GetAwaiter().GetResult().Any(t => t.Name == "queenguard"))
            {
                // Create command as its not present
                guild.CreateApplicationCommandAsync(new SlashCommandBuilder()
                        .WithName("queenguard")
                        .WithDescription("Queen Guard Configuration Command")
                        .AddOption(new SlashCommandOptionBuilder()
                            .WithName("setup")
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithRequired(true)
                                .WithName("verification-role")
                                .WithDescription("The role given after verification")
                                .WithType(ApplicationCommandOptionType.Role))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithRequired(true)
                                .WithName("reviewer-role")
                                .WithDescription("The role pinged for verification review")
                                .WithType(ApplicationCommandOptionType.Role))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithRequired(true)
                                .WithName("review-channel")
                                .WithDescription("The channel used to send verification requests to")
                                .WithType(ApplicationCommandOptionType.Channel))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithRequired(true)
                                .WithName("panel-channel")
                                .WithDescription("The channel to send the verification panel to")
                                .WithType(ApplicationCommandOptionType.Channel))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithRequired(false)
                                .WithName("panel-message")
                                .WithDescription("The message shown in the verification panel")
                                .WithMinValue(3)
                                .WithMaxValue(2000)
                                .WithType(ApplicationCommandOptionType.String))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithRequired(false)
                                .WithName("panel-button")
                                .WithDescription("The text of the verification button")
                                .WithMinValue(3)
                                .WithMaxValue(80)
                                .WithType(ApplicationCommandOptionType.String))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithRequired(false)
                                .WithName("secondary-verification-role")
                                .WithDescription("The secondary role given after verification")
                                .WithType(ApplicationCommandOptionType.Role))
                            .WithDescription("Creates a verification form")
                            .WithType(ApplicationCommandOptionType.SubCommand))
                        .AddOption(new SlashCommandOptionBuilder()
                            .WithName("edit")
                            .WithDescription("Edits a verification form")
                            .WithType(ApplicationCommandOptionType.SubCommandGroup)
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithName("form")
                                .WithDescription("Edits the verification form")
                                .WithType(ApplicationCommandOptionType.SubCommand))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithName("verification-role")
                                .AddOption(new SlashCommandOptionBuilder()
                                    .WithRequired(true)
                                    .WithName("verification-role")
                                    .WithDescription("The role given after verification")
                                    .WithType(ApplicationCommandOptionType.Role))
                                .AddOption(new SlashCommandOptionBuilder()
                                    .WithRequired(false)
                                    .WithName("secondary-verification-role")
                                    .WithDescription("The secondary role given after verification")
                                    .WithType(ApplicationCommandOptionType.Role))
                                .WithDescription("Edits the verification role")
                                .WithType(ApplicationCommandOptionType.SubCommand))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithName("panel")
                                .AddOption(new SlashCommandOptionBuilder()
                                    .WithRequired(true)
                                    .WithName("panel-channel")
                                    .WithDescription("The channel to send the verification panel to")
                                    .WithType(ApplicationCommandOptionType.Channel))
                                .AddOption(new SlashCommandOptionBuilder()
                                    .WithRequired(false)
                                    .WithName("panel-message")
                                    .WithDescription("The message shown in the verification panel")
                                    .WithMinValue(3)
                                    .WithMaxValue(2000)
                                    .WithType(ApplicationCommandOptionType.String))
                                .AddOption(new SlashCommandOptionBuilder()
                                    .WithRequired(false)
                                    .WithName("panel-button")
                                    .WithDescription("The text of the verification button")
                                    .WithMinValue(3)
                                    .WithMaxValue(80)
                                    .WithType(ApplicationCommandOptionType.String))
                                .WithDescription("Edits the verification panel")
                                .WithType(ApplicationCommandOptionType.SubCommand))
                            .AddOption(new SlashCommandOptionBuilder()
                                .WithName("review")
                                .AddOption(new SlashCommandOptionBuilder()
                                    .WithRequired(true)
                                    .WithName("reviewer-role")
                                    .WithDescription("The role pinged for verification review")
                                    .WithType(ApplicationCommandOptionType.Role))
                                .AddOption(new SlashCommandOptionBuilder()
                                    .WithRequired(true)
                                    .WithName("review-channel")
                                    .WithDescription("The channel used to send verification requests to")
                                    .WithType(ApplicationCommandOptionType.Channel))
                                .WithDescription("Edits the verification panel")
                                .WithType(ApplicationCommandOptionType.SubCommand))
                            )
                    .Build()).GetAwaiter().GetResult();
            }
        }

        public override void RegisterCommands(Bot bot)
        {
        }
    }
}