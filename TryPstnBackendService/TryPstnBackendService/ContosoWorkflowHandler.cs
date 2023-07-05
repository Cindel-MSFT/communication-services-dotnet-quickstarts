using Azure.Communication.CallAutomation;
using Azure.Communication;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace TryPstnBackendService
{
    public class ContosoWorkflowHandler : IWorkflowHandler
    {
        private readonly string playSourceBaseId;
        private readonly string callerId;
        private readonly ILogger<ContosoWorkflowHandler> logger;

        public ContosoWorkflowHandler(string playSourceBaseId, string callerId)
        {
            this.playSourceBaseId = playSourceBaseId;
            this.callerId = callerId;
            this.logger = new LoggerFactory().CreateLogger<ContosoWorkflowHandler>();
        }

        public async Task HandleAsync(
            string textToRead,
            CallAutomationEventBase @event,
            CallConnection callConnection,
            CallMedia callConnectionMedia)
        {
            if (@event is CallConnected)
            {
                await HandleUserMessageAsync(callConnectionMedia, textToRead);
            }

            if (@event is RecognizeCompleted)
            {
                var recognizeCompletedEvent = (RecognizeCompleted)@event;
                switch (recognizeCompletedEvent.RecognizeResult)
                {
                    case ChoiceResult choiceResult:
                        var labelDetected = choiceResult.Label;

                        if (labelDetected.Equals(ContosoSelections.EndCall, StringComparison.OrdinalIgnoreCase))
                        {
                            logger.LogInformation($"RecognizeCompleted event received for call connection id: {@event.CallConnectionId}");
                            var playSource = $"You've chosen to end the call. Goodbye!".ToSsmlPlaySource();

                            await callConnectionMedia.PlayToAllAsync(new PlayToAllOptions(playSource) { OperationContext = "EndCall", Loop = false });
                        }
                        else
                            logger.LogError($"Unexpected recognize event result identified for connection id: {@event.CallConnectionId}");
                        break;
                    default:
                        logger.LogError($"Unexpected recognize event result identified for connection id: {@event.CallConnectionId}");
                        break;
                }
            }

            if (@event is RecognizeFailed)
            {
                var recognizeFailedEvent = (RecognizeFailed)@event;

                // Check for time out, and then play audio message
                if (recognizeFailedEvent.ReasonCode.Equals(MediaEventReasonCode.RecognizeInitialSilenceTimedOut))
                {
                    logger.LogInformation($"Recognition timed out for call connection id: {@event.CallConnectionId}");
                    var playSource = $"No input received and recognition timed out. Your call will be disconnected, thank you!".ToSsmlPlaySource();

                    //Play text prompt for no response received
                    await callConnectionMedia.PlayToAllAsync(new PlayToAllOptions(playSource) { OperationContext = "NoResponseToChoice", Loop = false });
                }

                //Check for invalid speech option or invalid tone detection
                if (recognizeFailedEvent.ReasonCode.Equals(MediaEventReasonCode.RecognizeSpeechOptionNotMatched))
                {
                    logger.LogInformation($"Recognition failed for invalid speech detected, connection id: {@event.CallConnectionId}");
                    var playSource = "Invalid speech phrase detected. Your call will be disconnected, thank you!".ToSsmlPlaySource();

                    //Play text prompt for speech option not matched
                    await callConnectionMedia.PlayToAllAsync(new PlayToAllOptions(playSource) { OperationContext = "ResponseToChoiceNotMatched", Loop = false});
                }
                else if (recognizeFailedEvent.ReasonCode.Equals(MediaEventReasonCode.RecognizeIncorrectToneDetected))
                {
                    logger.LogInformation($"Recognition failed for invalid tone detected, connection id: {@event.CallConnectionId}");
                    var playSource = "An invalid key was pressed. Your call will be disconnected, thank you!".ToSsmlPlaySource();

                    //Play text prompt for key tone not matched
                    await callConnectionMedia.PlayToAllAsync(new PlayToAllOptions(playSource) { OperationContext = "ResponseToChoiceNotMatched", Loop = false});
                }
            }

            if (@event is PlayCompleted { OperationContext: "EndCall" } ||
                @event is PlayCompleted { OperationContext: "NoResponseToChoice" } ||
                @event is PlayCompleted { OperationContext: "ResponseToChoiceNotMatched"})
            {
                logger.LogInformation($"PlayCompleted event received for call connection id: {@event.CallConnectionId}");
                await callConnection.HangUpAsync(forEveryone: true);
            }

            if (@event is PlayFailed)
            {
                logger.LogInformation($"PlayFailed event received for call connection id: {@event.CallConnectionId}");
                await callConnection.HangUpAsync(forEveryone: true);
            }
        }

        async Task HandleUserMessageAsync(CallMedia callConnectionMedia, string textToRead)
        {
            var greetingPlaySource = $"{textToRead}".ToSsmlPlaySource();
            await callConnectionMedia.PlayToAllAsync(new PlayToAllOptions(greetingPlaySource) { OperationContext = "UserMessage", Loop = false });

            var choices = new List<RecognizeChoice>
            {
                new RecognizeChoice(ContosoSelections.EndCall, new List<string> { "end call", "hang up", "One" })
                {
                    Tone = DtmfTone.One
                }
            };

            var endCallPlaySource = $"To end the call, please press one or say end call".ToSsmlPlaySource();

            var recognizeOptions =
                new CallMediaRecognizeChoiceOptions(targetParticipant: CommunicationIdentifier.FromRawId(callerId),
                recognizeChoices: choices)
                {
                    InterruptPrompt = false,
                    InitialSilenceTimeout = TimeSpan.FromSeconds(10),
                    Prompt = endCallPlaySource,
                    OperationContext = "EndTone"
                };

            //Start recognition 
            await callConnectionMedia.StartRecognizingAsync(recognizeOptions);
        }

        string GetPlaySourceId(string name)
        {
            return playSourceBaseId + name;
        }
    }
}