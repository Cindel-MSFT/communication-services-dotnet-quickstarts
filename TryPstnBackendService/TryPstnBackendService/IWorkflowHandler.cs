using Azure.Communication.CallAutomation;

namespace TryPstnBackendService
{
    public interface IWorkflowHandler
    {
        Task HandleAsync(string textToRead,
            CallAutomationEventBase @event,
            CallConnection callConnection,
            CallMedia callConnectionMedia);
    }
}
