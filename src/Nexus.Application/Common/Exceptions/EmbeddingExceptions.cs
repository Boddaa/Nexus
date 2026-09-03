namespace Nexus.Application.Common.Exceptions;

public class EmbeddingException : Exception
{
    public EmbeddingException(string message) : base(message)
    {
    }

    public EmbeddingException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class EmbeddingConfigurationException : EmbeddingException
{
    public EmbeddingConfigurationException(string message) : base(message)
    {
    }
}
