namespace CareerConnect.Application.Applications.Exceptions;

public enum ApplicationWorkflowError
{
    CandidateProfileNotFound,
    ResumeRequired,
    JobUnavailable,
    AlreadyApplied,
    ApplicationNotFound,
    EmployerAccessDenied,
    InvalidStatus,
    CoverLetterTooLong
}

public sealed class ApplicationWorkflowException : Exception
{
    public ApplicationWorkflowException(ApplicationWorkflowError error, string message) : base(message)
    {
        Error = error;
    }

    public ApplicationWorkflowError Error { get; }
}
