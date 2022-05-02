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
using System;

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
                    } else  if (interaction.Data.CustomId == "verifiedSparkRole") {
                        interaction.DeferAsync().GetAwaiter().GetResult();
                        module.serverMemory[server]["SetupVerifiedSparkRole"] = ulong.Parse(interaction.Data.Values.ToArray()[0]);
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

                    try {
                        if (interaction.Data.CustomId == "setupNext" && module.serverMemory[server].ContainsKey("SetupStep")) {
                            if (((int)module.serverMemory[server]["SetupStep"]) == 1 && module.serverMemory[server].ContainsKey("SetupSparkRole")) {
                                List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                                int i = 0;
                                foreach (SocketRole role in guild.Roles) {
                                    if (!role.IsEveryone && !role.Permissions.Administrator && (role.Name.ToLower().Contains("spark") || role.Name.ToLower().Contains("member"))) {
                                        i++;
                                        if (i == 25)
                                            break; // FIXME: Temporary workaround so that ferever can put this bot to use
                                        if (conf.GetOrDefault("verifiedSparkRole", null) != null) {
                                            if (((ulong)conf.GetOrDefault("verifiedSparkRole", (ulong)0)) == role.Id) {
                                                module.serverMemory[server]["SetupVerifiedSparkRole"] = role.Id;
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
                                foreach (SocketRole role in guild.Roles) {
                                    if (!role.IsEveryone && !role.Permissions.Administrator && !role.Name.ToLower().Contains("spark") && !role.Name.ToLower().Contains("member")) {
                                        i++;
                                        if (i == 25)
                                            break; // FIXME: Temporary workaround so that ferever can put this bot to use
                                        if (conf.GetOrDefault("verifiedSparkRole", null) != null) {
                                            if (((ulong)conf.GetOrDefault("verifiedSparkRole", (ulong)0)) == role.Id) {
                                                module.serverMemory[server]["SetupVerifiedSparkRole"] = role.Id;
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

                                try {
                                    interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                        .WithTitle("Queen Guard Setup: Step 2")
                                        .WithDescription("Please select what secondary role to assign to verified members.\nThis role will be used to allow existing members to verify.")
                                        .WithColor(Color.Gold)
                                    .Build() }, false, false, null, new ComponentBuilder()
                                        .WithSelectMenu("verifiedSparkRole", options)
                                        .WithButton("Next", "setupNext", ButtonStyle.Success)
                                    .Build()).GetAwaiter().GetResult();
                                    module.serverMemory[server]["SetupStep"] = 2;
                                } catch {
                                    channel.SendMessageAsync("", false, new EmbedBuilder()
                                        .WithTitle("Queen Guard: An error occured")
                                        .WithDescription("Could not create a 'Member Role' selection list, this is most likely caused by incompatible roles.")
                                        .WithColor(Color.Red)
                                    .Build()).GetAwaiter().GetResult();
                                }
                            } else if (((int)module.serverMemory[server]["SetupStep"]) == 2 && module.serverMemory[server].ContainsKey("SetupVerifiedSparkRole")) {
                                int i = 0;
                                List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                                foreach (SocketRole role in guild.Roles) {
                                    if (!role.IsEveryone && (role.Name.ToLower().Contains("review") || role.Name.ToLower().Contains("mod") || role.Name.ToLower().Contains("admin"))) {
                                        i++;
                                        if (i == 25)
                                            break; // FIXME: Temporary workaround so that ferever can put this bot to use
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
                                foreach (SocketRole role in guild.Roles) {
                                    if (!role.IsEveryone && !role.Name.ToLower().Contains("review") && !role.Name.ToLower().Contains("mod") && !role.Name.ToLower().Contains("admin")) {
                                        i++;
                                        if (i == 25)
                                            break; // FIXME: Temporary workaround so that ferever can put this bot to use
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

                                try {
                                    interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                        .WithTitle("Queen Guard Setup: Step 3")
                                        .WithDescription("Please select the role to ping after a verification form has been submitted.")
                                        .WithColor(Color.Gold)
                                    .Build() }, false, false, null, new ComponentBuilder()
                                        .WithSelectMenu("reviewerRole", options)
                                        .WithButton("Next", "setupNext", ButtonStyle.Success)
                                    .Build()).GetAwaiter().GetResult();
                                    module.serverMemory[server]["SetupStep"] = 3;
                                } catch {
                                    channel.SendMessageAsync("", false, new EmbedBuilder()
                                        .WithTitle("Queen Guard: An error occured")
                                        .WithDescription("Could not create a 'Reviewer Role' selection list, this is most likely caused by incompatible roles.")
                                        .WithColor(Color.Red)
                                    .Build()).GetAwaiter().GetResult();
                                }
                            } else if (((int)module.serverMemory[server]["SetupStep"]) == 3 && module.serverMemory[server].ContainsKey("SetupReviewerRole")) {
                                int i = 0;
                                List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                                ulong sparkR = (ulong)module.serverMemory[server]["SetupSparkRole"];
                                SocketRole sparks = guild.GetRole(sparkR);
                                ulong vSparkR = (ulong)module.serverMemory[server]["SetupVerifiedSparkRole"];
                                SocketRole verifiedSparks = guild.GetRole(vSparkR);
                                ulong reviewerR = (ulong)module.serverMemory[server]["SetupReviewerRole"];
                                SocketRole reviewers = guild.GetRole(reviewerR);
                                foreach (SocketTextChannel ch in guild.TextChannels) {
                                    if (!ch.Name.ToLower().Contains("review")) {
                                        continue;
                                    }
                                    if (sparks != null) {
                                        if (canViewChannel(sparks, ch) || canViewChannel(verifiedSparks, ch) || canViewChannel(guild.EveryoneRole, ch)) {
                                            continue;
                                        }
                                    }
                                    if (reviewers != null) {
                                        if (!canViewChannel(reviewers, ch))
                                            continue;
                                    }
                                    
                                    i++;
                                    if (i == 25)
                                        break; // FIXME: Temporary workaround so that ferever can put this bot to use
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
                                foreach (SocketTextChannel ch in guild.TextChannels) {
                                    if (ch.Name.ToLower().Contains("review")) {
                                        continue;
                                    }
                                    if (sparks != null) {
                                        if (canViewChannel(sparks, ch) || canViewChannel(guild.EveryoneRole, ch)) {
                                            continue;
                                        }
                                    }
                                    if (reviewers != null) {
                                        if (!canViewChannel(reviewers, ch))
                                            continue;
                                    }
                                    
                                    i++;
                                    if (i == 25)
                                        break; // FIXME: Temporary workaround so that ferever can put this bot to use
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

                                try {
                                    interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                        .WithTitle("Queen Guard Setup: Step 4")
                                        .WithDescription("Please select the channel to which to send **submission review messages**.\nPlease make sure that this is a staff-only channel.")
                                        .WithColor(Color.Gold)
                                    .Build() }, false, false, null, new ComponentBuilder()
                                        .WithSelectMenu("reviewChannel", options)
                                        .WithButton("Next", "setupNext", ButtonStyle.Success)
                                    .Build()).GetAwaiter().GetResult();
                                    module.serverMemory[server]["SetupStep"] = 4;
                                } catch {
                                    channel.SendMessageAsync("", false, new EmbedBuilder()
                                        .WithTitle("Queen Guard: An error occured")
                                        .WithDescription("Unable to create a 'Submission Review Channel' selection menu, this is most likely due to incompatible channels. A review channel should only be visible for the selected 'Reviewer' role, if members can view.")
                                        .WithColor(Color.Red)
                                    .Build()).GetAwaiter().GetResult();
                                }
                            } else if (((int)module.serverMemory[server]["SetupStep"]) == 4 && module.serverMemory[server].ContainsKey("SetupReviewChannel")) {
                                List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                                int i = 0;

                                ulong sparkR = (ulong)module.serverMemory[server]["SetupSparkRole"];
                                SocketRole sparks = guild.GetRole(sparkR);
                                ulong vSparkR = (ulong)module.serverMemory[server]["SetupVerifiedSparkRole"];
                                SocketRole verifiedSparks = guild.GetRole(vSparkR);
                                foreach (SocketTextChannel ch in guild.TextChannels) {
                                    if (!ch.Name.ToLower().Contains("verif")) {
                                        continue;
                                    }
                                    if (sparks != null) {
                                        if (canViewChannel(sparks, ch) || !canViewChannel(guild.EveryoneRole, ch)) {
                                            continue;
                                        }
                                    }

                                    i++;
                                    if (i == 25)
                                        break; // FIXME: Temporary workaround so that ferever can put this bot to use
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
                                foreach (SocketTextChannel ch in guild.TextChannels) {
                                    if (ch.Name.ToLower().Contains("verif")) {
                                        continue;
                                    }
                                    if (sparks != null) {
                                        if ((canViewChannel(sparks, ch) && canViewChannel(verifiedSparks, ch)) || canViewChannel(verifiedSparks, ch) || !canViewChannel(guild.EveryoneRole, ch)) {
                                            continue;
                                        }
                                    }

                                    i++;
                                    if (i == 25)
                                        break; // FIXME: Temporary workaround so that ferever can put this bot to use
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

                                try {
                                    interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                        .WithTitle("Queen Guard Setup: Step 5")
                                        .WithDescription("Please select the channel to which to send the verification interface message.\nThis channel is used to verify new members.\n\nPlease make sure this is a read-only channel for unverified members.")
                                        .WithColor(Color.Gold)
                                    .Build() }, false, false, null, new ComponentBuilder()
                                        .WithSelectMenu("verificationChannel", options)
                                        .WithButton("Next", "setupNext", ButtonStyle.Success)
                                    .Build()).GetAwaiter().GetResult();
                                    module.serverMemory[server]["SetupStep"] = 5;
                                } catch {
                                    channel.SendMessageAsync("", false, new EmbedBuilder()
                                        .WithTitle("Queen Guard: An error occured")
                                        .WithDescription("Unable to create a 'Verification Channel' selection menu, this is most likely due to incompatible channels. A verification channel should only be visible for members without the selected 'Member' role.")
                                        .WithColor(Color.Red)
                                    .Build()).GetAwaiter().GetResult();
                                }
                            } else if (((int)module.serverMemory[server]["SetupStep"]) == 5 && module.serverMemory[server].ContainsKey("SetupVerificationChannel")) {
                                interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                    .WithTitle("Queen Guard Setup: Step 6")
                                    .WithDescription("Please create a verification welcome message.\nThis message will be displayed in the verification embed.\n\nPress 'Open Editor' to open the editor.")
                                    .WithColor(Color.Gold)
                                .Build() }, false, false, null, new ComponentBuilder()
                                    .WithButton("Open Editor", "verificationMessageEditor")
                                    .WithButton("Next", "setupNext", ButtonStyle.Success)
                                .Build()).GetAwaiter().GetResult();
                                module.serverMemory[server]["SetupStep"] = 6;
                            } else if (((int)module.serverMemory[server]["SetupStep"]) == 6) {
                                interaction.RespondAsync("", new Embed[] { new EmbedBuilder()
                                    .WithTitle("Queen Guard Setup: Step 7")
                                    .WithDescription("You can now create the 5 verification questions, press 'Open Editor' to start.")
                                    .WithColor(Color.Gold)
                                .Build() }, false, false, null, new ComponentBuilder()
                                    .WithButton("Open Editor", "verificationFormEditor")
                                    .WithButton("Finish Setup", "setupNext", ButtonStyle.Success)
                                .Build()).GetAwaiter().GetResult();
                                module.serverMemory[server]["SetupStep"] = 7;
                            } else if (((int)module.serverMemory[server]["SetupStep"]) == 7) {
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
                                conf.Set("verifiedSparkRole", module.serverMemory[server]["SetupVerifiedSparkRole"]);
                                
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
                    } catch (Exception e) {
                        channel.SendMessageAsync("", false, new EmbedBuilder()
                            .WithTitle("Queen Guard: An error occured")
                            .WithDescription("An error occured while processing the interaction.\nException: " + e.GetType().Name + (e.Message == null || e.Message == "" ? "" : ": " + e.Message) + "\n\n" + e.StackTrace)
                            .WithColor(Color.Red)
                        .Build()).GetAwaiter().GetResult();
                    }
                }

                return Task.CompletedTask;
            };
        }

        private bool canViewChannel(SocketRole role, SocketTextChannel ch)
        {
            var ovr = ch.GetPermissionOverwrite(role);
            if (ovr != null && ovr.HasValue) {
                if (ovr.Value.ViewChannel == PermValue.Inherit) {
                    return role.Permissions.ViewChannel;    
                }
                return ovr.Value.ViewChannel == PermValue.Allow;
            } else {
                if (!role.IsEveryone) {
                    if (!canViewChannel(role.Guild.EveryoneRole, ch))
                        return false;
                }
                return role.Permissions.ViewChannel;
            }
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
            try {
                Server server = GetBot().GetServerFromSocketGuild(guild);
                Server.ModuleConfig conf = server.GetModuleConfig(module);
                List<SelectMenuOptionBuilder> options = new List<SelectMenuOptionBuilder>();
                int i = 0;
                foreach (SocketRole role in guild.Roles) {
                    if (!role.IsEveryone && !role.Permissions.Administrator && (role.Name.ToLower().Contains("spark") || role.Name.ToLower().Contains("member"))) {
                        i++;
                        if (i == 25)
                            break; // FIXME: Temporary workaround so that ferever can put this bot to use
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
                foreach (SocketRole role in guild.Roles) {
                    if (!role.IsEveryone && !role.Permissions.Administrator && !role.Name.ToLower().Contains("spark") && !role.Name.ToLower().Contains("member")) {
                        i++;
                        if (i == 25)
                            break; // FIXME: Temporary workaround so that ferever can put this bot to use
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

                try {
                    await channel.SendMessageAsync("", false, new EmbedBuilder()
                        .WithTitle("Queen Guard Setup: Step 1")
                        .WithDescription("Please select what role to assign to verified members.")
                        .WithColor(Color.Gold)
                    .Build(), null, null, null, new ComponentBuilder()
                        .WithSelectMenu("sparkRole", options)
                        .WithButton("Next", "setupNext", ButtonStyle.Success)
                    .Build());
                    module.serverMemory[server]["SetupStep"] = 1;
                } catch {
                    channel.SendMessageAsync("", false, new EmbedBuilder()
                        .WithTitle("Queen Guard: An error occured")
                        .WithDescription("Could not create a 'Member Role' selection list, this is most likely caused by incompatible roles.")
                        .WithColor(Color.Red)
                    .Build()).GetAwaiter().GetResult();
                }
            } catch (Exception e) {
                await channel.SendMessageAsync("", false, new EmbedBuilder()
                    .WithTitle("Queen Guard Setup: An error occured")
                    .WithDescription("An error occured while running the setup process.\nException: " + e.GetType().Name + (e.Message == null || e.Message == "" ? "" : ": " + e.Message) + "\n\n" + e.StackTrace)
                    .WithColor(Color.Red)
                .Build());
            }
        }
        
        public override void OnExecuteFromTerminal(string fullcommand, string arguments_string, List<string> arguments) {
        }
    }
}
