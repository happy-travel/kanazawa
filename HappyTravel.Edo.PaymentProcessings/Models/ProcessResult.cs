namespace HappyTravel.Edo.PaymentProcessings.Models
{
    public readonly struct ProcessResult
    {
        public ProcessResult(string message)
        {
            Message = message;
        }

        /// <summary>
        ///     Process result message
        /// </summary>
        public string Message { get; }
    }
}