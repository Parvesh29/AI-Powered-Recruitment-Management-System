using System.Text.RegularExpressions;
using HireFlow.Domain.Entities.Candidates;
using HireFlow.Domain.Entities.Jobs;

namespace HireFlow.Application.Matching;

public sealed record MatchingWeights(decimal RequiredSkills = 0.50m, decimal Experience = 0.25m, decimal ProjectRelevance = 0.15m, decimal Education = 0.10m)
{
    public bool IsValid => RequiredSkills >= 0 && Experience >= 0 && ProjectRelevance >= 0 && Education >= 0 &&
                           Math.Abs(RequiredSkills + Experience + ProjectRelevance + Education - 1m) < 0.0001m;
}

public sealed record MatchCategory(string Label, decimal Weight, decimal Score, string Explanation);
public sealed record CandidateMatchEvaluation(decimal Score, string AlgorithmVersion, IReadOnlyCollection<string> MatchedSkills,
    IReadOnlyCollection<string> MissingRequiredSkills, IReadOnlyCollection<MatchCategory> Categories, IReadOnlyCollection<string> DataWarnings);

public static partial class CandidateMatchingEngine
{
    public const string AlgorithmVersion = "deterministic-1.0";
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "with", "from", "that", "this", "will", "your", "have", "years", "year", "work", "team", "role", "join", "must", "able", "using", "their", "about", "into", "for", "and", "the", "our", "you", "are", "who", "all", "one", "over", "such", "including", "experience", "required", "preferred", "candidate", "engineer", "developer"
    };

    public static CandidateMatchEvaluation Evaluate(Job job, IReadOnlyCollection<(string Name, bool IsRequired)> jobSkills,
        Candidate candidate, IReadOnlyCollection<string> candidateSkills, IReadOnlyCollection<CandidateEducation> education,
        IReadOnlyCollection<CandidateExperience> experience, MatchingWeights? configuredWeights = null)
    {
        var weights = configuredWeights is { IsValid: true } ? configuredWeights : new MatchingWeights();
        var required = jobSkills.Where(x => x.IsRequired).Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var matched = required.Where(requiredSkill => candidateSkills.Any(candidateSkill => SkillsMatch(requiredSkill, candidateSkill)))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var matchedSet = matched.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = required.Where(x => !matchedSet.Contains(x)).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        var skillScore = required.Length == 0 ? 100m : 100m * matched.Length / required.Length;

        var experienceScore = job.MinimumExperienceYears <= 0 ? 100m : candidate.TotalExperienceYears is decimal years
            ? Math.Clamp(years / job.MinimumExperienceYears * 100m, 0m, 100m) : 0m;
        var experienceExplanation = candidate.TotalExperienceYears is decimal knownYears
            ? $"Candidate lists {knownYears:0.##} years; job minimum is {job.MinimumExperienceYears}."
            : "Candidate experience is not provided.";

        var jobTerms = Tokenize(job.Title + " " + job.Description).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var evidence = string.Join(' ', new[] { candidate.ProfessionalSummary ?? string.Empty }
            .Concat(experience.Select(x => $"{x.JobTitle} {x.Description}"))
            .Concat(candidateSkills));
        var evidenceTerms = Tokenize(evidence).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projectScore = jobTerms.Count == 0 ? 0m : 100m * jobTerms.Intersect(evidenceTerms, StringComparer.OrdinalIgnoreCase).Count() / jobTerms.Count;

        var requiredEducation = EducationRequirements(job.Description);
        var educationText = string.Join(' ', education.Select(x => $"{x.Degree} {x.FieldOfStudy} {x.Institution}"));
        var educationScore = requiredEducation.Count == 0 ? 100m : requiredEducation.Count(x => educationText.Contains(x, StringComparison.OrdinalIgnoreCase)) * 100m / requiredEducation.Count;

        var categories = new[]
        {
            new MatchCategory("Required skills", weights.RequiredSkills, Math.Round(skillScore, 2), required.Length == 0 ? "No required skills were specified." : $"Matched {matched.Length} of {required.Length} required skills."),
            new MatchCategory("Experience", weights.Experience, Math.Round(experienceScore, 2), experienceExplanation),
            new MatchCategory("Project relevance", weights.ProjectRelevance, Math.Round(projectScore, 2), jobTerms.Count == 0 ? "No job description evidence was available." : $"Candidate evidence overlaps {Math.Round(projectScore, 0)}% of filtered job description terms."),
            new MatchCategory("Education", weights.Education, Math.Round(educationScore, 2), requiredEducation.Count == 0 ? "No explicit degree or certification requirement was identified." : $"Evidence matched {Math.Round(educationScore, 0)}% of identified education requirements.")
        };
        var score = Math.Round(categories.Sum(x => x.Weight * x.Score), 2);
        var warnings = new List<string>();
        if (candidate.TotalExperienceYears is null) warnings.Add("experience_missing");
        if (required.Length == 0) warnings.Add("required_skills_not_configured");
        if (evidenceTerms.Count == 0) warnings.Add("project_evidence_missing");
        if (education.Count == 0 && requiredEducation.Count > 0) warnings.Add("education_evidence_missing");
        return new CandidateMatchEvaluation(score, AlgorithmVersion, matched, missing, categories, warnings);
    }

    private static HashSet<string> EducationRequirements(string description)
    {
        var terms = new[] { "bachelor", "master", "phd", "doctorate", "degree", "certification", "certified", "diploma" };
        return terms.Where(x => description.Contains(x, StringComparison.OrdinalIgnoreCase)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> Tokenize(string text) => TokenPattern().Matches(text.ToLowerInvariant()).Select(x => x.Value)
        .Where(x => x.Length >= 4 && !StopWords.Contains(x));

    private static bool SkillsMatch(string required, string candidate)
    {
        var requiredKey = NormalizeSkill(required);
        var candidateKey = NormalizeSkill(candidate);
        if (requiredKey == candidateKey) return true;
        return requiredKey == "SQL" && candidateKey is "SQLSERVER" or "TSQL";
    }

    private static string NormalizeSkill(string value)
    {
        var normalized = Regex.Replace(value.ToUpperInvariant(), "[^A-Z0-9+#]", string.Empty);
        return normalized is "MSSQL" or "MSSQLSERVER" ? "SQLSERVER" : normalized;
    }

    [GeneratedRegex("[a-z0-9+#.]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TokenPattern();
}
