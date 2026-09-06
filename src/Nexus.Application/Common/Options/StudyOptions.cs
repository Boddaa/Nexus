namespace Nexus.Application.Common.Options;

public class StudyOptions
{
    public const string SectionName = "Study";

    public int MaxFlashcardGenerationCount { get; set; } = 20;
    public int MaxQuizQuestionCount { get; set; } = 20;
    public int MaxContextCharacters { get; set; } = 6000;
    public double StrongMasteryThreshold { get; set; } = 80.0;
    public double DevelopingMasteryThreshold { get; set; } = 60.0;
    public int DefaultTutorMaxSources { get; set; } = 5;
}
