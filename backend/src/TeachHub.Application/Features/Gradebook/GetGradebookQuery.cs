using Microsoft.EntityFrameworkCore;
using TeachHub.Application.Common;
using TeachHub.Application.Common.Messaging;
using TeachHub.Application.Features.Offerings;
using TeachHub.Domain.Grading;
using TeachHub.Domain.Submissions;

namespace TeachHub.Application.Features.Gradebook;

/// <param name="Provisional">
/// Provisional view: ungraded work is EXCLUDED from the denominator instead of counting as 0.
/// It only changes what is displayed, it must never change the data — see the note in the handler.
/// </param>
public sealed record GetGradebookQuery(Guid OfferingId, Guid InstructorId, bool Provisional = false)
    : IQuery<Result<GradebookDto>>;

public sealed class GetGradebookHandler(IAppDbContext db)
    : IQueryHandler<GetGradebookQuery, Result<GradebookDto>>
{
    public async Task<Result<GradebookDto>> Handle(GetGradebookQuery request, CancellationToken ct)
    {
        if (!await OfferingOwnership.OwnsOfferingAsync(db, request.OfferingId, request.InstructorId, ct))
            return Result<GradebookDto>.NotFound("Không tìm thấy lớp học phần.");

        var offering = await db.CourseOfferings.AsNoTracking()
            .Where(o => o.Id == request.OfferingId)
            .Select(o => new
            {
                o.Id, o.ClassCode, o.BonusDefaultTarget, o.BonusDefaultColumnId,
                o.BonusMaxPointsPerStudent, o.GradeConditions,
                Code = db.Subjects.Where(s => s.Id == o.SubjectId).Select(s => s.Code).Single(),
                Name = db.Subjects.Where(s => s.Id == o.SubjectId).Select(s => s.Name).Single(),
            })
            .SingleAsync(ct);

        // AsNoTracking on EVERY read-only query, and the "provisional" variant builds a NEW OBJECT
        // instead of mutating a tracked entity. GradeAggregation is an owned entity with setters:
        // mutate it, then let any SaveChangesAsync in the same scope run, and you have permanently
        // overwritten how the whole class is graded — no notice, no log, nobody able to trace it.
        var columns = await db.GradeColumns.AsNoTracking()
            .Include(c => c.Sources)
            .Where(c => c.OfferingId == request.OfferingId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(ct);

        // "Provisional" mode builds a COPY of the aggregation rule, it never touches the column's
        // Aggregation. Aggregation is an owned entity with setters: mutate it, then let any
        // SaveChangesAsync in the same scope run, and the whole class's grading is overwritten for good.
        var rules = columns.ToDictionary(c => c.Id, c => new GradeAggregation
        {
            Kind = c.Aggregation.Kind,
            N = c.Aggregation.N,
            TreatMissingAsZero = !request.Provisional && c.Aggregation.TreatMissingAsZero,
        });

        var students = await db.Enrollments.AsNoTracking()
            .Where(e => e.OfferingId == request.OfferingId)
            .OrderBy(e => e.FullName)
            .Select(e => new { e.Id, e.StudentCode, e.FullName, e.ClassName })
            .ToListAsync(ct);

        var columnIds = columns.Select(c => c.Id).ToList();

        var gradeRecords = await db.GradeRecords.AsNoTracking()
            .Where(r => columnIds.Contains(r.GradeColumnId))
            .ToListAsync(ct);

        var bonusPoints = await db.BonusPoints.AsNoTracking()
            .Where(b => b.OfferingId == request.OfferingId)
            .ToListAsync(ct);

        // Scores coming from the sources: read the original max scale AT THE SAME TIME as the
        // scores. Typing new SourceScore(score, 10m) here disables normalization altogether, and a
        // rubric graded on a 20-point scale lands in the column as-is and is then clamped at 10 —
        // wrong, and wrong on the lenient side.
        var sources = columns.SelectMany(c => c.Sources).ToList();
        var assignmentIds = sources.Where(s => s.SourceType == GradeSourceType.Assignment).Select(s => s.SourceId).ToList();
        var finalAssessmentIds = sources.Where(s => s.SourceType == GradeSourceType.FinalAssessment).Select(s => s.SourceId).ToList();

        var assignmentMaxScores = await db.Assignments.AsNoTracking()
            .Where(a => assignmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.MaxScore, ct);

        var finalAssessmentMaxScores = await db.FinalAssessments.AsNoTracking()
            .Where(f => finalAssessmentIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.MaxScore, ct);

        // Quiz sources (Phase 5). QuizAttempt already stores NormalizedScore on the 10-point scale,
        // so the original scale here is 10 — normalize ONCE at grading time, never a second time.
        var quizIds = sources.Where(s => s.SourceType == GradeSourceType.Quiz).Select(s => s.SourceId).ToList();

        var quizScores = await db.QuizAttempts.AsNoTracking()
            .Where(a => quizIds.Contains(a.QuizId) && a.NormalizedScore != null)
            .Select(a => new { a.QuizId, a.EnrollmentId, a.NormalizedScore })
            .ToListAsync(ct);

        var submissions = await db.Submissions.AsNoTracking()
            .Where(s => s.OfferingId == request.OfferingId && s.IsCurrent && s.Score != null)
            .Select(s => new { s.Id, s.AssignmentId, s.FinalAssessmentId, s.EnrollmentId, s.GroupId, s.Score })
            .ToListAsync(ct);

        // Per-member scores on group work (UC-10b, Phase 6b). Read here rather than re-deriving the
        // rule somewhere else: MemberScoring.Resolve is the ONLY copy of the "an individual score
        // beats the shared score" rule, and every duplicate of it is one more place that will drift.
        var submissionIds = submissions.Select(s => s.Id).ToList();

        var memberScores = await db.SubmissionMemberScores.AsNoTracking()
            .Where(m => submissionIds.Contains(m.SubmissionId))
            .ToListAsync(ct);

        // Group work: the group's score is every member's score (the MVP default).
        var groupMembers = await db.GroupMembers.AsNoTracking()
            .Where(m => db.StudentGroups.Any(g => g.Id == m.GroupId && g.OfferingId == request.OfferingId))
            .Select(m => new { m.GroupId, m.EnrollmentId })
            .ToListAsync(ct);

        var settings = new GradebookSettings
        {
            DefaultTarget = offering.BonusDefaultTarget,
            DefaultColumnId = offering.BonusDefaultColumnId,
            MaxBonusPerStudent = offering.BonusMaxPointsPerStudent,
        };

        // Casting to a nullable type and then ?? [] is NECESSARY even though the property is
        // declared non-nullable: rows created BEFORE this migration have grade_conditions = NULL in
        // the DB, and EF materializes an empty jsonb column as null, not as an empty list. The bug
        // only shows up on a database that already holds data — you never hit it on an empty DB.
        var conditions = (IReadOnlyList<GradeCondition>?)offering.GradeConditions ?? [];
        var unknownKeys = new HashSet<string>();
        var rows = new List<GradeRowDto>(students.Count);

        foreach (var e in students)
        {
            var bonusLedger = BonusLedger.Build(
                bonusPoints.Where(b => b.EnrollmentId == e.Id).Select(b => b.ToEntry()), settings);

            var rawScoreByColumn = new Dictionary<Guid, decimal>();
            var cells = new List<GradeCellDto>(columns.Count);

            foreach (var c in columns)
            {
                var record = gradeRecords.FirstOrDefault(r => r.EnrollmentId == e.Id && r.GradeColumnId == c.Id);

                var columnSources = c.Sources.Select(s => new SourceScore(
                    ScoreOfSource(s, e.Id),
                    s.SourceType switch
                    {
                        GradeSourceType.Assignment => assignmentMaxScores.GetValueOrDefault(s.SourceId, 10m),
                        GradeSourceType.FinalAssessment => finalAssessmentMaxScores.GetValueOrDefault(s.SourceId, 10m),
                        _ => 10m,                       // Quiz is already normalized to the 10-point scale
                    },
                    s.WeightWithinColumn)).ToList();

                var manualOverride = record?.IsManualOverride ?? false;

                var rawScore = manualOverride || rules[c.Id].Kind == AggregationKind.Manual
                    ? Math.Clamp(record?.Score ?? 0m, 0m, settings.MaxScore)
                    : GradeAggregator.Apply(rules[c.Id], columnSources, settings.MaxScore);

                rawScoreByColumn[c.Id] = rawScore;

                cells.Add(new GradeCellDto(
                    c.Id, rawScore,
                    GradebookCalculator.ColumnScore(c, rawScore, bonusLedger, settings),
                    record?.IsManualOverride ?? false,
                    bonusLedger.ForColumn(c.Id)));
            }

            var finalScore = GradebookCalculator.FinalScore(columns, rawScoreByColumn, bonusLedger, conditions, settings);

            // Warn about conditions pointing at a column that does not exist: staying silent here
            // means the subject's minimum-score rule is not in effect and nobody knows it.
            foreach (var k in GradeConditionEvaluator
                         .Apply(finalScore, columns, cells.ToDictionary(x => x.ColumnId, x => x.Score), conditions)
                         .UnknownColumnKeys)
                unknownKeys.Add(k);

            rows.Add(new GradeRowDto(
                e.Id, e.StudentCode, e.FullName, e.ClassName, cells, bonusLedger.ToFinalTotal, finalScore));

            continue;

            decimal? ScoreOfSource(GradeColumnSource s, Guid enrollmentId)
            {
                // Quiz: take the student's HIGHEST attempt. The whole point of allowing several
                // attempts is to let them keep re-learning until it sticks; taking the last attempt
                // means one misclick wipes out a good earlier result.
                if (s.SourceType == GradeSourceType.Quiz)
                    return quizScores.Where(x => x.QuizId == s.SourceId && x.EnrollmentId == enrollmentId)
                                     .Select(x => x.NormalizedScore).Max();

                var myGroupIds = groupMembers.Where(m => m.EnrollmentId == enrollmentId)
                                             .Select(m => m.GroupId).ToList();

                var submission = submissions.FirstOrDefault(x =>
                    (s.SourceType == GradeSourceType.Assignment
                        ? x.AssignmentId == s.SourceId
                        : x.FinalAssessmentId == s.SourceId)
                    && (x.EnrollmentId == enrollmentId
                        || (x.GroupId != null && myGroupIds.Contains(x.GroupId.Value))));

                if (submission is null) return null;

                // If a GROUP submission carries a per-member score for this very student, that
                // per-member score wins. Skip this check and an instructor who has just graded one
                // member individually still sees the shared score in the gradebook, and will
                // conclude that grading a member individually does nothing.
                if (submission.GroupId is not null)
                {
                    var memberScore = memberScores.FirstOrDefault(
                        m => m.SubmissionId == submission.Id && m.EnrollmentId == enrollmentId);

                    if (memberScore is not null) return memberScore.Score;
                }

                return submission.Score;
            }
        }

        var weights = GradebookCalculator.ValidateColumns(columns);

        return Result<GradebookDto>.Ok(new GradebookDto(
            offering.Id, offering.ClassCode, offering.Code, offering.Name,
            [.. columns.Select(ToColumnDto)],
            rows,
            [.. conditions.Select(d => new GradeConditionDto(d.ColumnKey, d.Op, d.Threshold, d.Action, d.ActionValue))],
            offering.BonusDefaultTarget, offering.BonusDefaultColumnId, offering.BonusMaxPointsPerStudent,
            weights.IsValid, weights.Error, [.. unknownKeys]));

        GradeColumnDto ToColumnDto(GradeColumn c) => new(
            c.Id, c.Key, c.Name, c.WeightPercent, c.OrderIndex,
            c.Aggregation.Kind, c.Aggregation.N, c.Aggregation.TreatMissingAsZero,
            [.. c.Sources.Select(s => new GradeColumnSourceDto(
                s.Id, s.SourceType, s.SourceId,
                s.SourceType switch
                {
                    GradeSourceType.Assignment =>
                        db.Assignments.Where(a => a.Id == s.SourceId).Select(a => a.Title).FirstOrDefault() ?? "(đã xóa)",
                    GradeSourceType.Quiz =>
                        db.Quizzes.Where(q => q.Id == s.SourceId).Select(q => q.Title).FirstOrDefault() ?? "(đã xóa)",
                    _ =>
                        db.FinalAssessments.Where(f => f.Id == s.SourceId).Select(f => f.Title).FirstOrDefault() ?? "(đã xóa)",
                },
                s.WeightWithinColumn))]);
    }
}
