namespace ScaryParty.Pizzeria.Domain.Types
{
    public enum CommandError : byte
    {
        None = 0,
        NotReady = 1,
        WrongGeneration = 2,
        StaleRevision = 3,
        OutOfRange = 4,
        Blocked = 5,
        NotOwner = 6,
        SlotFull = 7,
        SlotEmpty = 8,
        StockEmpty = 9,
        Busy = 10,
        MissingTool = 11,
        ToolBusy = 12,
        Incompatible = 13,
        Expired = 14,
        AlreadyAllocated = 15,
        InsufficientFunds = 16,
        LimitReached = 17,
        NoActiveOrder = 18,
        InvalidOperation = 19,
        ItemNotFound = 20
    }

    public readonly struct CommandResult
    {
        public readonly bool Success;
        public readonly CommandError Error;
        public readonly string Message;

        private CommandResult(bool success, CommandError error, string message)
        {
            Success = success;
            Error = error;
            Message = message ?? string.Empty;
        }

        public static CommandResult Ok() => new CommandResult(true, CommandError.None, null);
        public static CommandResult Fail(CommandError error, string message = null) => new CommandResult(false, error, message);

        public override string ToString() => Success ? "Ok" : $"Error({Error}: {Message})";
    }

    public readonly struct CommandResult<T>
    {
        public readonly bool Success;
        public readonly CommandError Error;
        public readonly string Message;
        public readonly T Value;

        private CommandResult(bool success, CommandError error, string message, T value)
        {
            Success = success;
            Error = error;
            Message = message ?? string.Empty;
            Value = value;
        }

        public static CommandResult<T> Ok(T value) => new CommandResult<T>(true, CommandError.None, null, value);
        public static CommandResult<T> Fail(CommandError error, string message = null) => new CommandResult<T>(false, error, message, default);

        public static implicit operator CommandResult(CommandResult<T> typed) => typed.Success ? CommandResult.Ok() : CommandResult.Fail(typed.Error, typed.Message);
        public override string ToString() => Success ? $"Ok({Value})" : $"Error({Error}: {Message})";
    }
}
