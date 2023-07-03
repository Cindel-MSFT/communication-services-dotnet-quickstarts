namespace CallAutomation_TryPstnBackendService
{
    /// <summary>
    /// Configuration associated with the call.
    /// </summary>
    public class CallConfiguration
    {
        public CallConfiguration()
        {

        }

        /// <summary>
        /// The connectionstring of Azure Communication Service resource.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The phone number to add to the call
        /// </summary>
        public string TargetPhoneNumber { get; set; }

        /// <summary>
        /// The phone number associated with the source. 
        /// </summary>
        public string SourcePhoneNumber { get; set; }

        /// <summary>
        /// The base url of the application.
        /// </summary>
        public string AppBaseUri { get; set; }

        /// <summary>
        /// The cognitive service resources endpoint used for media features
        /// </summary>
        public string CognitiveServiceEndpoint { get; set; }

        /// <summary>
        /// The base url of the application.
        /// </summary>
        public string EventCallBackRoute { get; set; }

        /// <summary>
        /// The callback url of the application where notification would be received.
        /// </summary>
        public string CallbackEventUri => $"{AppBaseUri}" + EventCallBackRoute + $"/{Guid.NewGuid()}";
    }
}