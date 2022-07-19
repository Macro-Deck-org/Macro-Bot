﻿using Develeon64.MacroBot.Logging;
using Develeon64.MacroBot.Models;
using Develeon64.MacroBot.Services;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Develeon64.MacroBot.Commands.Tagging
{
    [Group("tag", "Tag system")]
    public class TaggingCommandsModule : InteractionModuleBase<SocketInteractionContext>
    {

        public static List<UserTagAssignable> createTagAssignments = new();
        [SlashCommand("create", "Create a new tag")]
        public async Task Create([Summary(description: "Name of the tag")] string name)
        {
            // Handle Permissions
            SocketGuildUser guildUser = Context.Guild.GetUser(Context.User.Id);
            ulong[] requiredPermissions = ConfigManager.CommandsConfig.Tagging.PermissionManageTags;
            if (!checkPermissions(requiredPermissions, guildUser)) {

                await RespondAsync(embed: buildPermissionError(requiredPermissions), ephemeral: true);
                return;
            }

            // Insert user into Assignments cache to get tag name when the Modal is beeing sent
            UserTagAssignable? assignable = createTagAssignments.Find(x => x.guildId == Context.Guild.Id && x.userId == Context.User.Id);
            if (assignable != null)
            {
                createTagAssignments.Remove(assignable);
            }
            createTagAssignments.Add(new UserTagAssignable(Context.Guild.Id, Context.User.Id, name));

            // Send Modal Prompt

            await RespondWithModalAsync<TagCreateModal>("tag_create_modal");
        }

        [SlashCommand("delete", "Delete an existing tag")]
        public async Task Delete()
        {
            await RespondAsync("WIP");
        }

        [SlashCommand("edit", "Delete an existing tag")]
        public async Task Edit()
        {
            await RespondAsync("WIP");
        }

        private static List<Tag> tagList = new();
        [AutocompleteCommand("tag", "view")]
        public async Task AutoCompleteView()
        {
            string userInput = (Context.Interaction as SocketAutocompleteInteraction).Data.Current.Value.ToString();

            List<AutocompleteResult> resultList = new();
            foreach (Tag tag in await DatabaseManager.GetTagsForGuild(Context.Guild.Id))
            {
                resultList.Add(new AutocompleteResult(tag.Name, tag.Name));
            }
            IEnumerable<AutocompleteResult> results = resultList.AsEnumerable().Where(x => x.Name.StartsWith(userInput, StringComparison.InvariantCultureIgnoreCase));
            
            await (Context.Interaction as SocketAutocompleteInteraction).RespondAsync(results.Take(25));
        }
        [SlashCommand("view", "View a tag")]
        public async Task View([Summary("tag"), Autocomplete] string tagName)
        {
            Tag tag = await DatabaseManager.GetTag(tagName, Context.Guild.Id);
            
            if (tag == null)
            {
                EmbedBuilder embedBuilder = new EmbedBuilder()
                {
                    Title = "Tag not found",
                    Description = $"The tag `{tagName}` could not be found in the database!"
                };
                embedBuilder.WithColor(new Color(255, 50, 50));
                await RespondAsync(embed: embedBuilder.Build());
                return;
            }


            EmbedBuilder embed = new EmbedBuilder()
            {
                Title = tagName,
                Description = tag.Content,
            };
            SocketGuildUser author = Context.Guild.GetUser(tag.Author);
            if (author != null)
            {
                embed.WithFooter($"Tag written by {author.DisplayName}",author.GetAvatarUrl());
            } else
            {
                embed.WithFooter($"Tag written by ${tag.Author}");
            }

            await RespondAsync(embed: embed.Build());
        }



        private static Boolean checkPermissions(ulong[] permissions, SocketGuildUser user)
        {
            List<SocketRole> userRoles = user.Roles.ToList<SocketRole>();

            foreach (ulong permission in permissions)
            {
                if (userRoles.Find(x => x.Id.ToString() == permission.ToString()) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private static Embed buildPermissionError(ulong[] permissions)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder()
            {
                Title = "Invalid Permissions",
                Description = "You do not meet the permission requirements to run this command!"
            };
            String permissionList = "";
            foreach (ulong permissionID in permissions) permissionList += $"\n<@&{permissionID.ToString()}>";
            embedBuilder.AddField("Required Roles", $"At least one of those roles is required:{permissionList}");
            embedBuilder.WithColor(new Color(255, 50, 50));

            return embedBuilder.Build();
        }
    }

    public class TaggingInteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [ModalInteraction("tag_create_modal")]
        public async Task HandleCreateModal(TagCreateModal modal)
        {
            UserTagAssignable? assignable = TaggingCommandsModule.createTagAssignments.Find(x => x.guildId == Context.Guild.Id && x.userId == Context.User.Id);
            if (assignable != null)
            {
                await DatabaseManager.CreateTag(assignable.tagName, modal.TagContent, assignable.userId, assignable.guildId);

                EmbedBuilder embedBuilder = new EmbedBuilder()
                {
                    Title = "Tag Created",
                    Description = $"Successfully created tag `{assignable.tagName}`"
                };
                embedBuilder.AddField("Tag Content", modal.TagContent);
                embedBuilder.WithColor(new Color(50, 255, 50));

                TaggingCommandsModule.createTagAssignments.Remove(assignable);

                await RespondAsync(embed: embedBuilder.Build());
            } else
            {
                Logger.Warning(Modules.Tags, $"Could not find Tag name information for user {Context.User.Username} ({Context.User.Id}) in guild {Context.Guild.Name} ({Context.Guild.Id})!");
                await RespondAsync("An internal error occured while getting Tag Name information, please try again.", ephemeral: true);
            }
        }
    }

    public class TagCreateModal : IModal
    {
        public string Title => "Create Tag";
        [InputLabel("Tag Content")]
        [ModalTextInput("tag_create_content", TextInputStyle.Paragraph, minLength: 1, maxLength: 4000)]
        public string TagContent { get; set; }
    }

    public class TagEditModal : IModal
    {
        public string Title => "Edit Tag";
        [InputLabel("Tag Content")]
        [ModalTextInput("tag_create_content", TextInputStyle.Paragraph, minLength: 1, maxLength: 4000, placeholder: "Sadly we cannot provide the existing tag content here automatically due to Discord limitations")]
        public string TagContent { get; set; }
    }

    public class UserTagAssignable
    {
        // Apparently i have not found a more efficient way in asigning the tag name to an discord modal so i needed to temporarly save this assignment into a List

        public ulong userId { get; private set; }
        public ulong guildId { get; private set; }
        public string tagName{ get; private set; }
        public UserTagAssignable(ulong guildID,ulong userID,string tagName)
        {
            this.userId = userID;
            this.guildId = guildID;
            this.tagName = tagName;
        }
    }
}
