﻿// Copyright (c) Microsoft. All rights reserved.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI.Assistants;
using Azure.Core;
using Azure.Core.Pipeline;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Agents.OpenAI.Azure;
using Microsoft.SemanticKernel.Http;

namespace Microsoft.SemanticKernel.Agents.OpenAI;

/// <summary>
/// A <see cref="KernelAgent"/> specialization based on Open AI Assistant / GPT.
/// </summary>
public sealed partial class OpenAIAssistantAgent : KernelAgent
{
    private readonly Assistant _assistant;
    private readonly AssistantsClient _client;
    private readonly OpenAIAssistantConfiguration _config;

    /// <summary>
    /// A list of previously uploaded file IDs to attach to the assistant.
    /// </summary>
    public IReadOnlyList<string> FileIds => this._assistant.FileIds;

    /// <summary>
    /// A set of up to 16 key/value pairs that can be attached to an agent, used for
    /// storing additional information about that object in a structured format.Keys
    /// may be up to 64 characters in length and values may be up to 512 characters in length.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata => this._assistant.Metadata;

    /// <summary>
    /// Expose predefined tools.
    /// </summary>
    internal IReadOnlyList<ToolDefinition> Tools => this._assistant.Tools;

    /// <summary>
    /// Set when the assistant has been deleted via <see cref="DeleteAsync(CancellationToken)"/>.
    /// An assistant removed by other means will result in an exception when invoked.
    /// </summary>
    public bool IsDeleted { get; private set; }

    /// <summary>
    /// Define a new <see cref="OpenAIAssistantAgent"/>.
    /// </summary>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="config">Configuration for accessing the Assistants API service, such as the api-key.</param>
    /// <param name="definition">The assistant definition.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="OpenAIAssistantAgent"/> instance</returns>
    public static async Task<OpenAIAssistantAgent> CreateAsync(
        Kernel kernel,
        OpenAIAssistantConfiguration config,
        OpenAIAssistantDefinition definition,
        CancellationToken cancellationToken = default)
    {
        // Validate input
        Verify.NotNull(kernel, nameof(kernel));
        Verify.NotNull(config, nameof(config));
        Verify.NotNull(definition, nameof(definition));

        // Create the client
        AssistantsClient client = CreateClient(config);

        // Create the assistant
        AssistantCreationOptions assistantCreationOptions = CreateAssistantCreationOptions(definition);
        Assistant model = await client.CreateAssistantAsync(assistantCreationOptions, cancellationToken).ConfigureAwait(false);

        // Instantiate the agent
        return
            new OpenAIAssistantAgent(client, model, config)
            {
                Kernel = kernel,
            };
    }

    /// <summary>
    /// Retrieve a list of assistant definitions: <see cref="OpenAIAssistantDefinition"/>.
    /// </summary>
    /// <param name="config">Configuration for accessing the Assistants API service, such as the api-key.</param>
    /// <param name="maxResults">The maximum number of assistant definitions to retrieve</param>
    /// <param name="lastId">The identifier of the assistant beyond which to begin selection.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>An list of <see cref="OpenAIAssistantDefinition"/> objects.</returns>
    public static async IAsyncEnumerable<OpenAIAssistantDefinition> ListDefinitionsAsync(
        OpenAIAssistantConfiguration config,
        int maxResults = 100,
        string? lastId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Create the client
        AssistantsClient client = CreateClient(config);

        // Retrieve the assistants
        PageableList<Assistant> assistants;

        int resultCount = 0;
        do
        {
            assistants = await client.GetAssistantsAsync(limit: Math.Min(maxResults, 100), ListSortOrder.Descending, after: lastId, cancellationToken: cancellationToken).ConfigureAwait(false);
            foreach (Assistant assistant in assistants)
            {
                if (resultCount >= maxResults)
                {
                    break;
                }

                resultCount++;

                yield return
                    new()
                    {
                        Id = assistant.Id,
                        Name = assistant.Name,
                        Description = assistant.Description,
                        Instructions = assistant.Instructions,
                        EnableCodeInterpreter = assistant.Tools.Any(t => t is CodeInterpreterToolDefinition),
                        EnableRetrieval = assistant.Tools.Any(t => t is RetrievalToolDefinition),
                        FileIds = assistant.FileIds,
                        Metadata = assistant.Metadata,
                        ModelId = assistant.Model,
                    };

                lastId = assistant.Id;
            }
        }
        while (assistants.HasMore && resultCount < maxResults);
    }

