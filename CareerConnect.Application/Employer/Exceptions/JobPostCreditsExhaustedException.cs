namespace CareerConnect.Application.Employer.Exceptions;

public sealed class JobPostCreditsExhaustedException : Exception
{
    public JobPostCreditsExhaustedException(string message) : base(message)
    {
    }
}
