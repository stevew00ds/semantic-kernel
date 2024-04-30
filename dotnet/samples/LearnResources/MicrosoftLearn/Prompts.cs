﻿// Copyright (c) Microsoft. All rights reserved.

using Microsoft.SemanticKernel;

namespace Examples;

/// <summary>
/// This example demonstrates how to use prompts as described at
/// https://learn.microsoft.com/semantic-kernel/prompts/your-first-prompt
/// </summary>
public class Prompts(ITestOutputHelper output) : BaseTest(output)
{
    [Fact]
    public async Task RunAsync()
    {
        Console.WriteLine("======== Prompts ========");

        string? endpoint = TestConfiguration.AzureOpenAI.Endpoint;
        string? modelId = TestConfiguration.AzureOpenAI.ChatModelId;
        string? apiKey = TestConfiguration.AzureOpenAI.ApiKey;

        if (endpoint is null || modelId is null || apiKey is null)
        {
            Console.WriteLine("Azure OpenAI credentials not found. Skipping example.");

            return;
        }

        // <KernelCreation>
        Kernel kernel = Kernel.CreateBuilder()
                              .AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey)
                              .Build();
        // </KernelCreation>

        // 0.0 Initial prompt
        //////////////////////////////////////////////////////////////////////////////////
        string request = "I want to send an email to the marketing team celebrating their recent milestone.";
        string prompt = $"What is the intent of this request? {request}";

        /* Uncomment this code to make this example interactive
        // <InitialPrompt>
        Console.Write("Your request: ");
        string request = ReadLine()!;
        string prompt = $"What is the intent of this request? {request}";
        // </InitialPrompt>
        */

        Console.WriteLine("0.0 Initial prompt");
        // <InvokeInitialPrompt>
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));
        // </InvokeInitialPrompt>

        // 1.0 Make the prompt more specific
        //////////////////////////////////////////////////////////////////////////////////
        // <MoreSpecificPrompt>
        prompt = @$"What is the intent of this request? {request}
        You can choose between SendEmail, SendMessage, CompleteTask, CreateDocument.";
        // </MoreSpecificPrompt>

        Console.WriteLine("1.0 Make the prompt more specific");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));

        // 2.0 Add structure to the output with formatting
        //////////////////////////////////////////////////////////////////////////////////
        // <StructuredPrompt>
        prompt = @$"Instructions: What is the intent of this request?
        Choices: SendEmail, SendMessage, CompleteTask, CreateDocument.
        User Input: {request}
        Intent: ";
        // </StructuredPrompt>

        Console.WriteLine("2.0 Add structure to the output with formatting");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));

        // 2.1 Add structure to the output with formatting (using Markdown and JSON)
        //////////////////////////////////////////////////////////////////////////////////
        // <FormattedPrompt>
        prompt = $$"""
                 ## Instructions
                 Provide the intent of the request using the following format:
                 
                 ```json
                 {
                     "intent": {intent}
                 }
                 ```
                 
                 ## Choices
                 You can choose between the following intents:
                 
                 ```json
                 ["SendEmail", "SendMessage", "CompleteTask", "CreateDocument"]
                 ```
                 
                 ## User Input
                 The user input is:
                 
                 ```json
                 {
                     "request": "{{request}}"
                 }
                 ```
                 
                 ## Intent
                 """;
        // </FormattedPrompt>

        Console.WriteLine("2.1 Add structure to the output with formatting (using Markdown and JSON)");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));

        // 3.0 Provide examples with few-shot prompting
        //////////////////////////////////////////////////////////////////////////////////
        // <FewShotPrompt>
        prompt = @$"Instructions: What is the intent of this request?
Choices: SendEmail, SendMessage, CompleteTask, CreateDocument.

User Input: Can you send a very quick approval to the marketing team?
Intent: SendMessage

User Input: Can you send the full update to the marketing team?
Intent: SendEmail