    /// <summary>
    /// Retrieve a <see cref="OpenAIAssistantAgent"/> by identifier.
    /// </summary>
    /// <param name="kernel">The <see cref="Kernel"/> containing services, plugins, and other state for use throughout the operation.</param>
    /// <param name="config">Configuration for accessing the Assistants API service, such as the api-key.</param>
    /// <param name="id">The agent identifier</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests. The default is <see cref="CancellationToken.None"/>.</param>
    /// <returns>An <see cref="OpenAIAssistantAgent"/> instance</returns>
    public static async Task<OpenAIAssistantAgent> RetrieveAsync(
        Kernel kernel,
        OpenAIAssistantConfiguration config,
        string id,
        CancellationToken cancellationToken = default)
    {
        // Create the client
        AssistantsClient client = CreateClient(config);

        // Retrieve the assistant
        Assistant model = await client.GetAssistantAsync(id, cancellationToken).ConfigureAwait(false);

        // Instantiate the agent
        return
            new OpenAIAssistantAgent(client, model, config)
            {
                Kernel = kernel,
            };
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        if (this.IsDeleted)
        {
            return;
        }

        this.IsDeleted = (await this._client.DeleteAssistantAsync(this.Id, cancellationToken).ConfigureAwait(false)).Value;
    }

    /// <inheritdoc/>
    protected override IEnumerable<string> GetChannelKeys()
    {
        // Distinguish from other channel types.
        yield return typeof(AgentChannel<OpenAIAssistantAgent>).FullName;

        // Distinguish between different Azure OpenAI endpoints or OpenAI services.
        yield return this._config.Endpoint ?? "openai";

        // Distinguish between different API versioning.
        if (this._config.Version.HasValue)
        {
            yield return this._config.Version!.ToString();
        }

        // Custom client receives dedicated channel.
        if (this._config.HttpClient != null)
        {
            if (this._config.HttpClient.BaseAddress != null)
            {
                yield return this._config.HttpClient.BaseAddress.AbsoluteUri;
            }

            foreach (string header in this._config.HttpClient.DefaultRequestHeaders.SelectMany(h => h.Value))
            {
                yield return header;
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task<AgentChannel> CreateChannelAsync(ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogDebug("[{MethodName}] Creating assistant thread", nameof(CreateChannelAsync));

        AssistantThread thread = await this._client.CreateThreadAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation("[{MethodName}] Created assistant thread: {ThreadId}", nameof(CreateChannelAsync), thread.Id);

        return new OpenAIAssistantChannel(this._client, thread.Id, this._config.Polling);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIAssistantAgent"/> class.
    /// </summary>
    private OpenAIAssistantAgent(
        AssistantsClient client,
        Assistant model,
        OpenAIAssistantConfiguration config)
    {
        this._assistant = model;
        this._client = client;
        this._config = config;

        this.Description = this._assistant.Description;
        this.Id = this._assistant.Id;
        this.Name = this._assistant.Name;
        this.Instructions = this._assistant.Instructions;
    }

    private static AssistantCreationOptions CreateAssistantCreationOptions(OpenAIAssistantDefinition definition)
    {
        AssistantCreationOptions assistantCreationOptions =
            new(definition.ModelId)
            {
                Description = definition.Description,
                Instructions = definition.Instructions,
                Name = definition.Name,
                Metadata = definition.Metadata?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            };

        assistantCreationOptions.FileIds.AddRange(definition.FileIds ?? []);

        if (definition.EnableCodeInterpreter)
        {
            assistantCreationOptions.Tools.Add(new CodeInterpreterToolDefinition());
        }

        if (definition.EnableRetrieval)
        {
            assistantCreationOptions.Tools.Add(new RetrievalToolDefinition());
        }

        return assistantCreationOptions;
    }

    private static AssistantsClient CreateClient(OpenAIAssistantConfiguration config)
    {
        AssistantsClientOptions clientOptions = CreateClientOptions(config);

        // Inspect options
        if (!string.IsNullOrWhiteSpace(config.Endpoint))
        {
            // Create client configured for Azure OpenAI, if endpoint definition is present.
            return new AssistantsClient(new Uri(config.Endpoint), new AzureKeyCredential(config.ApiKey), clientOptions);
        }

        // Otherwise, create client configured for OpenAI.
        return new AssistantsClient(config.ApiKey, clientOptions);
    }

    private static AssistantsClientOptions CreateClientOptions(OpenAIAssistantConfiguration config)
    {
        AssistantsClientOptions options =
            config.Version.HasValue ?
                new(config.Version.Value) :
                new();

        options.Diagnostics.ApplicationId = HttpHeaderConstant.Values.UserAgent;
        options.AddPolicy(new AddHeaderRequestPolicy(HttpHeaderConstant.Names.SemanticKernelVersion, HttpHeaderConstant.Values.GetAssemblyVersion(typeof(OpenAIAssistantAgent))), HttpPipelinePosition.PerCall);

        if (config.HttpClient is not null)
        {
            options.Transport = new HttpClientTransport(config.HttpClient);
            options.RetryPolicy = new RetryPolicy(maxRetries: 0); // Disable Azure SDK retry policy if and only if a custom HttpClient is provided.
            options.Retry.NetworkTimeout = Timeout.InfiniteTimeSpan; // Disable Azure SDK default timeout
        }

        return options;
    }
}
