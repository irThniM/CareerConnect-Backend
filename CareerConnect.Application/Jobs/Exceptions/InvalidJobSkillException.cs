namespace CareerConnect.Application.Jobs.Exceptions;

public sealed class InvalidJobSkillException : Exception
{
    public InvalidJobSkillException(string message) : base(message)
    {
    }
}
