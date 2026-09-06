namespace Nexus.Application.Common.Exceptions;

public class LlmException : Exception
{
    public LlmException(string message) : base(message) { }
    public LlmException(string message, Exception innerException) : base(message, innerException) { }
}

public class LlmConfigurationException : LlmException
{
    public LlmConfigurationException(string message) : base(message) { }
}
