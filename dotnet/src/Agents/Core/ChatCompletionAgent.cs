﻿// Copyright (c) Microsoft. All rights reserved.
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Microsoft.SemanticKernel.Agents;

/// <summary>
/// A <see cref="KernelAgent"/> specialization based on <see cref="IChatCompletionService"/>.
/// </summary>
/// <remarks>
/// NOTE: Enable OpenAIPromptExecutionSettings.ToolCallBehavior for agent plugins.
/// (<see cref="ChatCompletionAgent.ExecutionSettings"/>)
/// </remarks>
public sealed class ChatCompletionAgent : ChatHistoryKernelAgent
{
    /// <summary>
    /// Optional execution settings for the agent.
    /// </summary>
    public PromptExecutionSettings? ExecutionSettings { get; set; }

    /// <inheritdoc/>
    public override async IAsyncEnumerable<ChatMessageContent> InvokeAsync(
        IReadOnlyList<ChatMessageContent> history,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var chatCompletionService = this.Kernel.GetRequiredService<IChatCompletionService>();

        ChatHistory chat = [];
        if (!string.IsNullOrWhiteSpace(this.Instructions))
        {
            chat.Add(new ChatMessageContent(AuthorRole.System, this.Instructions) { AuthorName = this.Name });
        }
        chat.AddRange(history);

        int messageCount = chat.Count;

        IReadOnlyList<ChatMessageContent> messages =
            await chatCompletionService.GetChatMessageContentsAsync(
                chat,
                this.ExecutionSettings,
                this.Kernel,
                cancellationToken).ConfigureAwait(false);

        // Capture mutated messages related function calling / tools
        for (int messageIndex = messageCount; messageIndex < chat.Count; messageIndex++)
        {
            ChatMessageContent message = chat[messageIndex];

            message.AuthorName = this.Name;

            yield return message;
        }

        foreach (ChatMessageContent message in messages ?? [])
        {
            // TODO: MESSAGE SOURCE - ISSUE #5731
            message.AuthorName = this.Name;

            yield return message;
        }
    }
}
