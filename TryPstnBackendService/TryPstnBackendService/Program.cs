﻿using Azure.Communication.CallAutomation;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Messaging.EventGrid.SystemEvents;
using CallAutomation_TryPstnBackendService;
using TryPstnBackendService;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//Fetch configuration and add call automation as singleton service
var playSourceBaseId = "0c4d8d7d-4000-498b-b78d-9f5864da21bf";
var callConfigurationSection = builder.Configuration.GetSection(nameof(CallConfiguration));
builder.Services.Configure<CallConfiguration>(callConfigurationSection);
var client = new CallAutomationClient(callConfigurationSection["ConnectionString"]);

//Below eventHandler showcases the Recognize Choices and Play TTS features
ContosoWorkflowHandler eventHandler = null;

//Use below if setting up the tunnel using VS Dev tunnel 
var callbackUriBase = Environment.GetEnvironmentVariable("VS_TUNNEL_URL").TrimEnd('/');
if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")))
{
    callbackUriBase = string.Format("https://{0}.azurewebsites.net/", Environment.ExpandEnvironmentVariables("%WEBSITE_SITE_NAME%"));
}

var cogSvcUri = callConfigurationSection["CognitiveServiceEndpoint"];
builder.Services.AddSingleton(client);

var app = builder.Build();

var outgoingCallIdentity = await app.ProvisionAzureCommunicationServicesIdentity(callConfigurationSection["ConnectionString"]);

// api to answer incoming calls
app.MapPost("/api/incomingCall", async (
    [FromBody] EventGridEvent[] eventGridEvents,
    ILogger<Program> logger) =>
{
    foreach (var eventGridEvent in eventGridEvents)
    {
        logger.LogInformation($"Incoming Call event received : {JsonConvert.SerializeObject(eventGridEvent)}");
        // Handle system events
        if (eventGridEvent.TryGetSystemEventData(out object eventData))
        {
            // Handle the subscription validation event.
            if (eventData is SubscriptionValidationEventData subscriptionValidationEventData)
            {
                var responseData = new SubscriptionValidationResponse
                {
                    ValidationResponse = subscriptionValidationEventData.ValidationCode
                };
                return Results.Ok(responseData);
            }
        }
        var jsonObject = GetJsonObject(eventGridEvent.Data);
        var textToRead = GetTextToRead();
        var callerId = GetCallerId(jsonObject);
        eventHandler = new ContosoWorkflowHandler(playSourceBaseId, callerId);
        var incomingCallContext = GetIncomingCallContext(jsonObject);
        var callbackUri = new Uri(callbackUriBase + $"/api/callbacks/{Guid.NewGuid()}?textToRead={textToRead}");
        var options = new AnswerCallOptions(incomingCallContext, callbackUri)
        {
            AzureCognitiveServicesEndpointUrl = new Uri(cogSvcUri)
        };
        AnswerCallResult answerCallResult = await client.AnswerCallAsync(options);
    }
    return Results.Ok();
});

// api to handle call back events
app.MapPost("/api/callbacks/{contextId}", async (
    [FromBody] CloudEvent[] cloudEvents,
    [FromRoute] string contextId,
    [Required] string textToRead,
    CallAutomationClient callAutomationClient, 
    IOptions<CallConfiguration> callConfiguration, 
    ILogger<Program> logger) =>
{
    foreach (var cloudEvent in cloudEvents)
    {
        logger.LogInformation($"Event received: {JsonConvert.SerializeObject(cloudEvent)}");

        CallAutomationEventBase @event = CallAutomationEventParser.Parse(cloudEvent);
        var callConnection = callAutomationClient.GetCallConnection(@event.CallConnectionId);
        var callConnectionMedia = callConnection.GetCallMedia();

        await eventHandler.HandleAsync(textToRead, @event, callConnection, callConnectionMedia);
    }
    return Results.Ok();
}).Produces(StatusCodes.Status200OK);

JsonObject GetJsonObject(BinaryData data)
{
    return JsonNode.Parse(data).AsObject();
}

string GetTextToRead()
{
    return "Hello World";
}

string GetCallerId(JsonObject jsonObject)
{
    return (string)(jsonObject["from"]["rawId"]);
}
string GetIncomingCallContext(JsonObject jsonObject)
{
    return (string)jsonObject["incomingCallContext"];
}

string GetPlaySourceId(string name)
{
    return playSourceBaseId + name;
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseAuthorization();

app.MapControllers();
app.UseHttpsRedirection();
app.Run();