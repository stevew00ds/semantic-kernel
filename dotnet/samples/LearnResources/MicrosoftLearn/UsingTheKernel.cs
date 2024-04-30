﻿// Copyright (c) Microsoft. All rights reserved.

// <NecessaryPackages>
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Plugins.Core;
// </NecessaryPackages>

namespace Examples;

/// <summary>
/// This example demonstrates how to interact with the kernel as described at
/// https://learn.microsoft.com/semantic-kernel/agents/kernel
/// </summary>
public class UsingTheKernel(ITestOutputHelper output) : BaseTest(output)
{
    [Fact]
    public async Task RunAsync()
    {
        Console.WriteLine("======== Kernel ========");

        string? endpoint = TestConfiguration.AzureOpenAI.Endpoint;
        string? modelId = TestConfiguration.AzureOpenAI.ChatModelId;
        string? apiKey = TestConfiguration.AzureOpenAI.ApiKey;

        if (endpoint is null || modelId is null || apiKey is null)
        {
            Console.WriteLine("Azure OpenAI credentials not found. Skipping example.");

            return;
        }

        // Create a kernel with a logger and Azure OpenAI chat completion service
        // <KernelCreation>
        var builder = Kernel.CreateBuilder()
                            .AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);
        builder.Services.AddLogging(c => c.AddDebug().SetMinimumLevel(LogLevel.Trace));
        builder.Plugins.AddFromType<TimePlugin>();
        builder.Plugins.AddFromPromptDirectory("./../../../Plugins/WriterPlugin");
        Kernel kernel = builder.Build();
        // </KernelCreation>

        // Get the current time
        // <InvokeUtcNow>
        var currentTime = await kernel.InvokeAsync("TimePlugin", "UtcNow");
        Console.WriteLine(currentTime);
        // </InvokeUtcNow>

        // Write a poem with the WriterPlugin.ShortPoem function using the current time as input
        // <InvokeShortPoem>
        var poemResult = await kernel.InvokeAsync("WriterPlugin", "ShortPoem", new()
        {
            { "input", currentTime }
        });
        Console.WriteLine(poemResult);
        // </InvokeShortPoem>
    }
}
