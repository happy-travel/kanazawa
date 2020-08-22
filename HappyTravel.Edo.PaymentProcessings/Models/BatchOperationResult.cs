namespace HappyTravel.Edo.PaymentProcessings.Models
{
    public readonly struct BatchOperationResult
    {
        public BatchOperationResult(string message, bool hasErrors)
        {
            Message = message;
            HasErrors = hasErrors;
        }


        /// <summary>
        ///     Process result message
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Set to true if there were errors in processing
        /// </summary>
        public bool HasErrors { get; }
    }
}