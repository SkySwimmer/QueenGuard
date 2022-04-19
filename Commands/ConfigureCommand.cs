using CMDR;
using Discord.WebSocket;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using Discord;

namespace Ferever {
    public class ConfigureCommand : SystemCommand {
        private Module module;
        
        public ConfigureCommand(Module module) {
            this.module = module;
            module.GetBot().client.SelectMenuExecuted += (interaction) => {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser) {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(module);

                    if (interaction.Data.CustomId == "sparkRole") {
                        interaction.DeferAsync().GetAwaiter().GetResult();
                        module.serverMemory[server]["SetupSparkRole"] = ulong.Parse(interaction.Data.Values.ToArray()[0]);
                    } else if (interaction.Data.CustomId == "reviewerRole") {
                        interaction.DeferAsync().GetAwaiter().GetResult();
                        module.serverMemory[server]["SetupReviewerRole"] = ulong.Parse(interaction.Data.Values.ToArray()[0]);
                    } else if (interaction.Data.CustomId == "reviewChannel") {
                        interaction.DeferAsync().GetAwaiter().GetResult();
                        module.serverMemory[server]["SetupReviewChannel"] = ulong.Parse(interaction.Data.Values.ToArray()[0]);
                    } else if (interaction.Data.CustomId == "verificationChannel") {
                        interaction.DeferAsync().GetAwaiter().GetResult();
                        module.serverMemory[server]["SetupVerificationChannel"] = ulong.Parse(interaction.Data.Values.ToArray()[0]);
                    }
                }

                return Task.CompletedTask;
            };

            module.GetBot().client.ModalSubmitted += (interaction) => {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser) {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(module);

                    if (interaction.Data.CustomId == "verificationMessageEditor") {
                        interaction.DeferAsync().GetAwaiter().GetResult();
                        module.serverMemory[server]["SetupVerificationMessage"] = interaction.Data.Components.First(t => t.CustomId == "editor").Value;
                    } else if (interaction.Data.CustomId == "verificationFormEditor") {
                        interaction.DeferAsync().GetAwaiter().GetResult();
                        foreach (var comp in interaction.Data.Components) {
                            string val = comp.Value;
                            if (val == "<skipped>")
                                val = "";
                            module.serverMemory[server]["SetupVerification" + comp.CustomId] = val;
                        }
                    }
                }

                return Task.CompletedTask;
            };

