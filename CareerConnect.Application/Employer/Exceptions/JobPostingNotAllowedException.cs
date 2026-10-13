namespace CareerConnect.Application.Employer.Exceptions;

public sealed class JobPostingNotAllowedException : Exception
{
    public JobPostingNotAllowedException(string message) : base(message)
    {
    }
}
