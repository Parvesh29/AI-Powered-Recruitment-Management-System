using HireFlow.Application.Matching;
using HireFlow.Domain.Entities.Candidates;
using HireFlow.Domain.Entities.Jobs;

namespace HireFlow.Infrastructure.Tests;

public sealed class CandidateMatchingTests
{
    [Fact]
    public void EvaluationAppliesWeightsAndExplainsSkillGaps()
    {
        var job = new Job { Title = "Backend Developer", Description = "Build APIs with PostgreSQL and a bachelor's degree." , MinimumExperienceYears = 5 };
        var candidate = new Candidate { ProfessionalSummary = "Build APIs and reliable services.", TotalExperienceYears = 5 };
        var education = new[] { new CandidateEducation { Degree = "Bachelor degree", Institution = "State University" } };
        var result = CandidateMatchingEngine.Evaluate(job,
            new[] { ("C#", true), ("PostgreSQL", true) }, candidate,
            new[] { "C#", "PostgreSQL" }, education, Array.Empty<CandidateExperience>());

        Assert.Equal("deterministic-1.0", result.AlgorithmVersion);
        Assert.Equal(2, result.MatchedSkills.Count);
        Assert.Empty(result.MissingRequiredSkills);
        Assert.Equal(92.5m, result.Score);
        Assert.Equal(4, result.Categories.Count);
    }

    [Fact]
    public void MissingEvidenceIsReportedAndNeverFabricated()
    {
        var job = new Job { Title = "Data Engineer", Description = "Requires a master's degree and Python.", MinimumExperienceYears = 4 };
        var candidate = new Candidate();
        var result = CandidateMatchingEngine.Evaluate(job, new[] { ("Python", true) }, candidate,
            Array.Empty<string>(), Array.Empty<CandidateEducation>(), Array.Empty<CandidateExperience>());

        Assert.Empty(result.MatchedSkills);
        Assert.Equal(new[] { "Python" }, result.MissingRequiredSkills);
        Assert.Contains("experience_missing", result.DataWarnings);
        Assert.Contains("education_evidence_missing", result.DataWarnings);
    }
}