            module.GetBot().client.ButtonExecuted += (interaction) => {
                if (interaction.Channel is SocketTextChannel && interaction.User is SocketGuildUser) {
                    SocketGuildUser user = (SocketGuildUser)interaction.User;
                    SocketTextChannel channel = (SocketTextChannel)interaction.Channel;
                    SocketGuild guild = channel.Guild;
                    Server server = GetBot().GetServerFromSocketGuild(guild);
                    Server.ModuleConfig conf = server.GetModuleConfig(module);

                    if (interaction.Data.CustomId == "setupNext" && module.serverMemory[server].ContainsKey("SetupStep")) {
                        if (((int)module.serverMemory[server]["SetupStep"]) == 1 && module.serverMemory[server].ContainsKey("SetupSparkRole")) {
                            List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                            foreach (SocketRole role in guild.Roles) {
                                if (!role.IsEveryone) {
                                    if (conf.GetOrDefault("reviewerRole", null) != null) {
                                        if (((ulong)conf.GetOrDefault("reviewerRole", (ulong)0)) == role.Id) {
                                            module.serverMemory[server]["SetupReviewerRole"] = role.Id;
                                            options.Add(new SelectMenuOptionBuilder()
                                                .WithLabel(role.Name)
                                                .WithValue(role.Id.ToString())
                                                .WithDefault(true)
                                            );
                                            continue;
                                        }
                                    }
                                    options.Add(new SelectMenuOptionBuilder()
                                        .WithLabel(role.Name)
                                        .WithValue(role.Id.ToString())
                                    );
                                }
                            }

                            module.serverMemory[server]["SetupStep"] = 2;
                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                .WithTitle("Queen Guard Setup: Step 2")
                                .WithDescription("Please select the role to ping after a verification form has been submitted.")
                                .WithColor(Color.Gold)
                            .Build() }, false, false, null, new ComponentBuilder()
                                .WithSelectMenu("reviewerRole", options)
                                .WithButton("Next", "setupNext", ButtonStyle.Success)
                            .Build()).GetAwaiter().GetResult();
                        } else if (((int)module.serverMemory[server]["SetupStep"]) == 2 && module.serverMemory[server].ContainsKey("SetupReviewerRole")) {
                            List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                            foreach (SocketTextChannel ch in guild.TextChannels) {
                                if (conf.GetOrDefault("reviewChannel", null) != null) {
                                    if (((ulong)conf.GetOrDefault("reviewChannel", (ulong)0)) == ch.Id) {
                                        module.serverMemory[server]["SetupReviewChannel"] = ch.Id;
                                        options.Add(new SelectMenuOptionBuilder()
                                            .WithLabel(ch.Name)
                                            .WithValue(ch.Id.ToString())
                                            .WithDefault(true)
                                        );
                                        continue;
                                    }
                                }
                                options.Add(new SelectMenuOptionBuilder()
                                    .WithLabel(ch.Name)
                                    .WithValue(ch.Id.ToString())
                                );
                            }

                            module.serverMemory[server]["SetupStep"] = 3;
                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                .WithTitle("Queen Guard Setup: Step 3")
                                .WithDescription("Please select the channel to which to send **submission review messages**.\nPlease make sure that this is a staff-only channel.")
                                .WithColor(Color.Gold)
                            .Build() }, false, false, null, new ComponentBuilder()
                                .WithSelectMenu("reviewChannel", options)
                                .WithButton("Next", "setupNext", ButtonStyle.Success)
                            .Build()).GetAwaiter().GetResult();
                        } else if (((int)module.serverMemory[server]["SetupStep"]) == 3 && module.serverMemory[server].ContainsKey("SetupReviewChannel")) {
                             List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                            foreach (SocketTextChannel ch in guild.TextChannels) {
                                if (conf.GetOrDefault("verificationChannel", null) != null) {
                                    if (((ulong)conf.GetOrDefault("verificationChannel", (ulong)0)) == ch.Id) {
                                        module.serverMemory[server]["SetupVerificationChannel"] = ch.Id;
                                        options.Add(new SelectMenuOptionBuilder()
                                            .WithLabel(ch.Name)
                                            .WithValue(ch.Id.ToString())
                                            .WithDefault(true)
                                        );
                                        continue;
                                    }
                                }
                                options.Add(new SelectMenuOptionBuilder()
                                    .WithLabel(ch.Name)
                                    .WithValue(ch.Id.ToString())
                                );
                            }

                            module.serverMemory[server]["SetupStep"] = 4;
                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                .WithTitle("Queen Guard Setup: Step 4")
                                .WithDescription("Please select the channel to which to send the verification interface message.\nThis channel is used to verify new members.\n\nPlease make sure this is a read-only channel for unverified members.")
                                .WithColor(Color.Gold)
                            .Build() }, false, false, null, new ComponentBuilder()
                                .WithSelectMenu("verificationChannel", options)
                                .WithButton("Next", "setupNext", ButtonStyle.Success)
                            .Build()).GetAwaiter().GetResult();
                        } else if (((int)module.serverMemory[server]["SetupStep"]) == 4 && module.serverMemory[server].ContainsKey("SetupVerificationChannel")) {
                            module.serverMemory[server]["SetupStep"] = 5;
                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                .WithTitle("Queen Guard Setup: Step 5")
                                .WithDescription("Please create a verification welcome message.\nThis message will be displayed in the verification embed.\n\nPress 'Open Editor' to open the editor.")
                                .WithColor(Color.Gold)
                            .Build() }, false, false, null, new ComponentBuilder()
                                .WithButton("Open Editor", "verificationMessageEditor")
                                .WithButton("Next", "setupNext", ButtonStyle.Success)
                            .Build()).GetAwaiter().GetResult();
                        } else if (((int)module.serverMemory[server]["SetupStep"]) == 5) {
                            module.serverMemory[server]["SetupStep"] = 6;
                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                .WithTitle("Queen Guard Setup: Step 6")
                                .WithDescription("You can now create the 5 verification questions, press 'Open Editor' to start.")
                                .WithColor(Color.Gold)
                            .Build() }, false, false, null, new ComponentBuilder()
                                .WithButton("Open Editor", "verificationFormEditor")
                                .WithButton("Finish Setup", "setupNext", ButtonStyle.Success)
                            .Build()).GetAwaiter().GetResult();
                        } else if (((int)module.serverMemory[server]["SetupStep"]) == 6) {
                            interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                .WithTitle("Queen Guard Setup")
                                .WithDescription("Saving configuration...")
                                .WithColor(Color.DarkGreen)
                            .Build() }).GetAwaiter().GetResult();

                            string oldQ1 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion1", conf.GetOrDefault("verificationQuestion1", "<skipped>").ToString()).ToString();
                            if (oldQ1 == "")
                                oldQ1 = "<skipped>";
                            string oldQ2 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion2", conf.GetOrDefault("verificationQuestion2", "<skipped>").ToString()).ToString();
                            if (oldQ2 == "")
                                oldQ2 = "<skipped>";
                            string oldQ3 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion3", conf.GetOrDefault("verificationQuestion3", "<skipped>").ToString()).ToString();
                            if (oldQ3 == "")
                                oldQ3 = "<skipped>";
                            string oldQ4 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion4", conf.GetOrDefault("verificationQuestion4", "<skipped>").ToString()).ToString();
                            if (oldQ4 == "")
                                oldQ4 = "<skipped>";
                            string oldQ5 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion5", conf.GetOrDefault("verificationQuestion5", "<skipped>").ToString()).ToString();
                            if (oldQ5 == "")
                                oldQ5 = "<skipped>";
                            conf.Set("verificationQuestion1", oldQ1);
                            conf.Set("verificationQuestion2", oldQ2);
                            conf.Set("verificationQuestion3", oldQ3);
                            conf.Set("verificationQuestion4", oldQ4);
                            conf.Set("verificationQuestion5", oldQ5);
                            
                            conf.Set("reviewChannel", module.serverMemory[server]["SetupReviewChannel"]);
                            conf.Set("verificationChannel", module.serverMemory[server]["SetupVerificationChannel"]);

                            string oldMsg = module.serverMemory[server].GetValueOrDefault("SetupVerificationMessage", conf.GetOrDefault("messageContent", "**Welcome to " + guild.Name + "!**\n"
                                + "\n"
                                + "Please verify that you are a ferellian to gain access to this server.\n"
                                + "To begin the verification process, press 'Verify' below.")).ToString();
                            conf.Set("messageContent", oldMsg);
                            conf.Set("reviewerRole", module.serverMemory[server]["SetupReviewerRole"]);
                            conf.Set("sparkRole", module.serverMemory[server]["SetupSparkRole"]);
                            
                            server.SaveAll();
                            module.LoadServer(GetBot(), guild, server);

                            channel.SendMessageAsync("", false, new EmbedBuilder()
                                .WithTitle("Queen Guard Setup")
                                .WithDescription("Saved and reloaded the server configuration!")
                                .WithColor(Color.Green)
                            .Build()).GetAwaiter().GetResult();
                        }
                    } else if (interaction.Data.CustomId == "verificationMessageEditor" && module.serverMemory[server].ContainsKey("SetupReviewChannel")) {
                        string oldMsg = module.serverMemory[server].GetValueOrDefault("SetupVerificationMessage", conf.GetOrDefault("messageContent", "**Welcome to " + guild.Name + "!**\n"
                            + "\n"
                            + "Please verify that you are a ferellian to gain access to this server.\n"
                            + "To begin the verification process, press 'Verify' below.")).ToString();
                        interaction.RespondWithModalAsync(new ModalBuilder()
                            .WithTitle("Message Editor")
                            .WithCustomId("verificationMessageEditor")
                            .AddTextInput(new TextInputBuilder()
                                .WithLabel("Message")
                                .WithCustomId("editor")
                                .WithStyle(TextInputStyle.Paragraph)
                                .WithMaxLength(4000)
                                .WithValue(oldMsg))
                        .Build());
                    } else if (interaction.Data.CustomId == "verificationFormEditor" && module.serverMemory[server].ContainsKey("SetupVerificationMessage")) {
                        string oldQ1 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion1", conf.GetOrDefault("verificationQuestion1", "<skipped>").ToString()).ToString();
                        if (oldQ1 == "")
                            oldQ1 = "<skipped>";
                        string oldQ2 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion2", conf.GetOrDefault("verificationQuestion2", "<skipped>").ToString()).ToString();
                        if (oldQ2 == "")
                            oldQ2 = "<skipped>";
                        string oldQ3 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion3", conf.GetOrDefault("verificationQuestion3", "<skipped>").ToString()).ToString();
                        if (oldQ3 == "")
                            oldQ3 = "<skipped>";
                        string oldQ4 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion4", conf.GetOrDefault("verificationQuestion4", "<skipped>").ToString()).ToString();
                        if (oldQ4 == "")
                            oldQ4 = "<skipped>";
                        string oldQ5 = module.serverMemory[server].GetValueOrDefault("SetupVerificationQuestion5", conf.GetOrDefault("verificationQuestion5", "<skipped>").ToString()).ToString();
                        if (oldQ5 == "")
                            oldQ5 = "<skipped>";
                        interaction.RespondWithModalAsync(new ModalBuilder()
                            .WithTitle("Form Editor")
                            .WithCustomId("verificationFormEditor")
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
                }

                return Task.CompletedTask;
            };
        }

        public override CmdCategory[] Categories => new CmdCategory[] { new CmdCategory("utility", "Utility commands"), new CmdCategory("verification", "Verification commands") };

        public override string commandid => "configure-queen-guard";
        public override string helpsyntax => "";
        public override string description => "configures the Queen Guard Discord Bot";
        public override string permissionnode => "commands.administration.configure.queenguard";

        public override bool setNoCmdPrefix => false;
        public override bool allowTerminal => false;
        public override bool allowDiscord => true;

        public override async Task OnExecuteFromDiscord(SocketGuild guild, SocketUser usr, SocketTextChannel channel, SocketMessage messageobject, string fullmessage, string arguments_string, List<string> arguments) {
            Server server = GetBot().GetServerFromSocketGuild(guild);
            Server.ModuleConfig conf = server.GetModuleConfig(module);
            List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
            foreach (SocketRole role in guild.Roles) {
                if (!role.IsEveryone) {
                    if (conf.GetOrDefault("sparkRole", null) != null) {
                        if (((ulong)conf.GetOrDefault("sparkRole", (ulong)0)) == role.Id) {
                            module.serverMemory[server]["SetupSparkRole"] = role.Id;
                            options.Add(new SelectMenuOptionBuilder()
                                .WithLabel(role.Name)
                                .WithValue(role.Id.ToString())
                                .WithDefault(true)
                            );
                            continue;
                        }
                    }
                    options.Add(new SelectMenuOptionBuilder()
                        .WithLabel(role.Name)
                        .WithValue(role.Id.ToString())
                    );
                }
            }

            module.serverMemory[server]["SetupStep"] = 1;
            await channel.SendMessageAsync("", false, new EmbedBuilder()
                .WithTitle("Queen Guard Setup: Step 1")
                .WithDescription("Please select what role to assign to verified members.")
                .WithColor(Color.Gold)
            .Build(), null, null, null, new ComponentBuilder()
                .WithSelectMenu("sparkRole", options)
                .WithButton("Next", "setupNext", ButtonStyle.Success)
            .Build());
        }
        
        public override void OnExecuteFromTerminal(string fullcommand, string arguments_string, List<string> arguments) {
        }
    }
}
