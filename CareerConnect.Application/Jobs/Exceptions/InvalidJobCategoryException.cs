namespace CareerConnect.Application.Jobs.Exceptions;

public sealed class InvalidJobCategoryException : Exception
{
    public InvalidJobCategoryException(string message) : base(message)
    {
    }
}