User Input: {request}
Intent: ";
        // </FewShotPrompt>

        Console.WriteLine("3.0 Provide examples with few-shot prompting");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));

        // 4.0 Tell the AI what to do to avoid doing something wrong
        //////////////////////////////////////////////////////////////////////////////////
        // <AvoidPrompt>
        prompt = $"""
                 Instructions: What is the intent of this request?
                 If you don't know the intent, don't guess; instead respond with "Unknown".
                 Choices: SendEmail, SendMessage, CompleteTask, CreateDocument, Unknown.

                 User Input: Can you send a very quick approval to the marketing team?
                 Intent: SendMessage

                 User Input: Can you send the full update to the marketing team?
                 Intent: SendEmail

                 User Input: {request}
                 Intent: 
                 """;
        // </AvoidPrompt>

        Console.WriteLine("4.0 Tell the AI what to do to avoid doing something wrong");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));

        // 5.0 Provide context to the AI
        //////////////////////////////////////////////////////////////////////////////////
        // <ContextPrompt>
        string history = """
                         User input: I hate sending emails, no one ever reads them.
                         AI response: I'm sorry to hear that. Messages may be a better way to communicate.
                         """;

        prompt = $"""
                 Instructions: What is the intent of this request?
                 If you don't know the intent, don't guess; instead respond with "Unknown".
                 Choices: SendEmail, SendMessage, CompleteTask, CreateDocument, Unknown.
                 
                 User Input: Can you send a very quick approval to the marketing team?
                 Intent: SendMessage
                 
                 User Input: Can you send the full update to the marketing team?
                 Intent: SendEmail
                 
                 {history}
                 User Input: {request}
                 Intent: 
                 """;
        // </ContextPrompt>

        Console.WriteLine("5.0 Provide context to the AI");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));

        // 6.0 Using message roles in chat completion prompts
        //////////////////////////////////////////////////////////////////////////////////
        // <RolePrompt>
        history = """
                  <message role="user">I hate sending emails, no one ever reads them.</message>
                  <message role="assistant">I'm sorry to hear that. Messages may be a better way to communicate.</message>
                  """;

        prompt = $"""
                 <message role="system">Instructions: What is the intent of this request?
                 If you don't know the intent, don't guess; instead respond with "Unknown".
                 Choices: SendEmail, SendMessage, CompleteTask, CreateDocument, Unknown.</message>
                 
                 <message role="user">Can you send a very quick approval to the marketing team?</message>
                 <message role="system">Intent:</message>
                 <message role="assistant">SendMessage</message>
                 
                 <message role="user">Can you send the full update to the marketing team?</message>
                 <message role="system">Intent:</message>
                 <message role="assistant">SendEmail</message>
                 
                 {history}
                 <message role="user">{request}</message>
                 <message role="system">Intent:</message>
                 """;
        // </RolePrompt>

        Console.WriteLine("6.0 Using message roles in chat completion prompts");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));

        // 7.0 Give your AI words of encouragement
        //////////////////////////////////////////////////////////////////////////////////
        // <BonusPrompt>
        history = """
                  <message role="user">I hate sending emails, no one ever reads them.</message>
                  <message role="assistant">I'm sorry to hear that. Messages may be a better way to communicate.</message>
                  """;

        prompt = $"""
                 <message role="system">Instructions: What is the intent of this request?
                 If you don't know the intent, don't guess; instead respond with "Unknown".
                 Choices: SendEmail, SendMessage, CompleteTask, CreateDocument, Unknown.
                 Bonus: You'll get $20 if you get this right.</message>
                
                 <message role="user">Can you send a very quick approval to the marketing team?</message>
                 <message role="system">Intent:</message>
                 <message role="assistant">SendMessage</message>
                
                 <message role="user">Can you send the full update to the marketing team?</message>
                 <message role="system">Intent:</message>
                 <message role="assistant">SendEmail</message>
                
                 {history}
                 <message role="user">{request}</message>
                 <message role="system">Intent:</message>
                 """;
        // </BonusPrompt>

        Console.WriteLine("7.0 Give your AI words of encouragement");
        Console.WriteLine(await kernel.InvokePromptAsync(prompt));
    }
}
