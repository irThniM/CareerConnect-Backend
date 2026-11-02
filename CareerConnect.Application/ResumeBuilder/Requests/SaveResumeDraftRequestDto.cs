namespace CareerConnect.Application.ResumeBuilder.Requests;

public sealed class SaveResumeDraftRequestDto
{
    public long? TemplateId { get; set; }
    public string ResumeDataJson { get; set; } = "{}";
}
